namespace CatMerge.Localization
{
    /// <summary>Short alias for <see cref="LocalizationService"/> used by UI code, plus shared formatting helpers.</summary>
    public static class Loc
    {
        public static Language Current => LocalizationService.Current;

        public static string Get(string key) => LocalizationService.Get(key);

        public static string Format(string key, params object[] args) => LocalizationService.Format(key, args);

        /// <summary>"1 h 5 min" / "5 min" / "30 sec".</summary>
        public static string FormatDuration(double seconds)
        {
            long total = (long)System.Math.Max(0, seconds);
            long hours = total / 3600;
            long minutes = total % 3600 / 60;
            if (hours > 0) return minutes > 0 ? Format("time_hm", hours, minutes) : Format("time_h", hours);
            if (minutes > 0) return Format("time_m", minutes);
            return Format("time_s", total);
        }
    }
}
