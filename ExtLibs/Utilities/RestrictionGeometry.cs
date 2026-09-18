using System;
using System.Collections.Generic;
using System.Linq;
using GMap.NET;
using GMap.NET.WindowsForms;

namespace MissionPlanner.Utilities
{
    public static class RestrictionGeometry
    {
        public class TfrConflict
        {
            public string Label { get; set; }
            public string PolygonName { get; set; }
            public PointLatLng Point { get; set; }
        }

        public static bool PointInPolygon(PointLatLng point, IList<PointLatLng> polygon)
        {
            if (polygon == null || polygon.Count < 3)
                return false;

            var inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var pi = polygon[i];
                var pj = polygon[j];
                if ((pi.Lng > point.Lng) != (pj.Lng > point.Lng) &&
                    point.Lat <
                    (pj.Lat - pi.Lat) * (point.Lng - pi.Lng) / (pj.Lng - pi.Lng + double.Epsilon) + pi.Lat)
                    inside = !inside;
            }

            return inside;
        }

        public static List<TfrConflict> FindTfrConflicts(
            IEnumerable<PointLatLng> checkPoints,
            IEnumerable<GMapPolygon> tfrPolygons)
        {
            var conflicts = new List<TfrConflict>();
            if (checkPoints == null || tfrPolygons == null)
                return conflicts;

            foreach (var point in checkPoints)
            {
                if (point.IsEmpty)
                    continue;

                foreach (var poly in tfrPolygons)
                {
                    if (poly?.Points == null || poly.Points.Count < 3)
                        continue;

                    if (!PointInPolygon(point, poly.Points))
                        continue;

                    var label = poly.Tag as string ?? poly.Name ?? "TFR";
                    conflicts.Add(new TfrConflict
                    {
                        Label = label,
                        PolygonName = poly.Name,
                        Point = point
                    });
                }
            }

            return conflicts
                .GroupBy(c => c.PolygonName + "@" + c.Point.Lat.ToString("F5") + c.Point.Lng.ToString("F5"))
                .Select(g => g.First())
                .ToList();
        }

        public static IEnumerable<PointLatLng> CollectPreflightCheckPoints(
            PointLatLngAlt home,
            PointLatLngAlt current,
            IEnumerable<PointLatLngAlt> mission)
        {
            var list = new List<PointLatLng>();
            if (home != PointLatLngAlt.Zero)
                list.Add(home);
            else if (current != PointLatLngAlt.Zero)
                list.Add(current);

            if (mission != null)
            {
                foreach (var wp in mission)
                {
                    if (wp != PointLatLngAlt.Zero)
                        list.Add(wp);
                }
            }

            return list;
        }
    }
}
