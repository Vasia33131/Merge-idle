using System;

namespace CatMerge.Core
{
    /// <summary>Income earned while the game was closed or in the background.</summary>
    public static class OfflineIncome
    {
        /// <summary>
        /// Seconds between the last time the game saw the clock and now. 0 if the clock was turned back
        /// (now earlier than <paramref name="lastSeenUnix"/>) or there is no previous time.
        /// </summary>
        public static double ElapsedSeconds(long lastSeenUnix, long nowUnix)
        {
            if (lastSeenUnix <= 0 || nowUnix <= lastSeenUnix) return 0;
            return nowUnix - lastSeenUnix;
        }

        /// <summary>The time to store as "last seen": never earlier than what was already stored.</summary>
        public static long NextLastSeen(long lastSeenUnix, long nowUnix) => Math.Max(lastSeenUnix, nowUnix);

        /// <summary>Seconds that count: <paramref name="seconds"/> capped at <paramref name="maxSeconds"/>.</summary>
        public static double CountedSeconds(double seconds, double maxSeconds)
        {
            if (!(seconds > 0) || !(maxSeconds > 0)) return 0;
            return Math.Min(seconds, maxSeconds);
        }

        /// <summary>Coins for <paramref name="seconds"/> away: incomePerSec × rate × min(seconds, maxSeconds).</summary>
        public static double Calculate(double incomePerSec, double seconds, double rate, double maxSeconds)
        {
            if (!(incomePerSec > 0) || !(rate > 0)) return 0;
            double amount = incomePerSec * rate * CountedSeconds(seconds, maxSeconds);
            return double.IsInfinity(amount) ? double.MaxValue : amount;
        }
    }
}
