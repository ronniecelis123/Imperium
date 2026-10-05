using System.Collections.Generic;
using System.IO;
using GrandStrategy.Core;
using GrandStrategy.Map.Generation;
using GrandStrategy.Map.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GrandStrategy.EditorTools
{
    /// <summary>Menú "Grand Strategy" del editor.</summary>
    public static class GrandStrategyMenu
    {
        private const string ScenePath = "Assets/Scenes/GrandStrategy.unity";

        [MenuItem("Grand Strategy/Crear escena principal", priority = 0)]
        public static void CreateMainScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Grand Strategy",
                    "Unity está en modo Play. Detén el juego (botón ▶ o Ctrl+P) y vuelve a intentarlo.", "Entendido");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Grand Strategy", $"La escena {ScenePath} ya existe. ¿Sobrescribirla?", "Sí", "No"))
            {
                EditorSceneManager.OpenScene(ScenePath);
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            EditorSceneManager.SaveScene(scene, ScenePath);

            // Poner la escena la primera en Build Settings.
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log($"[Grand Strategy] Escena creada en {ScenePath}. Pulsa Play para empezar.");
        }

        [MenuItem("Grand Strategy/Exportar mapa procedural a StreamingAssets", priority = 20)]
        public static void ExportProceduralMap()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Grand Strategy", "Detén el modo Play antes de exportar el mapa.", "Entendido");
                return;
            }
            var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            var settings = bootstrap != null ? bootstrap.Generation : new WorldGenSettings();
            // Se exporta a una carpeta propia para no sobrescribir el mapa mundial.
            string folder = MapFileIO.GetMapFolder("procedural");

            if (MapFileIO.MapExists(folder) &&
                !EditorUtility.DisplayDialog("Grand Strategy", $"Ya existe un mapa en {folder}. ¿Sobrescribirlo?", "Sí", "No"))
                return;

            try
            {
                var progress = new GenerationProgress();
                EditorUtility.DisplayProgressBar("Grand Strategy", "Generando mundo (semilla " + settings.Seed + ")...", 0.1f);
                var world = new WorldGenerator(settings).Generate(progress);
                EditorUtility.DisplayProgressBar("Grand Strategy", "Escribiendo archivos...", 0.8f);
                MapFileIO.Export(world, folder);
                Debug.Log($"[Grand Strategy] Mapa exportado a {folder} ({world.Provinces.Count} provincias, {world.Countries.Count} países). " +
                          "Puedes editar provinces.png / definition.csv / countries.json.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            AssetDatabase.Refresh();
        }

        [MenuItem("Grand Strategy/Abrir carpeta de mapas", priority = 21)]
        public static void OpenMapsFolder()
        {
            string folder = Path.Combine(Application.streamingAssetsPath, MapFileIO.MapsFolderName);
            Directory.CreateDirectory(folder);
            EditorUtility.RevealInFinder(folder);
        }
    }
}
