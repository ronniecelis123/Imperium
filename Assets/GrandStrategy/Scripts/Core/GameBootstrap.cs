using System;
using System.Collections;
using System.Threading.Tasks;
using GrandStrategy.CameraControl;
using GrandStrategy.Map;
using GrandStrategy.Map.Generation;
using GrandStrategy.Map.Interaction;
using GrandStrategy.Map.IO;
using GrandStrategy.Map.Rendering;
using GrandStrategy.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GrandStrategy.Core
{
    /// <summary>
    /// Punto de entrada: basta con este componente en una escena vacía.
    /// Crea cámara, luz, post-procesado, mapa, UI y gestor de juego, y carga o genera el mundo.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Mapa")]
        [Tooltip("Carpeta dentro de StreamingAssets/Maps. 'world' = mapa mundial real. Si no existe se genera un mundo procedural.")]
        public string MapFolder = "world";
        [Tooltip("Ignorar los archivos y generar siempre un mundo ficticio con los ajustes de abajo.")]
        public bool UseProceduralWorld;
        public WorldGenSettings Generation = new WorldGenSettings();

        [Header("Render")]
        public float MapWorldWidth = 200f;
        [Tooltip("Altura real de la malla del terreno. Baja para que no haya picos al acercarse.")]
        public float TerrainHeight = 1.4f;

        private IEnumerator Start()
        {
            Application.targetFrameRate = 120;
            EnsureEventSystem();
            var cameraRig = SetupCamera();
            SetupLighting();
            SetupPostProcessing();

            var ui = new GameObject("UIManager").AddComponent<UIManager>();
            ui.CreateLoadingScreen();
            ui.Loading.SetProgress(0f, "Preparando...");
            yield return null;

            // --- Obtener el mundo: archivos o generación procedural ---
            WorldData world = null;
            string folder = MapFileIO.GetMapFolder(MapFolder);
            if (!UseProceduralWorld && MapFileIO.MapExists(folder))
            {
                var loadProgress = new GenerationProgress();
                bool done = false;
                var routine = MapFileIO.LoadAsync(folder, loadProgress,
                    w => { world = w; done = true; },
                    e =>
                    {
                        Debug.LogException(e);
                        Debug.LogWarning("[GameBootstrap] Falló la carga del mapa; se generará uno procedural.");
                        done = true;
                    });
                StartCoroutine(routine);
                while (!done)
                {
                    ui.Loading.SetProgress(loadProgress.Value * 0.85f, loadProgress.Status);
                    yield return null;
                }
                if (world != null)
                {
                    FlagGenerator.FlagFolder = System.IO.Path.Combine(folder, "flags");
                    PortraitLoader.LeaderFolder = System.IO.Path.Combine(folder, "leaders");
                }
            }
            else if (!UseProceduralWorld)
            {
                Debug.Log($"[GameBootstrap] No hay mapa en {folder}; se genera un mundo procedural.");
            }

            if (world == null)
            {
                var progress = new GenerationProgress();
                var settings = Generation;
                var task = Task.Run(() => new WorldGenerator(settings).Generate(progress));
                while (!task.IsCompleted)
                {
                    ui.Loading.SetProgress(progress.Value * 0.85f, progress.Status);
                    yield return null;
                }
                if (task.IsFaulted)
                {
                    Debug.LogException(task.Exception);
                    ui.Loading.SetError(task.Exception?.GetBaseException().Message);
                    yield break;
                }
                world = task.Result;
            }

            ui.Loading.SetProgress(0.9f, "Preparando gráficos del mapa...");
            yield return null;

            // --- Mapa ---
            var map = new GameObject("Map").AddComponent<MapRenderer>();
            map.WorldWidth = MapWorldWidth;
            map.MaxTerrainHeight = TerrainHeight;
            map.Build(world);
            cameraRig.ZoomChanged += map.SetZoom;
            cameraRig.TerrainHeightProvider = (x, z) => map.GetTerrainHeight(x / map.WorldSize.x, z / map.WorldSize.y);
            cameraRig.Setup(map.WorldBounds, 0.75f);
            map.SetZoom(cameraRig.Zoom01);

            ui.Loading.SetProgress(0.94f, "Plantando bosques, levantando ciudades y trazando ríos...");
            yield return null;
            var decorations = map.gameObject.AddComponent<MapDecorations>();
            decorations.Build(map, world, cameraRig);
            map.gameObject.AddComponent<CountryLabels>().Build(map, world);

            cameraRig.EdgeScrolling = GameSettings.EdgeScrolling;
            GameSettings.Changed += () => cameraRig.EdgeScrolling = GameSettings.EdgeScrolling;

            var interaction = new GameObject("MapInteraction").AddComponent<MapInteraction>();
            interaction.Initialize(cameraRig.Camera, map);

            // --- Juego y UI ---
            var gameGo = new GameObject("GameManager");
            var clock = gameGo.AddComponent<GameClock>();
            var economy = gameGo.AddComponent<Economy.EconomySystem>();
            economy.Initialize(world, clock);
            var game = gameGo.AddComponent<GameManager>();
            game.Initialize(world, map, cameraRig, interaction, clock, economy);
            // Las nubes se mueven con el tiempo del juego: quietas en pausa, rápidas a velocidad 5.
            // En los menús derivan despacio para que el fondo tenga vida.
            decorations.CloudSpeed = () => game.State == GameState.Playing
                ? (clock.Running ? Mathf.Pow(GameClock.HoursPerSecond[clock.Speed], 0.75f) : 0f)
                : game.State == GameState.Paused ? 0f : 0.6f;
            ui.Initialize(game);

            ui.Loading.SetProgress(1f, "Listo");
            yield return null;
            ui.HideLoadingScreen();
            game.ShowMainMenu();
            Debug.Log($"[GameBootstrap] Mundo '{world.Name}': {world.Provinces.Count} provincias, {world.Countries.Count} países, mapa {world.Map.Width}x{world.Map.Height}.");
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private static StrategyCamera SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.09f, 0.14f);
            cam.fieldOfView = 38f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1000f;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = true;

            var rig = cam.GetComponent<StrategyCamera>();
            if (rig == null) rig = cam.gameObject.AddComponent<StrategyCamera>();
            return rig;
        }

        private static void SetupLighting()
        {
            Light sun = null;
            foreach (var l in FindObjectsByType<Light>())
                if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            // Luz desde el noroeste, como en los mapas pintados (realza el relieve).
            sun.transform.rotation = Quaternion.Euler(48f, 140f, 0f);
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.None;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.36f, 0.4f);
            RenderSettings.fog = false;
        }

        private static void SetupPostProcessing()
        {
            var go = new GameObject("PostProcessing");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.5f);
            var color = profile.Add<ColorAdjustments>(true);
            color.contrast.Override(10f);
            color.saturation.Override(-6f);
            // Bloom suave: hace brillar las luces de las ciudades de noche y el reflejo del sol en el agua.
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.6f);
            bloom.scatter.Override(0.65f);
            var tonemap = profile.Add<Tonemapping>(true);
            tonemap.mode.Override(TonemappingMode.Neutral);
            volume.sharedProfile = profile;
        }
    }
}
