using System;
using UnityEngine;

namespace GrandStrategy.Core
{
    /// <summary>Nivel de detalle de un elemento del mapa.</summary>
    public enum DetailLevel
    {
        Off = 0,
        Low = 1,
        Medium = 2,
        High = 3,
    }

    /// <summary>
    /// Ajustes del jugador (menú "Ajustes"). Se guardan en PlayerPrefs y avisan con <see cref="Changed"/>.
    /// Cada elemento del mapa tiene su propia calidad (desactivado / bajo / medio / alto).
    /// </summary>
    public static class GameSettings
    {
        private const string Prefix = "GS3_";

        public static event Action Changed;

        /// <summary>Árboles y vegetación.</summary>
        public static DetailLevel Forests { get => GetLevel(nameof(Forests)); set => SetLevel(nameof(Forests), value); }
        /// <summary>Edificios, calles, puertos y monumentos.</summary>
        public static DetailLevel Cities { get => GetLevel(nameof(Cities)); set => SetLevel(nameof(Cities), value); }
        public static DetailLevel Rivers { get => GetLevel(nameof(Rivers)); set => SetLevel(nameof(Rivers), value); }
        public static DetailLevel Clouds { get => GetLevel(nameof(Clouds)); set => SetLevel(nameof(Clouds), value); }
        /// <summary>Caminos y ferrocarriles entre ciudades.</summary>
        public static DetailLevel Roads { get => GetLevel(nameof(Roads)); set => SetLevel(nameof(Roads), value); }
        /// <summary>Detalle del suelo (texturas de cerca y campos de cultivo). No se puede desactivar: mínimo Bajo.</summary>
        public static DetailLevel Terrain
        {
            get { var l = GetLevel(nameof(Terrain)); return l == DetailLevel.Off ? DetailLevel.Low : l; }
            set => SetLevel(nameof(Terrain), value == DetailLevel.Off ? DetailLevel.Low : value);
        }

        public static bool DayNight { get => GetBool(nameof(DayNight), true); set => SetBool(nameof(DayNight), value); }
        /// <summary>Nombres de los países escritos sobre el mapa.</summary>
        public static bool CountryNames { get => GetBool(nameof(CountryNames), true); set => SetBool(nameof(CountryNames), value); }
        /// <summary>Iconos de capitales y ciudades importantes (aparecen al acercar la cámara).</summary>
        public static bool CityMarkers { get => GetBool(nameof(CityMarkers), true); set => SetBool(nameof(CityMarkers), value); }
        /// <summary>Mover la cámara llevando el ratón al borde de la pantalla.</summary>
        public static bool EdgeScrolling { get => GetBool(nameof(EdgeScrolling), true); set => SetBool(nameof(EdgeScrolling), value); }

        /// <summary>Aplica el mismo nivel a todos los elementos del mapa ("Gráficos bajos", "Gráficos altos"...).</summary>
        public static void ApplyPreset(DetailLevel level)
        {
            foreach (var key in new[] { nameof(Forests), nameof(Cities), nameof(Rivers), nameof(Clouds), nameof(Roads) })
                PlayerPrefs.SetInt(Prefix + key, (int)level);
            PlayerPrefs.SetInt(Prefix + nameof(Terrain), (int)(level == DetailLevel.Off ? DetailLevel.Low : level));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static DetailLevel GetLevel(string key)
        {
            try { return (DetailLevel)Mathf.Clamp(PlayerPrefs.GetInt(Prefix + key, (int)DetailLevel.High), 0, 3); }
            catch { return DetailLevel.High; }
        }

        private static void SetLevel(string key, DetailLevel value)
        {
            if (GetLevel(key) == value && PlayerPrefs.HasKey(Prefix + key)) return;
            PlayerPrefs.SetInt(Prefix + key, (int)value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static bool GetBool(string key, bool fallback)
        {
            try { return PlayerPrefs.GetInt(Prefix + key, fallback ? 1 : 0) != 0; }
            catch { return fallback; }
        }

        private static void SetBool(string key, bool value)
        {
            if (GetBool(key, !value) == value && PlayerPrefs.HasKey(Prefix + key)) return;
            PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
