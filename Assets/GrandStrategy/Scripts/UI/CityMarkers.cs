using System.Collections.Generic;
using GrandStrategy.Core;
using GrandStrategy.Map;
using UnityEngine;
using UnityEngine.UI;

namespace GrandStrategy.UI
{
    /// <summary>
    /// Iconos de capitales y ciudades importantes sobre el mapa, al estilo de los puntos de victoria de HOI4:
    /// <list type="bullet">
    /// <item>Capital: estrella oscura (verde si es la del jugador).</item>
    /// <item>Ciudad importante: círculo oscuro con punto; las más valiosas aparecen antes al acercar.</item>
    /// </list>
    /// No se ven con la cámara alejada: van apareciendo con el zoom (primero las capitales, luego las ciudades
    /// grandes y por último las demás) y los nombres solo de cerca. Los nombres que se pisarían se ocultan,
    /// dando prioridad a las capitales y a las ciudades con más puntos de victoria.
    /// </summary>
    public sealed class CityMarkers : MonoBehaviour
    {
        // Zoom (0 cerca, 1 lejos) en que cada elemento empieza a aparecer y en que se ve del todo.
        private const float CapitalIconStart = 0.6f, CapitalIconFull = 0.52f;
        private const float CapitalNameStart = 0.46f, CapitalNameFull = 0.4f;
        private const float CityIconStartMin = 0.3f, CityIconStartMax = 0.44f; // según puntos de victoria
        private const float CityIconRange = 0.07f;
        private const float CityNameStart = 0.3f, CityNameFull = 0.24f;

        // Iconos sobrios: casi negros con un fino borde claro; solo la capital del jugador destaca en verde.
        private static readonly Color CapitalColor = new Color(0.07f, 0.07f, 0.07f);
        private static readonly Color PlayerColor = new Color(0.36f, 0.78f, 0.32f);
        private static readonly Color CityColor = new Color(0.1f, 0.1f, 0.1f);
        private static readonly Color PlayerCityColor = new Color(0.12f, 0.3f, 0.12f);
        private static readonly Color NameColor = new Color(0.96f, 0.93f, 0.84f);
        private static readonly Color NightNameColor = new Color(1f, 0.98f, 0.92f);

        private sealed class Marker
        {
            public CityInfo City;
            public RectTransform Root;
            public Image Icon;
            public Text Name;
            public Outline NameOutline;
            public Image Plate;
            public float NameWidth;
            public float Latitude;
            public Vector3 World;
        }

        private GameManager _game;
        private RectTransform _rect;
        private readonly List<Marker> _markers = new List<Marker>();
        private readonly List<Rect> _placedNames = new List<Rect>();

        /// <summary>Rectángulos (coordenadas del canvas desde su centro) de los iconos y nombres visibles este fotograma.</summary>
        public static readonly List<Rect> Occupied = new List<Rect>();
        private static Sprite _star, _dot, _plate;

        public static CityMarkers Create(Transform parent, GameManager game)
        {
            var rt = UIFactory.Rect("CityMarkers", parent).Stretch();
            rt.SetAsFirstSibling(); // por debajo del resto de la interfaz
            var markers = rt.gameObject.AddComponent<CityMarkers>();
            markers._game = game;
            markers._rect = rt;
            markers.Build();
            return markers;
        }

