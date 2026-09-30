namespace CatMerge.Platform
{
    /// <summary>Picks the <see cref="IPlatformService"/> for the current build.</summary>
    public static class PlatformProvider
    {
        private static IPlatformService _current;

        public static IPlatformService Current => _current ??= Create();

        private static IPlatformService Create()
        {
            // Yandex Games / CrazyGames implementations go here, chosen by scripting define.
            return new EditorPlatform();
        }
    }
}
