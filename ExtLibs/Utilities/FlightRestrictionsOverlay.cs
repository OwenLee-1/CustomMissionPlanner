using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http;
using GMap.NET;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using log4net;
using MissionPlanner.Utilities.AviationLayers;
using Newtonsoft.Json.Linq;

namespace MissionPlanner.Utilities
{
    /// <summary>
    /// Loads TFR, airspace, UAS facility maps, NOTAM markers, and special-use airspace for the visible map area.
    /// </summary>
    public static class FlightRestrictionsOverlay
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FlightRestrictionsOverlay));

        private const string TfrWfsUrl = "https://tfr.faa.gov/geoserver/TFR/ows";

        private const string UasFacilityMapUrl =
            "https://services6.arcgis.com/ssFJjBXIUyZDrSYZ/arcgis/rest/services/FAA_UAS_FacilityMap_Data/FeatureServer/0";

        private const string DodUasRestrictionUrl =
            "https://services6.arcgis.com/ssFJjBXIUyZDrSYZ/arcgis/rest/services/DoD_Mar_13/FeatureServer/0";

        public static string AirspaceWfsUrl { get; set; } = "";

        public static string AirspaceLayerName { get; set; } = "openaip:airspaces";

        static FlightRestrictionsOverlay()
        {
            SyncAirspaceUrl();
        }

        static void SyncAirspaceUrl()
        {
            AviationFetch.AirspaceWfsUrl = AirspaceWfsUrl;
            AviationFetch.AirspaceLayerName = AirspaceLayerName;
        }

        private static readonly object RefreshLock = new object();
        private static DateTime _lastRefresh = DateTime.MinValue;
        private static RectLatLng _lastBounds = RectLatLng.Empty;
        private static CancellationTokenSource _refreshCts;

        public class RefreshOptions
        {
            public bool LoadTfr { get; set; }
            public bool LoadAirspace { get; set; }
            public bool LoadUasFacilityMap { get; set; }
            public bool LoadNotams { get; set; }
            public bool LoadSpecialUseAirspace { get; set; }
            public bool LoadBriefing { get; set; }
            public Action<Action> InvokeOnUi { get; set; }
            public Action<List<NotamBriefingItem>> BriefingUpdated { get; set; }
            public Action<RestrictionBriefingSummary> SummaryUpdated { get; set; }
            /// <summary>Called on UI thread after overlay geometry is updated (e.g. map Invalidate).</summary>
            public Action MapInvalidate { get; set; }
        }

        public class RestrictionBriefingSummary
        {
            public int TfrNotamCount { get; set; }
            public int UasGridCells { get; set; }
            public int SpecialUseAreas { get; set; }
        }

        public class OverlaySet
        {
            public GMapOverlay Tfr { get; set; }
            public GMapOverlay Airspace { get; set; }
            public GMapOverlay UasFacility { get; set; }
            public GMapOverlay NotamMarkers { get; set; }
            public GMapOverlay SpecialUse { get; set; }
        }

        public static void RequestRefresh(RectLatLng viewArea, OverlaySet overlays, RefreshOptions options)
        {
            if (viewArea.IsEmpty || options == null || overlays == null)
                return;

            lock (RefreshLock)
            {
                _lastRefresh = DateTime.UtcNow;
                var center = viewArea.IsEmpty ? PointLatLng.Empty : viewArea.LocationMiddle;
                var zone = AviationBounds.NormalizeForFetch(viewArea, center, 10,
                    AviationBounds.DefaultFetchRadiusKm);
                _lastBounds = AviationBounds.InflateFetchBounds(zone);

                _refreshCts?.Cancel();
                _refreshCts = new CancellationTokenSource();
            }

            var token = _refreshCts.Token;
            var bounds = _lastBounds;
            var invoke = options.InvokeOnUi ?? (a => a());

            var loadTfr = options.LoadTfr;
            var loadNotams = options.LoadNotams;
            var loadBriefing = options.LoadBriefing;
            var briefingUpdated = options.BriefingUpdated;
            var summaryUpdated = options.SummaryUpdated;

            Task.Run(async () =>
            {
                var summary = new RestrictionBriefingSummary();
                try
                {
                    var needTfrJson = loadTfr || loadNotams || loadBriefing;
                    string tfrJson = null;
                    List<(string Name, string ToolTip, List<PointLatLng> Ring)> tfrFeatures = null;

                    if (needTfrJson)
                    {
                        tfrJson = await FetchTfrGeoJsonAsync(bounds, token).ConfigureAwait(false);
                        token.ThrowIfCancellationRequested();
                        tfrFeatures = ParseGeoJsonFeatures(tfrJson);
                        summary.TfrNotamCount = tfrFeatures.Count;
                    }

                    if (loadTfr)
                        ApplyPolygons(overlays.Tfr, tfrFeatures, Color.FromArgb(75, Color.OrangeRed), Color.OrangeRed,
                            3f, "TFR", true, invoke);
                    else
                        ClearOverlay(overlays.Tfr, false, invoke);

                    if (loadNotams)
                        ApplyNotamMarkers(overlays.NotamMarkers, tfrFeatures, true, invoke);
                    else
                        ClearOverlay(overlays.NotamMarkers, false, invoke);

                    if (loadBriefing && tfrJson != null)
                    {
                        var briefing = ParseNotamBriefingItems(tfrJson);
                        invoke(() => briefingUpdated?.Invoke(briefing));
                    }
                    else if (loadBriefing)
                    {
                        invoke(() => briefingUpdated?.Invoke(new List<NotamBriefingItem>()));
                    }

                    if (options.LoadAirspace)
                        await LoadAirspaceAsync(overlays.Airspace, bounds, true, invoke, token).ConfigureAwait(false);
                    else
                        ClearOverlay(overlays.Airspace, false, invoke);

                    if (options.LoadUasFacilityMap)
                    {
                        var uasCount = await LoadUasFacilityMapAsync(overlays.UasFacility, bounds, true, invoke, token)
                            .ConfigureAwait(false);
                        summary.UasGridCells = uasCount;
                    }
                    else
                        ClearOverlay(overlays.UasFacility, false, invoke);

                    if (options.LoadSpecialUseAirspace)
                    {
                        var suaCount = await LoadSpecialUseAirspaceAsync(overlays.SpecialUse, bounds, true, invoke, token)
                            .ConfigureAwait(false);
                        summary.SpecialUseAreas = suaCount;
                    }
                    else
                        ClearOverlay(overlays.SpecialUse, false, invoke);

                    if (summaryUpdated != null)
                        invoke(() => summaryUpdated(summary));

                    if (options.MapInvalidate != null)
                        invoke(() => options.MapInvalidate());
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
            }, token);
        }

        private static async Task<string> FetchTfrGeoJsonAsync(RectLatLng bounds, CancellationToken cancel)
        {
            var url = BuildWfsUrl(TfrWfsUrl, "TFR:V_TFR_LOC", bounds);
            return await url.GetStringAsync(cancellationToken: cancel).ConfigureAwait(false);
        }

        public static async Task LoadTfrAsync(
            GMapOverlay overlay,
            RectLatLng bounds,
            bool visible,
            Action<Action> invokeOnUi,
            CancellationToken cancel = default)
        {
            var json = await FetchTfrGeoJsonAsync(bounds, cancel).ConfigureAwait(false);
            cancel.ThrowIfCancellationRequested();

            var features = ParseGeoJsonFeatures(json);
            ApplyPolygons(overlay, features, Color.FromArgb(75, Color.OrangeRed), Color.OrangeRed, 3f, "TFR", visible,
                invokeOnUi);
        }

        public static async Task LoadAirspaceAsync(
            GMapOverlay overlay,
            RectLatLng bounds,
            bool visible,
            Action<Action> invokeOnUi,
            CancellationToken cancel = default)
        {
            if (string.IsNullOrWhiteSpace(AirspaceWfsUrl))
                return;

            try
            {
                var url = BuildWfsUrl(AirspaceWfsUrl, AirspaceLayerName, bounds);
                var json = await url.GetStringAsync(cancellationToken: cancel).ConfigureAwait(false);
                cancel.ThrowIfCancellationRequested();

                var features = ParseGeoJsonFeatures(json);
                ApplyPolygons(overlay, features, Color.FromArgb(55, Color.DodgerBlue), Color.SteelBlue, 2.5f, "ASP",
                    visible, invokeOnUi);
            }
            catch (Exception ex)
            {
                log.Warn("Airspace overlay fetch failed (check AirspaceWfsUrl / network)", ex);
            }
        }

        public static async Task<int> LoadUasFacilityMapAsync(
            GMapOverlay overlay,
            RectLatLng bounds,
            bool visible,
            Action<Action> invokeOnUi,
            CancellationToken cancel = default)
        {
            try
            {
                var url = BuildArcGisQueryUrl(UasFacilityMapUrl, bounds, MaxArcGisFeaturesForBounds(bounds));
                var json = await url.GetStringAsync(cancellationToken: cancel).ConfigureAwait(false);
                cancel.ThrowIfCancellationRequested();

                var features = ParseArcGisGeoJson(json, (props, tooltip) =>
                {
                    var ceiling = props["CEILING"]?.Value<int?>() ?? 0;
                    var apt = props["APT1_NAME"]?.ToString() ?? props["APT1_ICAO"]?.ToString() ?? "";
                    var unit = props["UNIT"]?.ToString() ?? "ft";
                    return string.IsNullOrEmpty(apt)
                        ? $"UAS grid max {ceiling} {unit}"
                        : $"{apt}: max {ceiling} {unit} AGL";
                });

                invokeOnUi(() =>
                {
                    overlay.Polygons.Clear();
                    foreach (var feat in features)
                    {
                        var ceiling = 0;
                        if (feat.ToolTip.Contains("max "))
                        {
                            var parts = feat.ToolTip.Split(' ');
                            for (var i = 0; i < parts.Length; i++)
                            {
                                if (parts[i] == "max" && i + 1 < parts.Length && int.TryParse(parts[i + 1], out var c))
                                {
                                    ceiling = c;
                                    break;
                                }
                            }
                        }

                        var fill = ceiling <= 0
                            ? Color.FromArgb(70, Color.Red)
                            : Color.FromArgb(55, Color.Gold);
                        var stroke = ceiling <= 0 ? Color.Red : Color.Goldenrod;

                        var poly = new RestrictionGMapPolygon(feat.Ring, "UAS" + feat.Name)
                        {
                            Fill = new SolidBrush(fill),
                            Stroke = new Pen(stroke, 2f),
                            Tag = feat.ToolTip,
                            IsVisible = true
                        };
                        overlay.Polygons.Add(poly);
                    }

                    overlay.IsVisibile = visible;
                    SyncOverlayGeometry(overlay);
                });

                return features.Count;
            }
            catch (Exception ex)
            {
                log.Warn("UAS Facility Map fetch failed", ex);
            }

            return 0;
        }

        private static void ApplyNotamMarkers(
            GMapOverlay overlay,
            List<(string Name, string ToolTip, List<PointLatLng> Ring)> features,
            bool visible,
            Action<Action> invokeOnUi)
        {
            if (overlay == null)
                return;

            features = features ?? new List<(string, string, List<PointLatLng>)>();

            invokeOnUi(() =>
            {
                overlay.Markers.Clear();
                foreach (var feat in features)
                {
                    var center = Centroid(feat.Ring);
                    var marker = new GMarkerGoogle(center, GMarkerGoogleType.red_dot)
                    {
                        ToolTipText = feat.ToolTip,
                        ToolTipMode = MarkerTooltipMode.OnMouseOver,
                        Tag = feat.Name
                    };
                    overlay.Markers.Add(marker);
                }

                overlay.IsVisibile = visible;
                overlay.ForceUpdate();
            });
        }

        public static async Task LoadNotamMarkersAsync(
            GMapOverlay overlay,
            RectLatLng bounds,
            bool visible,
            Action<Action> invokeOnUi,
            CancellationToken cancel = default)
        {
            var json = await FetchTfrGeoJsonAsync(bounds, cancel).ConfigureAwait(false);
            cancel.ThrowIfCancellationRequested();

            var features = ParseGeoJsonFeatures(json);
            ApplyNotamMarkers(overlay, features, visible, invokeOnUi);
        }

        public static async Task<int> LoadSpecialUseAirspaceAsync(
            GMapOverlay overlay,
            RectLatLng bounds,
            bool visible,
            Action<Action> invokeOnUi,
            CancellationToken cancel = default)
        {
            var merged = new List<(string Name, string ToolTip, List<PointLatLng> Ring)>();

            if (!string.IsNullOrWhiteSpace(AirspaceWfsUrl))
            {
                try
                {
                    var url = BuildWfsUrl(AirspaceWfsUrl, AirspaceLayerName, bounds);
                    var json = await url.GetStringAsync(cancellationToken: cancel).ConfigureAwait(false);
                    cancel.ThrowIfCancellationRequested();
                    merged.AddRange(ParseGeoJsonFeaturesFiltered(json, IsSpecialUseProperties));
                }
                catch (Exception ex)
                {
                    log.Warn("Special-use airspace (OpenAIP) fetch failed", ex);
                }
            }

            try
            {
                var dodUrl = BuildArcGisQueryUrl(DodUasRestrictionUrl, bounds, 500);
                var dodJson = await dodUrl.GetStringAsync(cancellationToken: cancel).ConfigureAwait(false);
                cancel.ThrowIfCancellationRequested();
                merged.AddRange(ParseArcGisGeoJson(dodJson, (props, id) =>
                    "DoD UAS restriction: " + (props["NAME"]?.ToString() ?? props["Title"]?.ToString() ?? id)));
            }
            catch (Exception ex)
            {
                log.Warn("DoD UAS restriction fetch failed", ex);
            }

            ApplyPolygons(overlay, merged, Color.FromArgb(60, Color.MediumPurple), Color.Purple, 2.5f, "SUA", visible,
                invokeOnUi);
            return merged.Count;
        }

        public static List<NotamBriefingItem> ParseNotamBriefingItems(string json)
        {
            var items = new List<NotamBriefingItem>();
            if (string.IsNullOrWhiteSpace(json))
                return items;

            var root = JObject.Parse(json);
            var features = root["features"] as JArray;
            if (features == null)
                return items;

            foreach (var feature in features)
            {
                var props = feature["properties"] as JObject ?? new JObject();
                var notamKey = props["NOTAM_KEY"]?.ToString() ?? feature["id"]?.ToString() ?? "";
                var title = props["TITLE"]?.ToString() ?? "Temporary flight restriction";
                var type = props["LEGAL"]?.ToString() ?? "";
                var state = props["STATE"]?.ToString() ?? "";
                var modified = props["LAST_MODIFICATION_DATETIME"]?.ToString() ?? "";

                PointLatLng center = PointLatLng.Empty;
                var geom = feature["geometry"];
                if (geom != null)
                {
                    foreach (var ring in ExtractRings(geom))
                    {
                        center = Centroid(ring);
                        break;
                    }
                }

                items.Add(new NotamBriefingItem
                {
                    NotamKey = notamKey,
                    Type = type,
                    Title = title,
                    State = state,
                    LastModified = FormatFaaDateTime(modified),
                    Center = center,
                    DetailUrl = BuildTfrDetailUrl(notamKey)
                });
            }

            return items.OrderBy(i => i.DisplayId).ToList();
        }

        public static string BuildTfrDetailUrl(string notamKey)
        {
            if (string.IsNullOrWhiteSpace(notamKey))
                return null;

            var core = notamKey.Split('-')[0].Trim();
            var slash = core.IndexOf('/');
            if (slash <= 0)
                return "https://tfr.faa.gov/";

            return string.Format(CultureInfo.InvariantCulture,
                "https://tfr.faa.gov/tfr3/?page=detail_{0}_{1}.html",
                core.Substring(0, slash), core.Substring(slash + 1));
        }

        private static string FormatFaaDateTime(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw) || raw.Length < 8)
                return raw;

            if (DateTime.TryParseExact(raw.Substring(0, 8), "yyyyMMdd", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var dt))
                return dt.ToString("yyyy-MM-dd");

            return raw;
        }

        private static bool IsSpecialUseProperties(JObject props)
        {
            if (props == null)
                return false;

            var ic = (props["class"] ?? props["icaoClass"] ?? props["ICAOClass"])?.ToString()?.Trim()
                .ToUpperInvariant() ?? "";
            if (ic.Length == 1 && "RPQAW".Contains(ic))
                return true;

            var type = props["type"]?.ToString()?.ToUpperInvariant() ?? "";
            var name = props["name"]?.ToString()?.ToUpperInvariant() ?? "";
            var text = type + " " + name;
            return text.Contains("RESTRICT") || text.Contains("PROHIB") || text.Contains("MOA") ||
                   text.Contains("MILITARY") || text.Contains("DANGER") || text.Contains("WARNING");
        }

        private static List<(string Name, string ToolTip, List<PointLatLng> Ring)> ParseGeoJsonFeaturesFiltered(
            string json,
            Func<JObject, bool> propertyFilter)
        {
            var result = new List<(string, string, List<PointLatLng>)>();
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

                var id = feature["id"]?.ToString() ?? props["NOTAM_KEY"]?.ToString() ?? "restriction";
                var title = props["TITLE"]?.ToString() ?? props["name"]?.ToString() ?? id;
                var legal = props["LEGAL"]?.ToString() ?? props["class"]?.ToString() ?? props["icaoClass"]?.ToString() ?? "";
                var tooltip = string.IsNullOrEmpty(legal) ? title : legal + ": " + title;

                var geom = feature["geometry"];
                if (geom == null)
                    continue;

                foreach (var ring in ExtractRings(geom))
                {
                    if (ring.Count >= 3)
                        result.Add((id, tooltip, ring));
                }
            }

            return result;
        }

        private static int MaxFeaturesForBounds(RectLatLng bounds)
        {
            var area = Math.Abs(bounds.WidthLng * bounds.HeightLat);
            if (area <= 0.0001)
                return 3500;
            if (area <= 0.01)
                return 2500;
            if (area <= 0.25)
                return 1500;
            if (area <= 2.0)
                return 1000;
            return 600;
        }

        private static int MaxArcGisFeaturesForBounds(RectLatLng bounds)
        {
            var area = Math.Abs(bounds.WidthLng * bounds.HeightLat);
            if (area <= 0.0005)
                return 5000;
            if (area <= 0.05)
                return 3500;
            return 2000;
        }

        private static void SyncOverlayGeometry(GMapOverlay overlay)
        {
            if (overlay?.Control == null)
            {
                overlay?.ForceUpdate();
                return;
            }

            foreach (GMapPolygon poly in overlay.Polygons)
            {
                poly.IsVisible = true;
                overlay.Control.UpdatePolygonLocalPosition(poly);
            }

            foreach (GMapMarker marker in overlay.Markers)
                overlay.Control.UpdateMarkerLocalPosition(marker);

            overlay.ForceUpdate();
        }

        private static string BuildWfsUrl(string serviceUrl, string typeName, RectLatLng bounds)
        {
            var bbox = string.Format(CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},EPSG:4326",
                bounds.Left, bounds.Bottom, bounds.Right, bounds.Top);

            var maxFeatures = MaxFeaturesForBounds(bounds);

            return string.Format(CultureInfo.InvariantCulture,
                "{0}?service=WFS&version=1.0.0&request=GetFeature&typeName={1}&outputFormat=application/json&bbox={2}&maxFeatures={3}",
                serviceUrl.TrimEnd('/'), Uri.EscapeDataString(typeName), Uri.EscapeDataString(bbox), maxFeatures);
        }

        private static string BuildArcGisQueryUrl(string layerUrl, RectLatLng bounds, int maxFeatures)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0}/query?where=1%3D1&geometry={1},{2},{3},{4}&geometryType=esriGeometryEnvelope&inSR=4326&spatialRel=esriSpatialRelIntersects&outFields=*&returnGeometry=true&f=geojson&resultRecordCount={5}",
                layerUrl.TrimEnd('/'),
                bounds.Left, bounds.Bottom, bounds.Right, bounds.Top,
                maxFeatures);
        }

        private static List<(string Name, string ToolTip, List<PointLatLng> Ring)> ParseGeoJsonFeatures(string json)
        {
            var result = new List<(string, string, List<PointLatLng>)>();
            if (string.IsNullOrWhiteSpace(json))
                return result;

            var root = JObject.Parse(json);
            var features = root["features"] as JArray;
            if (features == null)
                return result;

            foreach (var feature in features)
            {
                var props = feature["properties"] as JObject ?? new JObject();
                var id = feature["id"]?.ToString() ?? props["NOTAM_KEY"]?.ToString() ?? "restriction";
                var title = props["TITLE"]?.ToString() ?? props["name"]?.ToString() ?? id;
                var legal = props["LEGAL"]?.ToString() ?? props["class"]?.ToString() ?? props["icaoClass"]?.ToString() ?? "";
                var tooltip = string.IsNullOrEmpty(legal) ? title : legal + ": " + title;

                var geom = feature["geometry"];
                if (geom == null)
                    continue;

                foreach (var ring in ExtractRings(geom))
                {
                    if (ring.Count >= 3)
                        result.Add((id, tooltip, ring));
                }
            }

            return result;
        }

        private static List<(string Name, string ToolTip, List<PointLatLng> Ring)> ParseArcGisGeoJson(
            string json,
            Func<JObject, string, string> tooltipBuilder)
        {
            var result = new List<(string, string, List<PointLatLng>)>();
            if (string.IsNullOrWhiteSpace(json))
                return result;

            var root = JObject.Parse(json);
            var features = root["features"] as JArray;
            if (features == null)
                return result;

            foreach (var feature in features)
            {
                var props = feature["properties"] as JObject ?? new JObject();
                var id = feature["id"]?.ToString() ?? props["OBJECTID"]?.ToString() ?? "feature";
                var tooltip = tooltipBuilder(props, id);
                var geom = feature["geometry"];
                if (geom == null)
                    continue;

                foreach (var ring in ExtractRings(geom))
                {
                    if (ring.Count >= 3)
                        result.Add((id, tooltip, ring));
                }
            }

            return result;
        }

        private static IEnumerable<List<PointLatLng>> ExtractRings(JToken geometry)
        {
            var type = geometry["type"]?.ToString();
            var coords = geometry["coordinates"];
            if (type == null || coords == null)
                yield break;

            switch (type)
            {
                case "Polygon":
                    foreach (var ring in PolygonRings(coords))
                        yield return ring;
                    break;
                case "MultiPolygon":
                    foreach (var poly in coords)
                    foreach (var ring in PolygonRings(poly))
                        yield return ring;
                    break;
            }
        }

        private static IEnumerable<List<PointLatLng>> PolygonRings(JToken polygonCoords)
        {
            var outer = polygonCoords.First as JArray;
            if (outer == null)
                yield break;

            var ring = new List<PointLatLng>();
            foreach (var pt in outer)
            {
                if (pt is JArray arr && arr.Count >= 2)
                {
                    var lng = arr[0].Value<double>();
                    var lat = arr[1].Value<double>();
                    ring.Add(new PointLatLng(lat, lng));
                }
            }

            if (ring.Count >= 3)
                yield return ring;
        }

        private static PointLatLng Centroid(List<PointLatLng> ring)
        {
            if (ring == null || ring.Count == 0)
                return PointLatLng.Empty;

            var lat = ring.Average(p => p.Lat);
            var lng = ring.Average(p => p.Lng);
            return new PointLatLng(lat, lng);
        }

        private static void ApplyPolygons(
            GMapOverlay overlay,
            List<(string Name, string ToolTip, List<PointLatLng> Ring)> features,
            Color fill,
            Color stroke,
            float strokeWidth,
            string namePrefix,
            bool visible,
            Action<Action> invokeOnUi)
        {
            if (overlay == null)
                return;

            features = features ?? new List<(string, string, List<PointLatLng>)>();

            invokeOnUi(() =>
            {
                overlay.Polygons.Clear();
                foreach (var feat in features)
                {
                    if (feat.Ring == null || feat.Ring.Count < 3)
                        continue;

                    var poly = new RestrictionGMapPolygon(feat.Ring, namePrefix + feat.Name)
                    {
                        Fill = new SolidBrush(fill),
                        Stroke = new Pen(stroke, strokeWidth),
                        Tag = feat.ToolTip,
                        IsVisible = true
                    };
                    overlay.Polygons.Add(poly);
                }

                overlay.IsVisibile = visible;
                SyncOverlayGeometry(overlay);

                if (features.Count > 0 && overlay.Polygons.Count == 0)
                    log.Warn($"Restriction overlay {overlay.Id}: parsed {features.Count} features but none had valid rings");
            });
        }

        private static void ClearOverlay(GMapOverlay overlay, bool visible, Action<Action> invokeOnUi)
        {
            if (overlay == null)
                return;

            invokeOnUi(() =>
            {
                overlay.Polygons.Clear();
                overlay.Markers.Clear();
                overlay.IsVisibile = visible;
                overlay.ForceUpdate();
            });
        }

        public static void SetOverlayVisibility(OverlaySet overlays, bool showTfr, bool showAirspace,
            bool showUasFacility, bool showNotams, bool showSpecialUse)
        {
            if (overlays == null)
                return;

            if (overlays.Tfr != null)
                overlays.Tfr.IsVisibile = showTfr;
            if (overlays.Airspace != null)
                overlays.Airspace.IsVisibile = showAirspace;
            if (overlays.UasFacility != null)
                overlays.UasFacility.IsVisibile = showUasFacility;
            if (overlays.NotamMarkers != null)
                overlays.NotamMarkers.IsVisibile = showNotams;
            if (overlays.SpecialUse != null)
                overlays.SpecialUse.IsVisibile = showSpecialUse;
        }
    }
}
