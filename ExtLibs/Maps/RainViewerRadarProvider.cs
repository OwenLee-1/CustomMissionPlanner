using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using Flurl.Http;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.Projections;
using Newtonsoft.Json.Linq;

namespace MissionPlanner.Maps
{
    /// <summary>
    /// Semi-transparent weather radar tiles from RainViewer (free, no API key).
    /// </summary>
    public class RainViewerRadarProvider : GMapProvider
    {
        public static readonly RainViewerRadarProvider Instance;

        private static readonly object RadarPathLock = new object();
        private static string _radarHost = "https://tilecache.rainviewer.com";
        private static string _radarPath = "";
        private static DateTime _radarPathUpdated = DateTime.MinValue;

        static RainViewerRadarProvider()
        {
            Instance = new RainViewerRadarProvider();

            var mytype = typeof(GMapProviders);
            var field = mytype.GetField("DbHash", BindingFlags.Static | BindingFlags.NonPublic);
            var list = (Dictionary<int, GMapProvider>)field.GetValue(Instance);
            list.Add(Instance.DbId, Instance);
        }

        private RainViewerRadarProvider()
        {
            MaxZoom = 12;
            MinZoom = 1;
        }

        readonly Guid id = new Guid("A3C8E1F2-6B4D-4E9A-8C2D-1F5E7B903456");

        public override Guid Id => id;

        readonly string name = "Weather Radar";

        public override string Name => name;

        GMapProvider[] overlays;

        public override GMapProvider[] Overlays
        {
            get
            {
                if (overlays == null)
                    overlays = new GMapProvider[] { this };
                return overlays;
            }
        }

        public override PureProjection Projection => MercatorProjection.Instance;

        public override PureImage GetTileImage(GPoint pos, int zoom)
        {
            EnsureRadarPath();
            var path = _radarPath;
            if (string.IsNullOrEmpty(path))
                return null;

            var tileZoom = Math.Min(zoom, MaxZoom);
            var shift = zoom - tileZoom;
            var tileX = pos.X >> shift;
            var tileY = pos.Y >> shift;

            var url = string.Format(CultureInfo.InvariantCulture,
                "{0}{1}/256/{2}/{3}/{4}/2/1_1.png",
                _radarHost, path, tileZoom, tileX, tileY);

            return GetTileImageUsingHttp(url);
        }

        /// <summary>
        /// Refresh radar frame metadata (at most once per minute).
        /// </summary>
        public static void EnsureRadarPath()
        {
            if ((DateTime.UtcNow - _radarPathUpdated).TotalSeconds < 60 && !string.IsNullOrEmpty(_radarPath))
                return;

            lock (RadarPathLock)
            {
                if ((DateTime.UtcNow - _radarPathUpdated).TotalSeconds < 60 && !string.IsNullOrEmpty(_radarPath))
                    return;

                try
                {
                    var json = "https://api.rainviewer.com/public/weather-maps.json"
                        .GetStringAsync()
                        .GetAwaiter()
                        .GetResult();
                    var root = JObject.Parse(json);
                    _radarHost = root["host"]?.ToString() ?? _radarHost;
                    var past = root["radar"]?["past"] as JArray;
                    if (past != null && past.Count > 0)
                        _radarPath = past[past.Count - 1]["path"]?.ToString() ?? _radarPath;
                    _radarPathUpdated = DateTime.UtcNow;
                }
                catch
                {
                    _radarPathUpdated = DateTime.UtcNow;
                }
            }
        }
    }
}
