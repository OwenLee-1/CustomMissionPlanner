using System;
using GMap.NET;

namespace MissionPlanner.Utilities.AviationLayers
{
    public static class AviationBounds
    {
        /// <summary>Default fetch radius around vehicle / map center (km).</summary>
        public const double DefaultFetchRadiusKm = 75;

        /// <summary>
        /// Prefer a fixed radius around the vehicle (or map center) so pan/zoom never requests continental bboxes.
        /// </summary>
        public static RectLatLng NormalizeForFetch(RectLatLng view, PointLatLng center, double zoom,
            double radiusKm = DefaultFetchRadiusKm)
        {
            radiusKm = ClampRadiusKm(radiusKm);

            if (center.IsEmpty && !view.IsEmpty)
                center = view.LocationMiddle;

            if (!center.IsEmpty)
                return BoxAroundKm(center, radiusKm);

            if (view.IsEmpty)
                return view;

            var left = Math.Min(view.Left, view.Right);
            var right = Math.Max(view.Left, view.Right);
            var bottom = Math.Min(view.Bottom, view.Top);
            var top = Math.Max(view.Bottom, view.Top);
            view = RectLatLng.FromLTRB(left, top, right, bottom);

            return CapToRadius(view, view.LocationMiddle, radiusKm);
        }

        public static double ClampRadiusKm(double radiusKm)
        {
            if (double.IsNaN(radiusKm) || radiusKm <= 0)
                return DefaultFetchRadiusKm;
            return Math.Max(25, Math.Min(150, radiusKm));
        }

        public static RectLatLng BoxAroundKm(PointLatLng center, double radiusKm)
        {
            if (center.IsEmpty)
                return RectLatLng.Empty;

            radiusKm = ClampRadiusKm(radiusKm);
            var halfLat = radiusKm / 111.32;
            var cosLat = Math.Cos(center.Lat * Math.PI / 180.0);
            var halfLng = radiusKm / (111.32 * Math.Max(0.2, Math.Abs(cosLat)));

            return RectLatLng.FromLTRB(
                center.Lng - halfLng,
                center.Lat + halfLat,
                center.Lng + halfLng,
                center.Lat - halfLat);
        }

        public static RectLatLng CapToRadius(RectLatLng view, PointLatLng center, double radiusKm)
        {
            var cap = BoxAroundKm(center.IsEmpty ? view.LocationMiddle : center, radiusKm);
            if (view.IsEmpty)
                return cap;
            if (cap.IsEmpty)
                return view;

            var left = Math.Max(Math.Min(view.Left, view.Right), Math.Min(cap.Left, cap.Right));
            var right = Math.Min(Math.Max(view.Left, view.Right), Math.Max(cap.Left, cap.Right));
            var bottom = Math.Max(Math.Min(view.Bottom, view.Top), Math.Min(cap.Bottom, cap.Top));
            var top = Math.Min(Math.Max(view.Bottom, view.Top), Math.Max(cap.Bottom, cap.Top));

            if (right <= left || top <= bottom)
                return cap;

            return RectLatLng.FromLTRB(left, top, right, bottom);
        }

        public static RectLatLng InflateFetchBounds(RectLatLng view)
        {
            if (view.IsEmpty)
                return view;

            var b = view;
            var padLat = Math.Max(Math.Abs(view.HeightLat) * 0.05, 0.00008);
            var padLng = Math.Max(Math.Abs(view.WidthLng) * 0.05, 0.00008);
            b.Inflate(padLat, padLng);
            return b;
        }

        public static bool RingIntersects(RectLatLng view, System.Collections.Generic.IList<PointLatLng> ring)
        {
            if (view.IsEmpty || ring == null || ring.Count < 2)
                return false;

            var minLat = ring[0].Lat;
            var maxLat = ring[0].Lat;
            var minLng = ring[0].Lng;
            var maxLng = ring[0].Lng;
            for (var i = 1; i < ring.Count; i++)
            {
                var p = ring[i];
                if (p.Lat < minLat) minLat = p.Lat;
                if (p.Lat > maxLat) maxLat = p.Lat;
                if (p.Lng < minLng) minLng = p.Lng;
                if (p.Lng > maxLng) maxLng = p.Lng;
            }

            var left = Math.Min(view.Left, view.Right);
            var right = Math.Max(view.Left, view.Right);
            var bottom = Math.Min(view.Bottom, view.Top);
            var top = Math.Max(view.Bottom, view.Top);

            return maxLat >= bottom && minLat <= top && maxLng >= left && minLng <= right;
        }

        public static System.Collections.Generic.List<MapPolygonFeature> FilterPolygonsInBounds(
            System.Collections.Generic.IEnumerable<MapPolygonFeature> features, RectLatLng bounds)
        {
            var result = new System.Collections.Generic.List<MapPolygonFeature>();
            if (features == null || bounds.IsEmpty)
                return result;

            foreach (var feat in features)
            {
                if (feat?.Ring == null || feat.Ring.Count < 3)
                    continue;
                if (!RingIntersects(bounds, feat.Ring))
                    continue;
                result.Add(feat);
            }

            return result;
        }

        public static int MaxWfsFeatures(RectLatLng bounds)
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

        public static int MaxArcGisFeatures(RectLatLng bounds)
        {
            var area = Math.Abs(bounds.WidthLng * bounds.HeightLat);
            if (area <= 0.0005)
                return 5000;
            if (area <= 0.05)
                return 3500;
            return 2000;
        }
    }
}
