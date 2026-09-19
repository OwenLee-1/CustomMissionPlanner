using System;
using System.Collections.Generic;
using GMap.NET;

namespace MissionPlanner.Utilities.AviationLayers
{
    public sealed class FlightMapVisibility
    {
        public bool Tfr { get; set; }
        public bool Airspace { get; set; }
        public bool UasFacility { get; set; }
        public bool Notams { get; set; }
        public bool SpecialUse { get; set; }
        public bool Metar { get; set; }
        public bool Sigmet { get; set; }
        public bool Gairmet { get; set; }
        public bool Pirep { get; set; }
        public bool FlightSafety { get; set; }
    }

    public sealed class FlightMapLayerContext
    {
        public Action<Action> InvokeOnUi { get; set; } = a => a();
        public Action<List<NotamBriefingItem>> BriefingUpdated { get; set; }
        public Action<RestrictionBriefingSummary> SummaryUpdated { get; set; }
        public Action BringRestrictionOverlaysToFront { get; set; }
        public Func<IEnumerable<PointLatLng>> GetMissionPoints { get; set; }
        public bool LoadBriefing { get; set; }
    }

    public sealed class FlightMapLayerStack
    {
        public TfrMapLayer Tfr { get; set; }
        public AirspaceMapLayer Airspace { get; set; }
        public UasFacilityMapLayer Uas { get; set; }
        public NotamMapLayer Notam { get; set; }
        public SpecialUseMapLayer SpecialUse { get; set; }
        public MetarMapLayer Metar { get; set; }
        public SigmetMapLayer Sigmet { get; set; }
        public GairmetMapLayer Gairmet { get; set; }
        public PirepMapLayer Pirep { get; set; }
        public FlightSafetyMapLayer FlightSafety { get; set; }

        public IEnumerable<IFlightMapLayer> All =>
            new IFlightMapLayer[] { Tfr, Airspace, Uas, Notam, SpecialUse, Metar, Sigmet, Gairmet, Pirep, FlightSafety };
    }
}
