using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http;
using GMap.NET;
using log4net;

namespace MissionPlanner.Utilities.AviationLayers
{
    public sealed class AwcGairmetDataProvider : IAwcGairmetProvider
    {
        static readonly ILog Log = LogManager.GetLogger(typeof(AwcGairmetDataProvider));
        readonly AviationDataCache<PolygonFetchResult> _cache = new AviationDataCache<PolygonFetchResult>();

        public string SourceName => AviationFetch.GairmetApiBase;
        public TimeSpan CacheTtl { get; set; } = TimeSpan.FromMinutes(1);

        public async Task<(PolygonFetchResult Data, LayerSnapshot Snapshot)> FetchAsync(RectLatLng fetchBounds,
            CancellationToken cancel)
        {
            var key = AviationDataCache<PolygonFetchResult>.BoundsKey(
                fetchBounds.Left, fetchBounds.Bottom, fetchBounds.Right, fetchBounds.Top);
            _cache.TimeToLive = CacheTtl;

            if (_cache.TryGet(key, out var cached, out var fetchedUtc))
                return (cached, Snap(cached.Polygons.Count, fetchedUtc));

            try
            {
                var url = AviationFetch.BuildGairmetUrl(fetchBounds);
                var json = await url.GetStringAsync(cancellationToken: cancel).ConfigureAwait(false);
                var polygons = AviationGeoJson.ParseGairmetFeatures(json);
                var result = new PolygonFetchResult { Polygons = polygons };
                _cache.Set(key, result);
                return (result, Snap(polygons.Count, DateTime.UtcNow));
            }
            catch (Exception ex) when (!AviationLayerErrors.IsBenignCancel(ex))
            {
                Log.Warn("G-AIRMET fetch failed", ex);
                return (PolygonFetchResult.Empty, new LayerSnapshot
                {
                    LayerId = AviationLayerIds.Gairmet,
                    DisplayName = "G-AIRMET",
                    Health = LayerHealthState.Error,
                    Source = SourceName,
                    Message = ex.Message
                });
            }
        }

        static LayerSnapshot Snap(int count, DateTime fetchedUtc) =>
            new LayerSnapshot
            {
                LayerId = AviationLayerIds.Gairmet,
                DisplayName = "G-AIRMET",
                Health = count > 0 ? LayerHealthState.Ok : LayerHealthState.Empty,
                FetchedAtUtc = fetchedUtc,
                Source = AviationFetch.GairmetApiBase,
                FeatureCount = count
            };
    }

    public sealed class AwcPirepDataProvider : IAwcPirepProvider
    {
        static readonly ILog Log = LogManager.GetLogger(typeof(AwcPirepDataProvider));
        readonly AviationDataCache<List<PirepObservation>> _cache = new AviationDataCache<List<PirepObservation>>();

        public string SourceName => AviationFetch.PirepApiBase;
        public TimeSpan CacheTtl { get; set; } = TimeSpan.FromMinutes(1);

        public async Task<(List<PirepObservation> Data, LayerSnapshot Snapshot)> FetchAsync(RectLatLng fetchBounds,
            CancellationToken cancel)
        {
            var key = AviationDataCache<PolygonFetchResult>.BoundsKey(
                fetchBounds.Left, fetchBounds.Bottom, fetchBounds.Right, fetchBounds.Top);
            _cache.TimeToLive = CacheTtl;

            if (_cache.TryGet(key, out var cached, out var fetchedUtc))
                return (cached, Snap(cached, fetchedUtc));

            try
            {
                var url = AviationFetch.BuildPirepUrl(fetchBounds);
                var json = await url.GetStringAsync(cancellationToken: cancel).ConfigureAwait(false);
                var list = AviationGeoJson.ParsePirepGeoJson(json);
                _cache.Set(key, list);
                return (list, Snap(list, DateTime.UtcNow));
            }
            catch (Exception ex) when (!AviationLayerErrors.IsBenignCancel(ex))
            {
                Log.Warn("PIREP fetch failed", ex);
                return (new List<PirepObservation>(), new LayerSnapshot
                {
                    LayerId = AviationLayerIds.Pirep,
                    DisplayName = "PIREP",
                    Health = LayerHealthState.Error,
                    Source = SourceName,
                    Message = ex.Message
                });
            }
        }

        static LayerSnapshot Snap(List<PirepObservation> list, DateTime fetchedUtc) =>
            new LayerSnapshot
            {
                LayerId = AviationLayerIds.Pirep,
                DisplayName = "PIREP",
                Health = list.Count > 0 ? LayerHealthState.Ok : LayerHealthState.Empty,
                FetchedAtUtc = fetchedUtc,
                Source = AviationFetch.PirepApiBase,
                FeatureCount = list.Count
            };
    }
}
