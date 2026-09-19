using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.WindowsForms;
using log4net;

namespace MissionPlanner.Utilities.AviationLayers
{
    public sealed class FlightMapLayerManager : IDisposable
    {
        static readonly ILog Log = LogManager.GetLogger(typeof(FlightMapLayerManager));

        readonly FlightMapLayerStack _stack;
        readonly FlightMapLayerContext _context;
        readonly FaaTfrDataProvider _tfrProvider = new FaaTfrDataProvider();
        readonly FaaNasrAirspaceProvider _airspaceProvider = new FaaNasrAirspaceProvider();
        readonly UasFacilityDataProvider _uasProvider = new UasFacilityDataProvider();
        readonly SpecialUseDataProvider _specialProvider = new SpecialUseDataProvider();
        readonly AwcMetarDataProvider _metarProvider = new AwcMetarDataProvider();
        readonly AwcSigmetDataProvider _sigmetProvider = new AwcSigmetDataProvider();
        readonly AwcGairmetDataProvider _gairmetProvider = new AwcGairmetDataProvider();
        readonly AwcPirepDataProvider _pirepProvider = new AwcPirepDataProvider();

        readonly System.Windows.Forms.Timer _debounce;
        readonly System.Windows.Forms.Timer _zoomDebounce;
        FlightMapVisibility _visibility = new FlightMapVisibility();

        CancellationTokenSource _refreshCts;
        GMapControl _map;
        bool _mapReady;
        bool _disposed;

        public FlightMapLayerManager(FlightMapLayerStack stack, FlightMapLayerContext context)
        {
            _stack = stack ?? throw new ArgumentNullException(nameof(stack));
            _context = context ?? new FlightMapLayerContext();

            _debounce = new System.Windows.Forms.Timer { Interval = AviationRefreshPolicy.PanDebounceMs };
            _zoomDebounce = new System.Windows.Forms.Timer { Interval = AviationRefreshPolicy.ZoomDebounceMs };
            _tfrProvider.CacheTtl = AviationRefreshPolicy.TfrCache;
            _metarProvider.CacheTtl = AviationRefreshPolicy.AwcWeatherCache;
            _sigmetProvider.CacheTtl = AviationRefreshPolicy.SigmetCache;
            _gairmetProvider.CacheTtl = AviationRefreshPolicy.AwcWeatherCache;
            _pirepProvider.CacheTtl = AviationRefreshPolicy.AwcWeatherCache;
            _uasProvider.CacheTtl = AviationRefreshPolicy.UasCache;
            _specialProvider.CacheTtl = AviationRefreshPolicy.SpecialUseCache;
            _zoomDebounce.Tick += (s, e) =>
            {
                _zoomDebounce.Stop();
                RefreshNow();
            };
            _debounce.Tick += (s, e) =>
            {
                _debounce.Stop();
                RefreshNow();
            };
        }

        public IReadOnlyList<LayerSnapshot> LastSnapshots =>
            _stack.All.Where(l => l != null).Select(l => l.LastSnapshot).ToList();

        public void Attach(GMapControl map)
        {
            DetachHandlers();
            _map = map;
            if (_map == null)
                return;

            _map.OnMapZoomChanged += OnViewChanged;
            _map.OnPositionChanged += OnPositionChanged;
            _map.Load += OnMapLoad;
        }

        void OnMapLoad(object sender, EventArgs e) => TryMarkMapReady();

        void OnViewChanged()
        {
            if (_mapReady && _map != null)
            {
                var radiusKm = Settings.Instance.GetDouble("aviationFetchRadiusKm", AviationRefreshPolicy.FetchRadiusKm);
                var zone = AviationBounds.NormalizeForFetch(_map.ViewArea, ResolveFetchCenter(), _map.Zoom, radiusKm);
                ApplyDisplayZone(zone);
                RefreshOverlayGeometryOnly();
                _map.Invalidate();
            }

            _debounce.Stop();
            _zoomDebounce.Stop();
            _zoomDebounce.Start();
        }

        void RefreshOverlayGeometryOnly()
        {
            foreach (var layer in _stack.All)
            {
                if (layer == null || !layer.Enabled)
                    continue;

                GMapOverlay overlay = null;
                if (layer is GMapPolygonFlightLayer pl)
                    overlay = pl.MapOverlay;
                else if (layer is NotamMapLayer n)
                    overlay = n.MapOverlay;
                else if (layer is MetarMapLayer m)
                    overlay = m.MapOverlay;
                else if (layer is PirepMapLayer pr)
                    overlay = pr.MapOverlay;
                else if (layer is FlightSafetyMapLayer f)
                    overlay = f.MapOverlay;

                if (overlay != null)
                    AviationGMapApply.RefreshOverlayGeometry(overlay);
            }
        }

