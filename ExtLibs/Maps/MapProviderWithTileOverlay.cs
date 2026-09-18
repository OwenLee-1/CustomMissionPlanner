using System;
using System.Collections.Generic;
using System.Reflection;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.Projections;

namespace MissionPlanner.Maps
{
    /// <summary>
    /// Stacks optional tile overlays (e.g. weather radar) on top of any base map provider.
    /// </summary>
    public class MapProviderWithTileOverlay : GMapProvider
    {
        public static readonly MapProviderWithTileOverlay Instance;

        static MapProviderWithTileOverlay()
        {
            Instance = new MapProviderWithTileOverlay();

            var mytype = typeof(GMapProviders);
            var field = mytype.GetField("DbHash", BindingFlags.Static | BindingFlags.NonPublic);
            var list = (Dictionary<int, GMapProvider>)field.GetValue(Instance);
            list.Add(Instance.DbId, Instance);
        }

        private MapProviderWithTileOverlay()
        {
            MaxZoom = 24;
        }

        readonly Guid id = new Guid("B7D2A4E8-1C3F-4A6B-9D0E-2F8C5B714639");

        public override Guid Id => id;

        public override string Name => BaseProvider?.Name ?? "Map with overlays";

        public GMapProvider BaseProvider { get; set; } = GMapProviders.EmptyProvider;

        public bool WeatherRadarEnabled { get; set; }

        public override PureProjection Projection =>
            BaseProvider?.Projection ?? MercatorProjection.Instance;

        public override GMapProvider[] Overlays
        {
            get
            {
                var list = new List<GMapProvider>();
                if (BaseProvider != null)
                {
                    var baseOverlays = BaseProvider.Overlays;
                    if (baseOverlays != null && baseOverlays.Length > 0)
                        list.AddRange(baseOverlays);
                    else
                        list.Add(BaseProvider);
                }

                if (WeatherRadarEnabled)
                    list.Add(RainViewerRadarProvider.Instance);

                return list.ToArray();
            }
        }

        public override PureImage GetTileImage(GPoint pos, int zoom)
        {
            return BaseProvider?.GetTileImage(pos, zoom);
        }

        /// <summary>
        /// Returns the underlying map provider, unwrapping this overlay if needed.
        /// </summary>
        public static GMapProvider Unwrap(GMapProvider provider)
        {
            if (provider is MapProviderWithTileOverlay overlay)
                return overlay.BaseProvider ?? GMapProviders.EmptyProvider;
            return provider;
        }

        /// <summary>
        /// Apply the current weather-radar setting to a map provider selection.
        /// </summary>
        public static GMapProvider Resolve(GMapProvider selected, bool weatherRadar)
        {
            var baseProvider = Unwrap(selected);
            if (!weatherRadar)
                return baseProvider;

            Instance.BaseProvider = baseProvider;
            Instance.WeatherRadarEnabled = true;
            if (weatherRadar)
                RainViewerRadarProvider.EnsureRadarPath();
            return Instance;
        }

    }
}
