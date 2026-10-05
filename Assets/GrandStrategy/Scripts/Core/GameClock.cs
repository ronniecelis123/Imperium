using System;
using UnityEngine;

namespace GrandStrategy.Core
{
    /// <summary>
    /// Reloj de la partida, al estilo de HOI4: el tiempo avanza por horas con 5 velocidades y se puede
    /// pausar. También calcula la posición del sol para el ciclo de día y noche del mapa.
    /// </summary>
    public sealed class GameClock : MonoBehaviour
    {
        public const int MinSpeed = 1;
        public const int MaxSpeed = 5;

        /// <summary>Horas de juego por segundo real para cada velocidad (índice 1..5).</summary>
        public static readonly float[] HoursPerSecond = { 0f, 1f, 3f, 8f, 24f, 72f };

        private static readonly string[] Months =
        {
            "enero", "febrero", "marzo", "abril", "mayo", "junio",
            "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre",
        };

        public DateTime Now { get; private set; } = new DateTime(1936, 1, 1, 12, 0, 0);
        public int Speed { get; private set; } = 2;
        /// <summary>Pausa del tiempo (barra espaciadora), independiente del menú de pausa.</summary>
        public bool Paused { get; private set; } = true;
        /// <summary>Lo controla el GameManager: el tiempo solo corre durante la partida.</summary>
        public bool TimeAllowed { get; set; }
        public bool Running => TimeAllowed && !Paused;

        /// <summary>Se dispara cada hora de juego.</summary>
        public event Action<DateTime> HourPassed;
        /// <summary>Se dispara cada día de juego (útil para la economía en el futuro).</summary>
        public event Action<DateTime> DayPassed;
        /// <summary>Cambio de velocidad o de pausa.</summary>
        public event Action StateChanged;

        private void Update()
        {
            if (!Running) return;
            var before = Now;
            Now = Now.AddHours(HoursPerSecond[Speed] * Time.unscaledDeltaTime);
            if (Now.Hour != before.Hour || (Now - before).TotalHours >= 1) HourPassed?.Invoke(Now);
            if (Now.Date != before.Date) DayPassed?.Invoke(Now);
        }

        public void SetDate(DateTime date)
        {
            Now = date;
            HourPassed?.Invoke(Now);
        }

        public void SetPaused(bool paused)
        {
            if (Paused == paused) return;
            Paused = paused;
            StateChanged?.Invoke();
        }

        public void TogglePause() => SetPaused(!Paused);

        public void SetSpeed(int speed)
        {
            speed = Mathf.Clamp(speed, MinSpeed, MaxSpeed);
            if (speed == Speed) return;
            Speed = speed;
            StateChanged?.Invoke();
        }

        public void SpeedUp() => SetSpeed(Speed + 1);
        public void SlowDown() => SetSpeed(Speed - 1);

        // ------------------------------------------------------------------ Sol

        /// <summary>Longitud (grados) donde el sol está en el cenit: 0° a las 12:00 UTC.</summary>
        public float SubsolarLongitude
        {
            get
            {
                double hours = Now.TimeOfDay.TotalHours;
                float lon = (float)(-(hours - 12.0) * 15.0);
                return Mathf.Repeat(lon + 180f, 360f) - 180f;
            }
        }

        /// <summary>Declinación solar (grados): latitud del sol según la estación.</summary>
        public float SolarDeclination =>
            23.44f * Mathf.Sin(2f * Mathf.PI * (Now.DayOfYear - 81) / 365f);

        // ------------------------------------------------------------------ Texto

        public string FormatDate() => $"{Now:HH}:00, {Now.Day} de {Months[Now.Month - 1]} de {Now.Year}";

        /// <summary>"16 de enero de 1936".</summary>
        public string FormatDay(DateTime date) => $"{date.Day} de {Months[date.Month - 1]} de {date.Year}";
    }
}