        void OnPositionChanged(PointLatLng point) => ScheduleRefresh();

        public void TryMarkMapReady()
        {
            if (_mapReady || _map?.Core == null)
                return;

            if (_map.Core.IsStarted)
            {
                _mapReady = true;
                ApplyVisibility(_visibility);
                ScheduleRefresh(forceImmediate: true);
            }
        }

        public void ApplyAllSettings(FlightMapVisibility visibility)
        {
            if (_map == null)
                return;

            _visibility = visibility ?? _visibility ?? new FlightMapVisibility();
            ApplyVisibility(_visibility);

            if (_mapReady)
                ScheduleRefresh(forceImmediate: true);
        }

        public void ApplyVisibility(FlightMapVisibility visibility)
        {
            visibility = visibility ?? new FlightMapVisibility();
            if (_stack.Tfr != null)
                _stack.Tfr.Enabled = visibility.Tfr;
            if (_stack.Airspace != null)
                _stack.Airspace.Enabled = visibility.Airspace;
            if (_stack.Uas != null)
                _stack.Uas.Enabled = visibility.UasFacility;
            if (_stack.Notam != null)
                _stack.Notam.Enabled = visibility.Notams;
            if (_stack.SpecialUse != null)
                _stack.SpecialUse.Enabled = visibility.SpecialUse;
            if (_stack.Metar != null)
                _stack.Metar.Enabled = visibility.Metar;
            if (_stack.Sigmet != null)
                _stack.Sigmet.Enabled = visibility.Sigmet;
            if (_stack.Gairmet != null)
                _stack.Gairmet.Enabled = visibility.Gairmet;
            if (_stack.Pirep != null)
                _stack.Pirep.Enabled = visibility.Pirep;
            if (_stack.FlightSafety != null)
                _stack.FlightSafety.Enabled = visibility.FlightSafety;
        }

        public void ScheduleRefresh(bool forceImmediate = false)
        {
            if (!_mapReady || _map?.ViewArea == null || _map.ViewArea.IsEmpty)
                return;

            if (forceImmediate)
            {
                _debounce.Stop();
                RefreshNow();
                return;
            }

            _debounce.Stop();
            _debounce.Start();
        }

