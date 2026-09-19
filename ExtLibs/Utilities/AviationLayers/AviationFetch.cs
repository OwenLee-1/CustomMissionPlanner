using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GMap.NET;
using Newtonsoft.Json.Linq;

namespace MissionPlanner.Utilities.AviationLayers
{
    public static class AviationFetch
    {
        public const string TfrWfsUrl = "https://tfr.faa.gov/geoserver/TFR/ows";
        public const string TfrLayerName = "TFR:V_TFR_LOC";

        public const string UasFacilityMapUrl =
            "https://services6.arcgis.com/ssFJjBXIUyZDrSYZ/arcgis/rest/services/FAA_UAS_FacilityMap_Data/FeatureServer/0";

        public const string DodUasRestrictionUrl =
            "https://services6.arcgis.com/ssFJjBXIUyZDrSYZ/arcgis/rest/services/DoD_Mar_13/FeatureServer/0";

        public static string AirspaceWfsUrl { get; set; } = "";

        /// <summary>When true (default), load airspace from OpenAIP country GeoJSON exports instead of WFS.</summary>
        public static bool UseOpenAipExport { get; set; } = true;

        public static string AirspaceLayerName { get; set; } = "openaip:airspaces";

        public const string MetarApiBase = "https://aviationweather.gov/api/data/metar";
        public const string TafApiBase = "https://aviationweather.gov/api/data/taf";
        public const string SigmetApiBase = "https://aviationweather.gov/api/data/isigmet";
        public const string GairmetApiBase = "https://aviationweather.gov/api/data/gairmet";
        public const string PirepApiBase = "https://aviationweather.gov/api/data/pirep";

        public static string BuildWfsUrl(string serviceUrl, string typeName, RectLatLng bounds)
        {
            var bbox = string.Format(CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},EPSG:4326",
                bounds.Left, bounds.Bottom, bounds.Right, bounds.Top);

            var maxFeatures = AviationBounds.MaxWfsFeatures(bounds);

            return string.Format(CultureInfo.InvariantCulture,
                "{0}?service=WFS&version=1.0.0&request=GetFeature&typeName={1}&outputFormat=application/json&bbox={2}&maxFeatures={3}",
                serviceUrl.TrimEnd('/'), Uri.EscapeDataString(typeName), Uri.EscapeDataString(bbox), maxFeatures);
        }

        public static string BuildArcGisQueryUrl(string layerUrl, RectLatLng bounds, int maxFeatures)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0}/query?where=1%3D1&geometry={1},{2},{3},{4}&geometryType=esriGeometryEnvelope&inSR=4326&spatialRel=esriSpatialRelIntersects&outFields=*&returnGeometry=true&f=geojson&resultRecordCount={5}",
                layerUrl.TrimEnd('/'),
                bounds.Left, bounds.Bottom, bounds.Right, bounds.Top,
                maxFeatures);
        }

        public static string BuildMetarUrl(RectLatLng bounds)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0}?format=json&bbox={1},{2},{3},{4}",
                MetarApiBase,
                bounds.Left, bounds.Bottom, bounds.Right, bounds.Top);
        }

        public static string BuildSigmetUrl(RectLatLng bounds)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0}?format=geojson&bbox={1},{2},{3},{4}",
                SigmetApiBase,
                bounds.Left, bounds.Bottom, bounds.Right, bounds.Top);
        }

        public static string BuildGairmetUrl(RectLatLng bounds)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0}?format=geojson&bbox={1},{2},{3},{4}",
                GairmetApiBase,
                bounds.Left, bounds.Bottom, bounds.Right, bounds.Top);
        }

        public static string BuildPirepUrl(RectLatLng bounds)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0}?format=geojson&bbox={1},{2},{3},{4}",
                PirepApiBase,
                bounds.Left, bounds.Bottom, bounds.Right, bounds.Top);
        }

        public static string BuildTafUrl(RectLatLng bounds)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0}?format=json&bbox={1},{2},{3},{4}",
                TafApiBase,
                bounds.Left, bounds.Bottom, bounds.Right, bounds.Top);
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
    }
}
