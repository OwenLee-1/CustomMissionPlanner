using System;

namespace MissionPlanner.Utilities.AviationLayers
{
    /// <summary>
    /// Canonical flight-map layer ids, authoritative sources, and implementation status.
    /// Map UI toggles should align with <see cref="AviationLayerIds"/>.
    /// </summary>
    public static class AviationLayerCatalog
    {
        public static readonly LayerSpec[] All =
        {
            // FAA notices
            new LayerSpec(AviationLayerIds.Tfr, "TFRs", AviationDataAgency.Faa, "FAA TFR WFS (graphic)", true, TimeSpan.FromMinutes(3)),
            new LayerSpec(AviationLayerIds.Notam, "NOTAMs (graphic TFR)", AviationDataAgency.Faa, "FAA TFR WFS-derived polygons; NMS API planned", true, TimeSpan.FromMinutes(3)),
            new LayerSpec(AviationLayerIds.Airspace, "Class airspace", AviationDataAgency.Faa, "NASR shapefiles planned; OpenAIP export interim", true, TimeSpan.FromDays(28)),
            new LayerSpec(AviationLayerIds.SpecialUse, "Special use", AviationDataAgency.Faa, "FAA / DoD ArcGIS + OpenAIP", true, TimeSpan.FromMinutes(10)),
            new LayerSpec(AviationLayerIds.UasFacility, "LAANC grid", AviationDataAgency.Faa, "FAA UAS Facility Map", true, TimeSpan.FromMinutes(5)),
            // AWC weather
            new LayerSpec(AviationLayerIds.Metar, "METAR", AviationDataAgency.NwsAwc, "aviationweather.gov API", true, TimeSpan.FromMinutes(1)),
            new LayerSpec(AviationLayerIds.Taf, "TAF", AviationDataAgency.NwsAwc, "API ready; airport popup not wired", false, TimeSpan.FromMinutes(10)),
            new LayerSpec(AviationLayerIds.Sigmet, "SIGMET", AviationDataAgency.NwsAwc, "aviationweather.gov isigmet", true, TimeSpan.FromMinutes(1)),
            new LayerSpec(AviationLayerIds.Gairmet, "G-AIRMET", AviationDataAgency.NwsAwc, "aviationweather.gov gairmet GeoJSON", true, TimeSpan.FromMinutes(1)),
            new LayerSpec(AviationLayerIds.Pirep, "PIREP/AIREP", AviationDataAgency.NwsAwc, "aviationweather.gov pirep GeoJSON", true, TimeSpan.FromMinutes(1)),
            new LayerSpec(AviationLayerIds.Cwa, "CWA", AviationDataAgency.NwsAwc, "Planned AWC product", false, TimeSpan.FromMinutes(1)),
            // App / other
            new LayerSpec(AviationLayerIds.WeatherRadar, "Weather radar", AviationDataAgency.Nws, "RainViewer (NWS mosaic proxy)", true, TimeSpan.FromMinutes(1)),
            new LayerSpec(AviationLayerIds.FlightSafety, "Mission safety", AviationDataAgency.App, "Local mission vs TFR/SUA/SIGMET", true, TimeSpan.Zero),
            new LayerSpec(AviationLayerIds.NoFly, "Custom NoFly", AviationDataAgency.App, "User geofences", true, TimeSpan.Zero),
            new LayerSpec(AviationLayerIds.Adsb, "ADS-B traffic", AviationDataAgency.ThirdParty, "Mission Planner ADS-B feed", false, TimeSpan.Zero),
        };
    }

    public static class AviationLayerIds
    {
        public const string Tfr = "tfr";
        public const string Notam = "notam";
        public const string Airspace = "airspace";
        public const string SpecialUse = "specialuse";
        public const string UasFacility = "uas";
        public const string Metar = "metar";
        public const string Taf = "taf";
        public const string Sigmet = "sigmet";
        public const string Gairmet = "gairmet";
        public const string Pirep = "pirep";
        public const string Cwa = "cwa";
        public const string WeatherRadar = "weather";
        public const string FlightSafety = "flightsafety";
        public const string NoFly = "nofly";
        public const string Adsb = "adsb";
    }

    public enum AviationDataAgency
    {
        Faa,
        NwsAwc,
        Nws,
        App,
        ThirdParty
    }

    public sealed class LayerSpec
    {
        public LayerSpec(string id, string displayName, AviationDataAgency agency, string sourceNote, bool mapToggleImplemented, TimeSpan refreshHint)
        {
            Id = id;
            DisplayName = displayName;
            Agency = agency;
            SourceNote = sourceNote;
            MapToggleImplemented = mapToggleImplemented;
            RefreshHint = refreshHint;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public AviationDataAgency Agency { get; }
        public string SourceNote { get; }
        public bool MapToggleImplemented { get; }
        public TimeSpan RefreshHint { get; }
    }
}