        private void Build()
        {
            _star ??= CreateSprite(true);
            _dot ??= CreateSprite(false);
            _plate ??= CreatePlateSprite();

            var cities = new List<CityInfo>(_game.World.Cities);
            // Orden de prioridad: capitales primero, después por puntos de victoria.
            cities.Sort((a, b) => a.IsCapital != b.IsCapital ? (a.IsCapital ? -1 : 1) : b.VictoryPoints.CompareTo(a.VictoryPoints));
            foreach (var city in cities)
            {
                var root = UIFactory.Rect(city.Name, _rect);
                root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
                root.sizeDelta = Vector2.zero;

                var icon = new GameObject("Icono", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                icon.transform.SetParent(root, false);
                icon.sprite = city.IsCapital ? _star : _dot;
                icon.raycastTarget = false;
                float size = city.IsCapital ? 24f : Mathf.Lerp(11f, 16f, Mathf.InverseLerp(5f, 30f, city.VictoryPoints));
                icon.rectTransform.sizeDelta = new Vector2(size, size);

                // Placa oscura tras el nombre: solo de noche, cuando las luces de la ciudad restan contraste.
                var plate = new GameObject("Placa", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                plate.transform.SetParent(root, false);
                plate.sprite = _plate;
                plate.type = Image.Type.Sliced;
                plate.raycastTarget = false;

                var name = UIFactory.Label(root, city.Name, city.IsCapital ? 15 : 13, NameColor, TextAnchor.UpperCenter,
                    city.IsCapital ? FontStyle.Bold : FontStyle.Normal);
                name.horizontalOverflow = HorizontalWrapMode.Overflow;
                var shadow = name.gameObject.AddComponent<Outline>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
                shadow.effectDistance = new Vector2(1f, -1f);
                name.rectTransform.anchorMin = name.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                name.rectTransform.pivot = new Vector2(0.5f, 1f);
                name.rectTransform.sizeDelta = new Vector2(200f, 20f);
                name.rectTransform.anchoredPosition = new Vector2(0f, -size * 0.5f - 1f);
                float nameW = name.preferredWidth, nameH = city.IsCapital ? 18f : 16f;
                plate.rectTransform.anchorMin = plate.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                plate.rectTransform.pivot = new Vector2(0.5f, 1f);
                plate.rectTransform.sizeDelta = new Vector2(nameW + 12f, nameH + 2f);
                plate.rectTransform.anchoredPosition = new Vector2(0f, -size * 0.5f);

                var world = _game.Map.UVToWorld(city.UV);
                world.y += 0.02f;
                _markers.Add(new Marker
                {
                    City = city, Root = root, Icon = icon, Name = name, NameOutline = shadow, Plate = plate, NameWidth = nameW, World = world,
                    Latitude = _game.World.Map.LatitudeAt(city.UV.y) * Mathf.Deg2Rad,
                });
                root.gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            var state = _game.State;
            bool show = GameSettings.CityMarkers &&
                        (state == GameState.CountrySelection || state == GameState.Playing || state == GameState.Paused);
            var rig = _game.CameraRig;
            Occupied.Clear();
            if (!show || rig == null)
            {
                foreach (var m in _markers) if (m.Root.gameObject.activeSelf) m.Root.gameObject.SetActive(false);
                return;
            }

            var cam = rig.Camera;
            float zoom = rig.Zoom01;
            float worldWidth = _game.Map.WorldSize.x;
            float camX = cam.transform.position.x;
            Vector2 canvasSize = _rect.rect.size;
            float scale = canvasSize.y / Mathf.Max(1f, Screen.height);
            // De muy cerca los iconos crecen un poco.
            float iconScale = Mathf.Lerp(1.15f, 1f, Mathf.InverseLerp(0f, 0.3f, zoom));
            int playerId = _game.PlayerCountry != null ? _game.PlayerCountry.Id : -1;
            // Sol (mismo cálculo que el shader del terreno) para saber qué ciudades están de noche.
            var clock = _game.Clock;
            bool dayNight = GameSettings.DayNight && clock != null;
            float sunLon = dayNight ? clock.SubsolarLongitude * Mathf.Deg2Rad : 0f;
            float sunDecl = dayNight ? clock.SolarDeclination * Mathf.Deg2Rad : 0f;
            _placedNames.Clear();

            foreach (var m in _markers)
            {
                var city = m.City;
                float iconAlpha, nameAlpha;
                if (city.IsCapital)
                {
                    iconAlpha = Fade(zoom, CapitalIconStart, CapitalIconFull);
                    nameAlpha = Fade(zoom, CapitalNameStart, CapitalNameFull);
                }
                else
                {
                    float start = Mathf.Lerp(CityIconStartMin, CityIconStartMax, Mathf.InverseLerp(5f, 30f, city.VictoryPoints));
                    iconAlpha = Fade(zoom, start, start - CityIconRange);
                    nameAlpha = Fade(zoom, CityNameStart, CityNameFull);
                }

                if (iconAlpha <= 0.01f)
                {
                    if (m.Root.gameObject.activeSelf) m.Root.gameObject.SetActive(false);
                    continue;
                }

                // El mundo da la vuelta: se usa la copia de la ciudad más cercana a la cámara.
                var world = m.World;
                world.x += Mathf.Round((camX - world.x) / worldWidth) * worldWidth;
                Vector3 screen = cam.WorldToScreenPoint(world);
                if (screen.z <= 0f || screen.x < -60f || screen.x > Screen.width + 60f || screen.y < -40f || screen.y > Screen.height + 40f)
                {
                    if (m.Root.gameObject.activeSelf) m.Root.gameObject.SetActive(false);
                    continue;
                }

                if (!m.Root.gameObject.activeSelf) m.Root.gameObject.SetActive(true);
                Vector2 pos = new Vector2(screen.x, screen.y) * scale - canvasSize * 0.5f;
                m.Root.anchoredPosition = pos;
                m.Root.localScale = Vector3.one * iconScale;
                float iconSize = m.Icon.rectTransform.sizeDelta.x * iconScale;
                Occupied.Add(new Rect(pos.x - iconSize * 0.5f, pos.y - iconSize * 0.5f, iconSize, iconSize));

                bool own = city.OwnerId == playerId && playerId >= 0;
                var color = city.IsCapital ? (own ? PlayerColor : CapitalColor) : (own ? PlayerCityColor : CityColor);
                color.a = iconAlpha;
                m.Icon.color = color;

                // Nombre: solo si no pisa otro ya colocado (los de más prioridad van antes en la lista).
                bool nameVisible = nameAlpha > 0.01f;
                if (nameVisible)
                {
                    float w = m.NameWidth * iconScale + 6f, h = (city.IsCapital ? 18f : 16f) * iconScale;
                    var r = new Rect(pos.x - w * 0.5f, pos.y - h - 10f * iconScale, w, h + 10f * iconScale);
                    foreach (var other in _placedNames)
                        if (other.Overlaps(r)) { nameVisible = false; break; }
                    if (nameVisible)
                    {
                        _placedNames.Add(r);
                        Occupied.Add(r);
                    }
                }
                m.Name.enabled = nameVisible;
                m.Plate.enabled = nameVisible;
                if (nameVisible)
                {
                    float night = 0f;
                    if (dayNight)
                    {
                        float lon = (Mathf.Repeat(city.UV.x, 1f) * 2f - 1f) * Mathf.PI;
                        float cosZ = Mathf.Sin(m.Latitude) * Mathf.Sin(sunDecl) + Mathf.Cos(m.Latitude) * Mathf.Cos(sunDecl) * Mathf.Cos(lon - sunLon);
                        night = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.1f, 0.06f, cosZ));
                    }
                    var c = Color.Lerp(NameColor, NightNameColor, night);
                    c.a = nameAlpha;
                    m.Name.color = c;
                    m.NameOutline.effectColor = new Color(0f, 0f, 0f, Mathf.Lerp(0.75f, 1f, night) * nameAlpha);
                    m.Plate.color = new Color(0.02f, 0.025f, 0.04f, 0.6f * night * nameAlpha);
                }
            }
        }

        private static float Fade(float zoom, float start, float full) => Mathf.Clamp01((start - zoom) / Mathf.Max(1e-4f, start - full));

        /// <summary>Icono generado por código: estrella de 5 puntas o círculo con punto, con borde oscuro.</summary>
        private static Sprite CreateSprite(bool star)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = star ? "IconoCapital" : "IconoCiudad", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            var poly = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? 0.92f : 0.4f;
                poly[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }
            const float outline = 0.09f, aa = 2.5f / size;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                // Distancia con signo (negativa dentro).
                float d = star ? PolygonDistance(p, poly) : p.magnitude - 0.62f;
                float fill = Mathf.Clamp01(0.5f - d / aa);
                float border = Mathf.Clamp01(0.5f - (d - outline) / aa);
                // Interior claro (se tiñe con Image.color); borde fino claro semitransparente para que el icono
                // oscuro se lea sobre cualquier terreno; en la ciudad, punto central hueco.
                float inner = fill;
                if (!star)
                {
                    float dotD = p.magnitude - 0.22f;
                    inner *= Mathf.Clamp01(0.5f + dotD / aa);
                }
                float shade = star ? Mathf.Lerp(0.8f, 1f, (p.y + 1f) * 0.5f) : 1f;
                float rim = border * (1f - inner);
                float value = Mathf.Lerp(0.85f, shade, inner);
                float alpha = Mathf.Max(inner, rim * 0.55f);
                byte v = (byte)(255 * value);
                byte a = (byte)(255 * alpha);
                px[y * size + x] = new Color32(v, v, v, a);
            }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Rectángulo de esquinas redondeadas y bordes suaves (placa del nombre, 9-slice).</summary>
        private static Sprite CreatePlateSprite()
        {
            const int size = 32, radius = 10;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "PlacaNombre", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Max(radius - (x + 0.5f), (x + 0.5f) - (size - radius)), 0f);
                float dy = Mathf.Max(Mathf.Max(radius - (y + 0.5f), (y + 0.5f) - (size - radius)), 0f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01((radius - d) / radius * 1.6f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * a * a));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
        }

        private static float PolygonDistance(Vector2 p, Vector2[] poly)
        {
            float d = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[j], b = poly[i];
                Vector2 e = b - a, w = p - a;
                float t = Mathf.Clamp01(Vector2.Dot(w, e) / e.sqrMagnitude);
                d = Mathf.Min(d, (w - e * t).magnitude);
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside ? -d : d;
        }
    }
}
