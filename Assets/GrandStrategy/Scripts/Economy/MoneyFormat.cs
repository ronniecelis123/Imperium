using System;

namespace GrandStrategy.Economy
{
    /// <summary>
    /// Formato único de las cantidades de dinero (en millones de $) para toda la interfaz:
    /// "$152.2 M", "+$36.1 M", "−$0.45 M", "$1.2 mil M". Por debajo de $1 M se muestran dos decimales para que los
    /// mantenimientos pequeños ($0.22 M, $0.65 M) no se confundan. Solo afecta a cómo se ve, no a los cálculos.
    /// </summary>
    public static class MoneyFormat
    {
        public static string Millions(double millions, bool sign = false)
        {
            double abs = Math.Abs(millions);
            string body = abs >= 1000 ? (abs / 1000).ToString("0.0") + " mil M"
                : abs >= 0.995 || abs < 0.005 ? abs.ToString("0.0") + " M"
                : abs.ToString("0.00") + " M";
            string prefix = millions <= -0.005 ? "−" : sign && millions >= 0.005 ? "+" : "";
            return prefix + "$" + body;
        }

        /// <summary>"$0.22 M / 15 días".</summary>
        public static string PerPeriod(double millions, bool sign = false) =>
            Millions(millions, sign) + " / " + EconomyRules.PeriodDays + " días";
    }
}
