using System;
using System.Drawing;
using System.Linq;
using GMap.NET;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;

namespace MissionPlanner.Utilities
{
    /// <summary>
    /// Restriction overlays skip GMap's lat/lng bbox culling so polygons stay visible when zoomed in,
    /// but paint is clipped to <see cref="DisplayZone"/> (typically the ~75 km fetch box).
    /// </summary>
    public sealed class RestrictionMapOverlay : GMapOverlay
    {
        /// <summary>When set, only draw inside this lat/lng rectangle (vehicle-centered fetch zone).</summary>
        public RectLatLng DisplayZone { get; set; } = RectLatLng.Empty;

        public RestrictionMapOverlay(string id)
            : base(id)
        {
        }

        public override void OnRender(System.Drawing.IGraphics g)
        {
            if (Control == null || !IsVisibile)
                return;

            var clipped = TryClipToDisplayZone(g);
            try
            {
                if (Control.PolygonsEnabled)
                {
                    foreach (GMapPolygon poly in Polygons.ToArray())
                    {
                        if (poly == null || !poly.IsVisible || poly.Points.Count <= 1)
                            continue;

                        poly.OnRender(g);
                    }
                }

                if (Control.MarkersEnabled)
                {
                    foreach (GMapMarker marker in Markers.ToArray())
                    {
                        if (marker == null)
                            continue;

                        if (!DisplayZone.IsEmpty && !PointInZone(marker.Position, DisplayZone))
                            continue;

                        if (marker.IsVisible || marker.DisableRegionCheck)
                            marker.OnRender(g);
                    }
                }
            }
            finally
            {
                if (clipped)
                    g.ResetClip();
            }
        }

        bool TryClipToDisplayZone(System.Drawing.IGraphics g)
        {
            if (DisplayZone.IsEmpty || Control == null)
                return false;

            var topLeft = Control.FromLatLngToLocal(DisplayZone.LocationTopLeft);
            var bottomRight = Control.FromLatLngToLocal(DisplayZone.LocationRightBottom);

            var x = (int)Math.Min(topLeft.X, bottomRight.X);
            var y = (int)Math.Min(topLeft.Y, bottomRight.Y);
            var w = (int)Math.Abs(bottomRight.X - topLeft.X);
            var h = (int)Math.Abs(bottomRight.Y - topLeft.Y);
            if (w < 2 || h < 2)
                return false;

            g.SetClip(new Rectangle(x, y, w, h), System.Drawing.Drawing2D.CombineMode.Intersect);
            return true;
        }

        static bool PointInZone(PointLatLng pt, RectLatLng zone)
        {
            if (pt.IsEmpty || zone.IsEmpty)
                return true;

            var left = Math.Min(zone.Left, zone.Right);
            var right = Math.Max(zone.Left, zone.Right);
            var bottom = Math.Min(zone.Bottom, zone.Top);
            var top = Math.Max(zone.Bottom, zone.Top);
            return pt.Lng >= left && pt.Lng <= right && pt.Lat >= bottom && pt.Lat <= top;
        }
    }
}
