using System.Drawing;
using GMap.NET.WindowsForms;

namespace MissionPlanner.Utilities
{
    /// <summary>
    /// Renders with FillPolygon/DrawPolygon so restriction shapes still draw when local coords exceed GraphicsPath limits.
    /// </summary>
    public sealed class RestrictionGMapPolygon : GMapPolygon
    {
        public RestrictionGMapPolygon(System.Collections.Generic.List<GMap.NET.PointLatLng> points, string name)
            : base(points, name)
        {
        }

#if !PocketPC
        public override void OnRender(IGraphics g)
        {
            if (!IsVisible || LocalPoints == null || LocalPoints.Count < 2)
                return;

            var pnts = new Point[LocalPoints.Count];
            for (var i = 0; i < LocalPoints.Count; i++)
                pnts[i] = new Point((int)LocalPoints[i].X, (int)LocalPoints[i].Y);

            if (pnts.Length > 2)
            {
                g.FillPolygon(Fill, pnts);
                g.DrawPolygon(Stroke, pnts);
            }
            else
            {
                g.DrawLine(Stroke, pnts[0], pnts[1]);
            }
        }
#endif
    }
}
