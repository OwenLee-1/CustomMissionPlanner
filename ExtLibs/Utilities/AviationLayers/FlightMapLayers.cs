using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GMap.NET.WindowsForms;

namespace MissionPlanner.Utilities.AviationLayers
{
    public interface IFlightMapLayer
    {
        string Id { get; }
        string DisplayName { get; }
        bool Enabled { get; set; }
        LayerSnapshot LastSnapshot { get; }

        void ClearMap();
        void ApplyToMap();
    }

    public abstract class GMapPolygonFlightLayer : IFlightMapLayer
    {
        public GMapOverlay MapOverlay { get; }
        readonly List<MapPolygonFeature> _features = new List<MapPolygonFeature>();

        protected abstract Color Fill { get; }
        protected abstract Color Stroke { get; }
        protected abstract float StrokeWidth { get; }
        protected abstract string NamePrefix { get; }

        protected GMapPolygonFlightLayer(string id, string displayName, GMapOverlay overlay)
        {
            Id = id;
            DisplayName = displayName;
            MapOverlay = overlay;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public bool Enabled { get; set; }
        public LayerSnapshot LastSnapshot { get; protected set; } = new LayerSnapshot();

        public Color FillColor => Fill;
        public Color StrokeColor => Stroke;
        public float StrokeWidthPx => StrokeWidth;
        public string PolygonNamePrefix => NamePrefix;

        public IReadOnlyList<MapPolygonFeature> GetFeatures() => _features;

        public void SetFeatures(IList<MapPolygonFeature> features, LayerSnapshot snapshot)
        {
            _features.Clear();
            if (features != null)
                _features.AddRange(features);
            LastSnapshot = snapshot ?? LastSnapshot;
        }

        public void ClearMap()
        {
            _features.Clear();
            MapOverlayHelper.Clear(MapOverlay, false);
        }

        public void ApplyToMap()
        {
            if (AviationMapBridge.ApplyLayerToMap != null)
                AviationMapBridge.ApplyLayerToMap(this);
        }
    }

    public sealed class TfrMapLayer : GMapPolygonFlightLayer
    {
        public TfrMapLayer(GMapOverlay overlay)
            : base("tfr", "TFRs", overlay)
        {
        }

        protected override Color Fill => Color.FromArgb(75, Color.OrangeRed);
        protected override Color Stroke => Color.OrangeRed;
        protected override float StrokeWidth => 3f;
        protected override string NamePrefix => "TFR";

        public TfrFetchResult LastFetch { get; private set; } = TfrFetchResult.Empty;

        public void SetFetchResult(TfrFetchResult result, LayerSnapshot snapshot)
        {
            LastFetch = result ?? TfrFetchResult.Empty;
            SetFeatures(result?.Polygons, snapshot);
        }
    }

    public sealed class AirspaceMapLayer : GMapPolygonFlightLayer
    {
        public AirspaceMapLayer(GMapOverlay overlay)
            : base("airspace", "Airspace", overlay)
        {
        }

        protected override Color Fill => Color.FromArgb(55, Color.DodgerBlue);
        protected override Color Stroke => Color.SteelBlue;
        protected override float StrokeWidth => 2.5f;
        protected override string NamePrefix => "ASP";
    }

    public sealed class UasFacilityMapLayer : GMapPolygonFlightLayer
    {
        public UasFacilityMapLayer(GMapOverlay overlay)
            : base("uas", "LAANC grid", overlay)
        {
        }

        protected override Color Fill => Color.FromArgb(55, Color.Gold);
        protected override Color Stroke => Color.Goldenrod;
        protected override float StrokeWidth => 2f;
        protected override string NamePrefix => "UAS";
    }

    public sealed class SpecialUseMapLayer : GMapPolygonFlightLayer
    {
        public SpecialUseMapLayer(GMapOverlay overlay)
            : base("specialuse", "Special use", overlay)
        {
        }

        protected override Color Fill => Color.FromArgb(60, Color.MediumPurple);
        protected override Color Stroke => Color.Purple;
        protected override float StrokeWidth => 2.5f;
        protected override string NamePrefix => "SUA";
    }

    public sealed class SigmetMapLayer : GMapPolygonFlightLayer
    {
        public SigmetMapLayer(GMapOverlay overlay)
            : base(AviationLayerIds.Sigmet, "SIGMET", overlay)
        {
        }

        protected override Color Fill => Color.FromArgb(45, Color.DarkOrange);
        protected override Color Stroke => Color.OrangeRed;
        protected override float StrokeWidth => 2f;
        protected override string NamePrefix => "SIG";
    }

    public sealed class GairmetMapLayer : GMapPolygonFlightLayer
    {
        public GairmetMapLayer(GMapOverlay overlay)
            : base(AviationLayerIds.Gairmet, "G-AIRMET", overlay)
        {
        }

