using System;

namespace MissionPlanner.Utilities.AviationLayers
{
    /// <summary>
    /// Debounce and cache TTLs for aviation map layers. AWC asks clients not to hammer APIs;
    /// these defaults balance freshness vs. rate limits.
    /// </summary>
    public static class AviationRefreshPolicy
    {
        /// <summary>Delay after pan before fetching (ms).</summary>
        public static int PanDebounceMs { get; set; } = 450;

        /// <summary>Delay after zoom before fetching (ms).</summary>
        public static int ZoomDebounceMs { get; set; } = 700;

        /// <summary>Fetch radius around vehicle / map center (km). Default 75; clamp 25–150.</summary>
        public static double FetchRadiusKm { get; set; } = AviationBounds.DefaultFetchRadiusKm;

        public static TimeSpan TfrCache { get; set; } = TimeSpan.FromSeconds(90);
        public static TimeSpan NotamCache { get; set; } = TimeSpan.FromSeconds(90);
        public static TimeSpan AwcWeatherCache { get; set; } = TimeSpan.FromSeconds(45);
        public static TimeSpan SigmetCache { get; set; } = TimeSpan.FromSeconds(60);
        public static TimeSpan UasCache { get; set; } = TimeSpan.FromMinutes(3);
        public static TimeSpan SpecialUseCache { get; set; } = TimeSpan.FromMinutes(5);
    }
}
