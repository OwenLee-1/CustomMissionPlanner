using System;
using GMap.NET;
using MissionPlanner.GCSViews;

namespace MissionPlanner.Utilities
{
    public sealed class MissionFlightEstimate
    {
        public double DistanceKm { get; set; }
        public double CruiseSpeedMps { get; set; }
        public double EstimatedMinutes { get; set; }
        public int WaypointCount { get; set; }
        public string Summary { get; set; }

        public static MissionFlightEstimate Compute()
        {
            var result = new MissionFlightEstimate { CruiseSpeedMps = 5 };

            var planner = FlightPlanner.instance;
            var points = planner?.pointlist;
            if (points == null || points.Count < 2)
            {
                result.Summary = "No mission loaded";
                return result;
            }

            result.WaypointCount = points.Count;

            var proj = planner.MainMap?.MapProvider?.Projection;
            if (proj == null)
            {
                result.Summary = WaypointCountText(result.WaypointCount) + " · distance unknown";
                return result;
            }

            double distKm = 0;
            for (var i = 1; i < points.Count; i++)
                distKm += proj.GetDistance(points[i - 1], points[i]);

            result.DistanceKm = distKm;

            if (MainV2.comPort?.MAV?.param != null)
            {
                if (MainV2.comPort.MAV.param.ContainsKey("WPNAV_SPEED"))
                    result.CruiseSpeedMps = MainV2.comPort.MAV.param["WPNAV_SPEED"].Value / 100f;
                else if (MainV2.comPort.MAV.param.ContainsKey("AIRSPEED_CRUISE"))
                    result.CruiseSpeedMps = MainV2.comPort.MAV.param["AIRSPEED_CRUISE"].Value;
                else if (MainV2.comPort.MAV.cs.groundspeed > 1)
                    result.CruiseSpeedMps = MainV2.comPort.MAV.cs.groundspeed;
            }

            if (result.CruiseSpeedMps < 0.5)
                result.CruiseSpeedMps = 5;

            var seconds = distKm * 1000 / result.CruiseSpeedMps;
            seconds += Math.Max(0, points.Count - 2) * 5;
            result.EstimatedMinutes = seconds / 60;

            result.Summary = WaypointCountText(result.WaypointCount)
                             + " · " + distKm.ToString("0.00") + " km · ~"
                             + result.EstimatedMinutes.ToString("0") + " min @ "
                             + result.CruiseSpeedMps.ToString("0.0") + " m/s";

            return result;
        }

        static string WaypointCountText(int count) => count + " waypoint" + (count == 1 ? "" : "s");
    }
}
