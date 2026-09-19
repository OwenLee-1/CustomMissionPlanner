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
            MaxZoom = 17;
            MinZoom = 1;
        }

        /// <summary>Tile pyramid limit for the base map (not the radar overlay).</summary>
        public static int CapBaseProviderMaxZoom(GMapProvider baseProvider)
        {
            if (baseProvider == null)
                return 17;

            int max;
            if (baseProvider.MaxZoom.HasValue)
                max = baseProvider.MaxZoom.Value;
            else
            {
                var name = baseProvider.Name ?? "";
                if (name.IndexOf("Satellite", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Google", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Hybrid", StringComparison.OrdinalIgnoreCase) >= 0)
                    max = 17;
                else if (name.IndexOf("Bing", StringComparison.OrdinalIgnoreCase) >= 0)
                    max = 17;
                else
                    max = 16;
            }

            return Math.Min(17, Math.Max(1, max));
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
            Instance.MaxZoom = CapBaseProviderMaxZoom(baseProvider);
            Instance.MinZoom = baseProvider.MinZoom;
            RainViewerRadarProvider.EnsureRadarPath();
            return Instance;
        }

    }
}