        void RefreshNow()
        {
            if (_map?.ViewArea == null || _map.ViewArea.IsEmpty)
                return;

            _refreshCts?.Cancel();
            _refreshCts = new CancellationTokenSource();
            var token = _refreshCts.Token;

            var radiusKm = Settings.Instance.GetDouble("aviationFetchRadiusKm", AviationRefreshPolicy.FetchRadiusKm);
            var center = ResolveFetchCenter();
            var zone = AviationBounds.NormalizeForFetch(_map.ViewArea, center, _map.Zoom, radiusKm);
            var fetchBounds = AviationBounds.InflateFetchBounds(zone);
            ApplyDisplayZone(zone);

            Task.Run(async () =>
            {
                try
                {
                    await FetchAndApplyAsync(fetchBounds, zone, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
            }, token);
        }

        void ApplyDisplayZone(RectLatLng zone)
        {
            void SetZone(GMapOverlay overlay)
            {
                if (overlay is RestrictionMapOverlay r)
                    r.DisplayZone = zone;
            }

            SetZone(_stack.Tfr?.MapOverlay);
            SetZone(_stack.Airspace?.MapOverlay);
            SetZone(_stack.Uas?.MapOverlay);
            SetZone(_stack.Notam?.MapOverlay);
            SetZone(_stack.SpecialUse?.MapOverlay);
            SetZone(_stack.Metar?.MapOverlay);
            SetZone(_stack.Sigmet?.MapOverlay);
            SetZone(_stack.Gairmet?.MapOverlay);
            SetZone(_stack.Pirep?.MapOverlay);
            SetZone(_stack.FlightSafety?.MapOverlay);
        }

        /// <summary>Prefer vehicle GPS, then home, then map center — keeps fetches local.</summary>
        PointLatLng ResolveFetchCenter()
        {
            try
            {
                var cs = MainV2.comPort?.MAV?.cs;
                if (cs != null && cs.connected && cs.gpsstatus >= 2 &&
                    !(Math.Abs(cs.lat) < 1e-6 && Math.Abs(cs.lng) < 1e-6))
                    return new PointLatLng(cs.lat, cs.lng);

                if (cs != null && cs.Base != null && cs.Base != PointLatLngAlt.Zero)
                    return cs.Base;
            }
            catch
            {
            }

            return _map?.Position ?? PointLatLng.Empty;
        }

        async Task FetchAndApplyAsync(RectLatLng fetchBounds, RectLatLng displayZone, CancellationToken token)
        {
            var summary = new RestrictionBriefingSummary();
            TfrFetchResult tfrResult = TfrFetchResult.Empty;
            LayerSnapshot tfrSnap = null;
            PolygonFetchResult special = PolygonFetchResult.Empty;
            PolygonFetchResult sigmet = PolygonFetchResult.Empty;

            var wantTfr = _stack.Tfr?.Enabled == true || _stack.Notam?.Enabled == true || _context.LoadBriefing;
            var wantAirspace = _stack.Airspace?.Enabled == true;
            var wantUas = _stack.Uas?.Enabled == true;
            var wantSpecial = _stack.SpecialUse?.Enabled == true;
            var wantMetar = _stack.Metar?.Enabled == true;
            var wantSigmet = _stack.Sigmet?.Enabled == true;
            var wantGairmet = _stack.Gairmet?.Enabled == true;
            var wantPirep = _stack.Pirep?.Enabled == true;

            var tfrTask = wantTfr ? _tfrProvider.FetchAsync(fetchBounds, token) : null;
            var airspaceTask = wantAirspace ? _airspaceProvider.FetchAsync(fetchBounds, token) : null;
            var uasTask = wantUas ? _uasProvider.FetchAsync(fetchBounds, token) : null;
            var specialTask = wantSpecial ? _specialProvider.FetchAsync(fetchBounds, token) : null;
            var metarTask = wantMetar ? _metarProvider.FetchAsync(fetchBounds, token) : null;
            var sigmetTask = wantSigmet ? _sigmetProvider.FetchAsync(fetchBounds, token) : null;
            var gairmetTask = wantGairmet ? _gairmetProvider.FetchAsync(fetchBounds, token) : null;
            var pirepTask = wantPirep ? _pirepProvider.FetchAsync(fetchBounds, token) : null;

            var wait = new List<Task>();
            if (tfrTask != null) wait.Add(tfrTask);
            if (airspaceTask != null) wait.Add(airspaceTask);
            if (uasTask != null) wait.Add(uasTask);
            if (specialTask != null) wait.Add(specialTask);
            if (metarTask != null) wait.Add(metarTask);
            if (sigmetTask != null) wait.Add(sigmetTask);
            if (gairmetTask != null) wait.Add(gairmetTask);
            if (pirepTask != null) wait.Add(pirepTask);

            if (wait.Count > 0)
                await Task.WhenAll(wait).ConfigureAwait(false);

            token.ThrowIfCancellationRequested();

            PolygonFetchResult gairmet = PolygonFetchResult.Empty;
            PolygonFetchResult uas = PolygonFetchResult.Empty;
            List<MetarObservation> metars = null;
            List<PirepObservation> pireps = null;
            List<FlightSafetyWarning> safetyWarnings = null;
            var zone = displayZone.IsEmpty ? fetchBounds : displayZone;

            if (tfrTask != null)
            {
                var (data, snap) = tfrTask.Result;
                var polys = AviationBounds.FilterPolygonsInBounds(data.Polygons, zone);
                var briefingItems = data.Briefing == null
                    ? new List<NotamBriefingItem>()
                    : data.Briefing.Where(b => BriefingInZone(b, zone)).ToList();
                tfrResult = new TfrFetchResult
                {
                    Json = data.Json,
                    Polygons = polys,
                    Briefing = briefingItems
                };
                tfrSnap = snap;
                summary.TfrNotamCount = polys.Count;
            }

            if (airspaceTask != null)
            {
                var (data, snap) = airspaceTask.Result;
                var polys = AviationBounds.FilterPolygonsInBounds(data.Polygons, zone);
                _stack.Airspace.SetFeatures(polys, snap);
            }

            if (uasTask != null)
            {
                var (data, snap) = uasTask.Result;
                var polys = AviationBounds.FilterPolygonsInBounds(data.Polygons, zone);
                uas = new PolygonFetchResult { Polygons = polys };
                summary.UasGridCells = polys.Count;
                _stack.Uas.SetFeatures(polys, snap);
            }

            if (specialTask != null)
            {
                var (data, snap) = specialTask.Result;
                var polys = AviationBounds.FilterPolygonsInBounds(data.Polygons, zone);
                special = new PolygonFetchResult { Polygons = polys };
                summary.SpecialUseAreas = polys.Count;
                _stack.SpecialUse.SetFeatures(polys, snap);
            }

            if (metarTask != null)
            {
                var (data, snap) = metarTask.Result;
                metars = data;
                summary.MetarCount = data.Count;
                _stack.Metar.SetMetars(data, snap);
            }

            if (sigmetTask != null)
            {
                var (data, snap) = sigmetTask.Result;
                var polys = AviationBounds.FilterPolygonsInBounds(data.Polygons, zone);
                sigmet = new PolygonFetchResult { Polygons = polys };
                summary.SigmetCount = polys.Count;
                _stack.Sigmet.SetFeatures(polys, snap);
            }

            if (gairmetTask != null)
            {
                var (data, snap) = gairmetTask.Result;
                var polys = AviationBounds.FilterPolygonsInBounds(data.Polygons, zone);
                gairmet = new PolygonFetchResult { Polygons = polys };
                summary.GairmetCount = polys.Count;
                _stack.Gairmet.SetFeatures(polys, snap);
            }

            if (pirepTask != null)
            {
                var (data, snap) = pirepTask.Result;
                pireps = data;
                summary.PirepCount = data.Count;
                _stack.Pirep.SetReports(data, snap);
            }

            if (_stack.Tfr?.Enabled == true)
                _stack.Tfr.SetFetchResult(tfrResult, tfrSnap);

            if (_stack.Notam?.Enabled == true)
            {
                var notices = AviationGeoJson.ToAviationNotices(tfrResult.Polygons);
                _stack.Notam.SetNotices(notices,
                    tfrSnap ?? new LayerSnapshot { LayerId = "notam", DisplayName = "NOTAMs" });
            }

            if (_stack.FlightSafety?.Enabled == true)
            {
                var missionPts = _context.GetMissionPoints?.Invoke()?.ToList() ?? new List<PointLatLng>();
                safetyWarnings = FlightSafetyAnalyzer.AnalyzeMission(
                    missionPts,
                    tfrResult.Polygons,
                    special.Polygons,
                    sigmet.Polygons);
                summary.FlightSafetyWarnings = safetyWarnings.Count;
                _stack.FlightSafety.SetWarnings(safetyWarnings, new LayerSnapshot
                {
                    LayerId = "flightsafety",
                    DisplayName = "Flight safety",
                    Health = safetyWarnings.Count > 0 ? LayerHealthState.Ok : LayerHealthState.Empty,
                    FeatureCount = safetyWarnings.Count,
                    FetchedAtUtc = DateTime.UtcNow
                });
            }

            List<NotamBriefingItem> briefing = null;
            if (_context.BriefingUpdated != null)
            {
                briefing = BriefingListBuilder.Build(
                    ResolveFetchCenter(),
                    tfrResult.Briefing,
                    _stack.Sigmet?.Enabled == true ? sigmet.Polygons : null,
                    _stack.Gairmet?.Enabled == true ? gairmet.Polygons : null,
                    _stack.SpecialUse?.Enabled == true ? special.Polygons : null,
                    _stack.Uas?.Enabled == true ? uas.Polygons : null,
                    _stack.Metar?.Enabled == true ? metars : null,
                    _stack.Pirep?.Enabled == true ? pireps : null,
                    _stack.FlightSafety?.Enabled == true ? safetyWarnings : null);
            }

            _context.InvokeOnUi(() =>
            {
                if (_map == null)
                    return;

                try
                {
                    ApplyVisibility(_visibility);

                    foreach (var layer in _stack.All)
                    {
                        if (layer == null)
                            continue;

                        try
                        {
                            if (!layer.Enabled)
                                layer.ClearMap();
                            else
                                layer.ApplyToMap();
                        }
                        catch (Exception ex)
                        {
                            Log.Error("Aviation layer apply failed: " + layer.Id, ex);
                        }
                    }

                    _context.BringRestrictionOverlaysToFront?.Invoke();
                    _map.Invalidate();

                    if (briefing != null)
                        _context.BriefingUpdated?.Invoke(briefing);
                    _context.SummaryUpdated?.Invoke(summary);
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
            });
        }

        static bool BriefingInZone(NotamBriefingItem item, RectLatLng zone)
        {
            if (item == null || zone.IsEmpty)
                return true;
            if (item.Center.IsEmpty)
                return true;
            return zone.Contains(item.Center);
        }

        void DetachHandlers()
        {
            if (_map == null)
                return;

            _map.OnMapZoomChanged -= OnViewChanged;
            _map.OnPositionChanged -= OnPositionChanged;
            _map.Load -= OnMapLoad;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _refreshCts?.Cancel();
            _debounce?.Stop();
            _debounce?.Dispose();
            _zoomDebounce?.Stop();
            _zoomDebounce?.Dispose();
            DetachHandlers();
            _map = null;
        }
    }
}
