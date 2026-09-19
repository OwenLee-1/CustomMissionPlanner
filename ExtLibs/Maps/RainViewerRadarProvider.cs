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
    public sealed class RainViewerRadarProvider : GMapProvider
    {
        public static readonly RainViewerRadarProvider Instance;

        /// <summary>RainViewer only publishes radar rasters up to this zoom.</summary>
        public const int NativeRadarMaxZoom = 12;

        /// <summary>Allow GMap to request overlay tiles up to flight-map zoom (overzoom with crop).</summary>
        public const int RadarOverlayMaxZoom = 18;

        private static readonly object RadarPathLock = new object();
        private static readonly object TileCacheLock = new object();
        private static readonly Dictionary<string, byte[]> TileCache = new Dictionary<string, byte[]>();
        private const int MaxCachedTiles = 96;

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
            MaxZoom = RadarOverlayMaxZoom;
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

            if (zoom <= NativeRadarMaxZoom)
            {
                var url = BuildTileUrl(path, 256, zoom, pos.X, pos.Y);
                return GetTileImageUsingHttp(url);
            }

            var shift = zoom - NativeRadarMaxZoom;
            if (shift < 1)
                shift = 1;

            var parentX = pos.X >> shift;
            var parentY = pos.Y >> shift;
            var ix = 1L << shift;
            var xoff = pos.X - (parentX << shift);
            var yoff = pos.Y - (parentY << shift);

            // 512px parent tiles give 2× sharper crops when overzooming to street level.
            var bytes = FetchTileBytes(path, 512, NativeRadarMaxZoom, parentX, parentY);
            if (bytes == null || bytes.Length == 0)
                return null;

            var img = GMapProvider.TileImageProxy?.FromArray(bytes);
            if (img == null)
                return null;

            img.IsParent = true;
            img.Ix = ix;
            img.Xoff = xoff;
            img.Yoff = yoff;
            return img;
        }

        static string BuildTileUrl(string path, int size, int z, long x, long y)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0}{1}/{2}/{3}/{4}/{5}/2/1_1.png",
                _radarHost, path, size, z, x, y);
        }

        static byte[] FetchTileBytes(string path, int size, int z, long x, long y)
        {
            var key = string.Format(CultureInfo.InvariantCulture, "{0}:{1}:{2}:{3}:{4}", path, size, z, x, y);
            lock (TileCacheLock)
            {
                if (TileCache.TryGetValue(key, out var cached))
                    return cached;
            }

            var url = BuildTileUrl(path, size, z, x, y);
            PureImage downloaded;
            try
            {
                downloaded = Instance.GetTileImageUsingHttp(url);
            }
            catch
            {
                return null;
            }

            if (downloaded?.Data == null || downloaded.Data.Length == 0)
            {
                downloaded?.Dispose();
                return null;
            }

            var bytes = downloaded.Data.ToArray();
            downloaded.Dispose();

            lock (TileCacheLock)
            {
                if (TileCache.Count >= MaxCachedTiles)
                    TileCache.Clear();
                TileCache[key] = bytes;
            }

            return bytes;
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
                    {
                        var newPath = past[past.Count - 1]["path"]?.ToString();
                        if (!string.IsNullOrEmpty(newPath) && newPath != _radarPath)
                        {
                            _radarPath = newPath;
                            lock (TileCacheLock)
                                TileCache.Clear();
                        }
                    }

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
