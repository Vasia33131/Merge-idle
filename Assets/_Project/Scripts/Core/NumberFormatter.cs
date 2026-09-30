using System;
using System.Globalization;

namespace CatMerge.Core
{
    /// <summary>
    /// Idle-game style short numbers: 950, 1.2K, 12.3K, 123K, 3.4M, 5.6B, 7.8T, then 1aa, 1ab ... 1zz, then 1e2046.
    /// Always truncates (never rounds up), so the player never sees more coins than they actually have.
    /// </summary>
    public static class NumberFormatter
    {
        private static readonly string[] NamedSuffixes = { "", "K", "M", "B", "T" };
        private const int Letters = 26;
        private const double Epsilon = 1e-6; // 5.6e9 / 1e9 = 5.59999..., which must still show as 5.6

        public static string Format(double value)
        {
            if (double.IsNaN(value)) return "NaN";
            if (double.IsInfinity(value)) return value > 0 ? "∞" : "-∞";
            if (value < 0) return "-" + Format(-value);
            if (value < 1000) return Mantissa(value);

            int group = (int)Math.Floor(Math.Log10(value) / 3);
            return Compose(value / Math.Pow(1000, group), group);
        }

        /// <summary>Formats 10^<paramref name="log10"/>; works for numbers far beyond double range.</summary>
        public static string FromLog10(double log10)
        {
            if (double.IsNaN(log10)) return "NaN";
            if (double.IsNegativeInfinity(log10)) return "0";
            if (log10 < 300) return Format(Math.Pow(10, log10));

            int group = (int)Math.Floor(log10 / 3);
            return Compose(Math.Pow(10, log10 - group * 3.0), group);
        }

        /// <summary>"", K, M, B, T, aa, ab, ..., az, ba, ..., zz; null once the letters run out.</summary>
        public static string Suffix(int group)
        {
            if (group < 0) throw new ArgumentOutOfRangeException(nameof(group));
            if (group < NamedSuffixes.Length) return NamedSuffixes[group];

            int index = group - NamedSuffixes.Length;
            if (index >= Letters * Letters) return null;
            return new string(new[] { (char)('a' + index / Letters), (char)('a' + index % Letters) });
        }

        private static string Compose(double mantissa, int group)
        {
            // Log10/Pow are off by an ulp around exact powers of 1000: 1e90 may arrive as 0.99999...az or 999.99...ay.
            if (mantissa < 1 - Epsilon) { group--; mantissa *= 1000; }
            else if (mantissa < 1) mantissa = 1;
            else if (mantissa > 1000 - Epsilon) { group++; mantissa = Math.Max(1, mantissa / 1000); }

            string suffix = Suffix(group);
            if (suffix != null) return Mantissa(mantissa) + suffix;

            int digits = (int)Math.Floor(Math.Log10(mantissa) + Epsilon);
            double m = mantissa / Math.Pow(10, digits);
            return Truncate(m, 1).ToString("0.#", CultureInfo.InvariantCulture) + "e" + (group * 3L + digits);
        }

        /// <summary>1 decimal below 100 (1.2, 12.3), whole number above (123).</summary>
        private static string Mantissa(double value)
        {
            return value < 100
                ? Truncate(value, 1).ToString("0.#", CultureInfo.InvariantCulture)
                : Truncate(value, 0).ToString("0", CultureInfo.InvariantCulture);
        }

        private static double Truncate(double value, int decimals)
        {
            double scale = Math.Pow(10, decimals);
            return Math.Floor(value * scale + Epsilon) / scale;
        }
    }
}