        protected override Color Fill => Color.FromArgb(50, Color.MediumSeaGreen);
        protected override Color Stroke => Color.SeaGreen;
        protected override float StrokeWidth => 2f;
        protected override string NamePrefix => "GAM";
    }

    public sealed class PirepMapLayer : IFlightMapLayer
    {
        public GMapOverlay MapOverlay { get; }
        List<PirepObservation> _reports = new List<PirepObservation>();

        public PirepMapLayer(GMapOverlay overlay)
        {
            MapOverlay = overlay;
        }

        public string Id => AviationLayerIds.Pirep;
        public string DisplayName => "PIREP";
        public bool Enabled { get; set; }
        public LayerSnapshot LastSnapshot { get; private set; } = new LayerSnapshot();

        public IReadOnlyList<PirepObservation> GetReports() => _reports;

        public void SetReports(IList<PirepObservation> reports, LayerSnapshot snapshot)
        {
            _reports = reports?.ToList() ?? new List<PirepObservation>();
            LastSnapshot = snapshot;
        }

        public void ClearMap()
        {
            _reports.Clear();
            MapOverlayHelper.Clear(MapOverlay, false);
        }

        public void ApplyToMap()
        {
            if (AviationMapBridge.ApplyLayerToMap != null)
                AviationMapBridge.ApplyLayerToMap(this);
        }
    }

    public sealed class NotamMapLayer : IFlightMapLayer
    {
        public GMapOverlay MapOverlay { get; }
        List<AviationNotice> _notices = new List<AviationNotice>();

        public NotamMapLayer(GMapOverlay overlay)
        {
            MapOverlay = overlay;
        }

        public string Id => "notam";
        public string DisplayName => "NOTAMs";
        public bool Enabled { get; set; }
        public LayerSnapshot LastSnapshot { get; private set; } = new LayerSnapshot();

        public IReadOnlyList<AviationNotice> GetNotices() => _notices;

        public void SetNotices(IList<AviationNotice> notices, LayerSnapshot snapshot)
        {
            _notices = notices?.ToList() ?? new List<AviationNotice>();
            LastSnapshot = snapshot;
        }

        public void ClearMap()
        {
            _notices.Clear();
            MapOverlayHelper.Clear(MapOverlay, false);
        }

        public void ApplyToMap()
        {
            if (AviationMapBridge.ApplyLayerToMap != null)
                AviationMapBridge.ApplyLayerToMap(this);
        }
    }

    public sealed class MetarMapLayer : IFlightMapLayer
    {
        public GMapOverlay MapOverlay { get; }
        List<MetarObservation> _metars = new List<MetarObservation>();

        public MetarMapLayer(GMapOverlay overlay)
        {
            MapOverlay = overlay;
        }

        public string Id => "metar";
        public string DisplayName => "METAR";
        public bool Enabled { get; set; }
        public LayerSnapshot LastSnapshot { get; private set; } = new LayerSnapshot();

        public IReadOnlyList<MetarObservation> GetMetars() => _metars;

        public void SetMetars(IList<MetarObservation> metars, LayerSnapshot snapshot)
        {
            _metars = metars?.ToList() ?? new List<MetarObservation>();
            LastSnapshot = snapshot;
        }

        public void ClearMap()
        {
            _metars.Clear();
            MapOverlayHelper.Clear(MapOverlay, false);
        }

        public void ApplyToMap()
        {
            if (AviationMapBridge.ApplyLayerToMap != null)
                AviationMapBridge.ApplyLayerToMap(this);
        }
    }

    public sealed class FlightSafetyMapLayer : IFlightMapLayer
    {
        public GMapOverlay MapOverlay { get; }
        List<FlightSafetyWarning> _warnings = new List<FlightSafetyWarning>();

        public FlightSafetyMapLayer(GMapOverlay overlay)
        {
            MapOverlay = overlay;
        }

        public string Id => "flightsafety";
        public string DisplayName => "Flight safety";
        public bool Enabled { get; set; }
        public LayerSnapshot LastSnapshot { get; private set; } = new LayerSnapshot();

        public IReadOnlyList<FlightSafetyWarning> GetWarnings() => _warnings;

        public void SetWarnings(IList<FlightSafetyWarning> warnings, LayerSnapshot snapshot)
        {
            _warnings = warnings?.ToList() ?? new List<FlightSafetyWarning>();
            LastSnapshot = snapshot;
        }

        public void ClearMap()
        {
            _warnings.Clear();
            MapOverlayHelper.Clear(MapOverlay, false);
        }

        public void ApplyToMap()
        {
            if (AviationMapBridge.ApplyLayerToMap != null)
                AviationMapBridge.ApplyLayerToMap(this);
        }
    }

    static class MapOverlayHelper
    {
        public static void Clear(GMapOverlay overlay, bool visible)
        {
            if (overlay == null)
                return;

            overlay.Polygons.Clear();
            overlay.Markers.Clear();
            overlay.IsVisibile = visible;
            overlay.ForceUpdate();
        }
    }
}
