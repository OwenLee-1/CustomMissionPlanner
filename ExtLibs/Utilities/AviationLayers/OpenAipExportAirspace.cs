using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http;
using GMap.NET;
using log4net;
using Newtonsoft.Json.Linq;

namespace MissionPlanner.Utilities.AviationLayers
{
    /// <summary>
    /// OpenAIP retired map.openaip.org WFS; loads country airspace GeoJSON exports from storage.openaip.net.
    /// </summary>
    public static class OpenAipExportAirspace
    {
        static readonly ILog Log = LogManager.GetLogger(typeof(OpenAipExportAirspace));

        const string ExportBase = "https://storage.openaip.net/openaip-system-exports";

        static readonly object CacheLock = new object();
        static readonly Dictionary<string, List<MapPolygonFeature>> CountryCache =
            new Dictionary<string, List<MapPolygonFeature>>(StringComparer.OrdinalIgnoreCase);

        static readonly Dictionary<string, DateTime> CountryLoadedUtc =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        public static TimeSpan CacheTtl { get; set; } = TimeSpan.FromHours(12);

        public static string CacheDirectory { get; set; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Mission Planner", "openaip-cache");

        public static string CountryCodeForView(RectLatLng view)
        {
            if (view.IsEmpty)
                return "us";

            var lat = view.Lat - view.HeightLat * 0.5;
            var lng = view.Lng + view.WidthLng * 0.5;

            if (lat >= 24 && lat <= 50 && lng >= -125 && lng <= -66)
                return "us";
            if (lat >= 18 && lat <= 72 && lng >= -180 && lng <= -60)
                return "us";
            if (lat >= 35 && lat <= 72 && lng >= -10 && lng <= 40)
            {
                if (lng >= 5 && lng <= 16 && lat >= 47 && lat <= 55)
                    return "de";
                if (lng >= -6 && lng <= 2 && lat >= 49 && lat <= 61)
                    return "gb";
                if (lng >= -5 && lng <= 10 && lat >= 41 && lat <= 52)
                    return "fr";
            }

            return "us";
        }

        public static async Task<List<MapPolygonFeature>> GetFeaturesInBoundsAsync(
            RectLatLng fetchBounds,
            CancellationToken cancel,
            Func<JObject, bool> propertyFilter = null)
        {
            var country = CountryCodeForView(fetchBounds);
            var all = await LoadCountryAsync(country, cancel).ConfigureAwait(false);
            cancel.ThrowIfCancellationRequested();

            var result = new List<MapPolygonFeature>();
            foreach (var feat in all)
            {
                if (feat?.Ring == null || feat.Ring.Count < 3)
                    continue;

                if (!Intersects(fetchBounds, feat.Ring))
                    continue;

                result.Add(feat);
                if (result.Count >= 750)
                    break;
            }

            return result;
        }

        public static async Task<List<MapPolygonFeature>> LoadCountryAsync(string countryCode, CancellationToken cancel)
        {
            countryCode = (countryCode ?? "us").Trim().ToLowerInvariant();
            lock (CacheLock)
            {
                if (CountryCache.TryGetValue(countryCode, out var cached) &&
                    CountryLoadedUtc.TryGetValue(countryCode, out var loaded) &&
                    DateTime.UtcNow - loaded < CacheTtl)
                {
                    return cached;
                }
            }

            try
            {
                Directory.CreateDirectory(CacheDirectory);
                var localPath = Path.Combine(CacheDirectory, countryCode + "_asp.geojson");
                var url = string.Format(CultureInfo.InvariantCulture, "{0}/{1}_asp.geojson", ExportBase, countryCode);

                if (!File.Exists(localPath) || new FileInfo(localPath).Length < 1024)
                {
                    Log.Info("Downloading OpenAIP airspace export: " + url);
                    await url.DownloadFileAsync(localPath, cancellationToken: cancel).ConfigureAwait(false);
                }

                cancel.ThrowIfCancellationRequested();
                var json = File.ReadAllText(localPath);
                var features = ParseExportGeoJson(json, propertyFilter: null);

                lock (CacheLock)
                {
                    CountryCache[countryCode] = features;
                    CountryLoadedUtc[countryCode] = DateTime.UtcNow;
                }

                return features;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Log.Warn("OpenAIP export load failed for " + countryCode, ex);
                return new List<MapPolygonFeature>();
            }
        }

        public static List<MapPolygonFeature> ParseExportGeoJson(string json, Func<JObject, bool> propertyFilter)
        {
            var result = new List<MapPolygonFeature>();
            if (string.IsNullOrWhiteSpace(json))
                return result;

            var root = JObject.Parse(json);
            var features = root["features"] as JArray;
            if (features == null)
                return result;

            foreach (var feature in features)
            {
                var props = feature["properties"] as JObject ?? new JObject();
                if (propertyFilter != null && !propertyFilter(props))
                    continue;

                var id = feature["id"]?.ToString() ?? props["id"]?.ToString() ?? props["name"]?.ToString() ?? "asp";
                var title = props["name"]?.ToString() ?? props["type"]?.ToString() ?? id;
                var ic = props["class"]?.ToString() ?? props["icaoClass"]?.ToString() ?? "";
                var tooltip = string.IsNullOrEmpty(ic) ? title : ic + ": " + title;

                var geom = feature["geometry"];
                if (geom == null)
                    continue;

                foreach (var ring in AviationGeoJson.ExtractRings(geom))
                {
                    if (ring.Count >= 3)
                        result.Add(new MapPolygonFeature { Id = id, Title = title, ToolTip = tooltip, Ring = ring });
                }
            }

            return result;
        }

        static bool Intersects(RectLatLng view, List<PointLatLng> ring)
        {
            var minLat = ring.Min(p => p.Lat);
            var maxLat = ring.Max(p => p.Lat);
            var minLng = ring.Min(p => p.Lng);
            var maxLng = ring.Max(p => p.Lng);

            return maxLat >= view.Bottom && minLat <= view.Top &&
                   maxLng >= view.Left && minLng <= view.Right;
        }

        public static bool IsLegacyDeadWfsUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            return url.IndexOf("openaip.org", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   url.IndexOf("map.openaip", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
