using System;
using System.Collections.Generic;
using System.Linq;
using GMap.NET;

namespace MissionPlanner.Utilities.AviationLayers
{
    public static class FlightSafetyAnalyzer
    {
        public static List<FlightSafetyWarning> AnalyzeMission(
            IEnumerable<PointLatLng> missionPoints,
            IList<MapPolygonFeature> tfrPolygons,
            IList<MapPolygonFeature> specialUsePolygons,
            IList<MapPolygonFeature> sigmetPolygons)
        {
            var warnings = new List<FlightSafetyWarning>();
            if (missionPoints == null)
                return warnings;

            var index = 0;
            foreach (var pt in missionPoints)
            {
                if (pt.IsEmpty)
                {
                    index++;
                    continue;
                }

                index++;
                CheckPolygons(warnings, pt, tfrPolygons, "TFR", "TFR_INTERSECT",
                    $"Waypoint {index} intersects active TFR airspace");
                CheckPolygons(warnings, pt, specialUsePolygons, "SUA", "SUA_INTERSECT",
                    $"Waypoint {index} intersects special-use / restricted airspace");
                CheckPolygons(warnings, pt, sigmetPolygons, "SIGMET", "SIGMET_INTERSECT",
                    $"Waypoint {index} inside SIGMET area");
            }

            return warnings;
        }

        static void CheckPolygons(
            List<FlightSafetyWarning> warnings,
            PointLatLng pt,
            IList<MapPolygonFeature> polygons,
            string relatedPrefix,
            string code,
            string messageTemplate)
        {
            if (polygons == null)
                return;

            foreach (var poly in polygons)
            {
                if (poly?.Ring == null || poly.Ring.Count < 3)
                    continue;

                if (!PointInPolygon(pt, poly.Ring))
                    continue;

                warnings.Add(new FlightSafetyWarning
                {
                    Code = code,
                    Severity = "warning",
                    Message = messageTemplate + ": " + poly.ToolTip,
                    Location = pt,
                    RelatedId = relatedPrefix + ":" + poly.Id
                });
                break;
            }
        }

        public static bool PointInPolygon(PointLatLng point, IList<PointLatLng> ring)
        {
            if (point.IsEmpty || ring == null || ring.Count < 3)
                return false;

            var inside = false;
            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                var pi = ring[i];
                var pj = ring[j];
                if (((pi.Lat > point.Lat) != (pj.Lat > point.Lat)) &&
                    (point.Lng <
                     (pj.Lng - pi.Lng) * (point.Lat - pi.Lat) / (pj.Lat - pi.Lat + double.Epsilon) + pi.Lng))
                {
                    inside = !inside;
                }
            }

            return inside;
        }
    }
}
