using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GMap.NET;

namespace MissionPlanner.Utilities.AviationLayers
{
    /// <summary>Provider → cache → layer. Map code must not call HTTP directly.</summary>
    public interface IFaaTfrProvider : IAviationDataProvider<TfrFetchResult>
    {
    }

    /// <summary>FAA NOTAM Management Service (future); graphic NOTAMs today come from TFR WFS.</summary>
    public interface IFaaNotamProvider : IAviationDataProvider<List<AviationNotice>>
    {
    }

    /// <summary>FAA NASR local airspace (future); interim OpenAIP export.</summary>
    public interface IFaaNasrAirspaceProvider : IAviationDataProvider<PolygonFetchResult>
    {
    }

    public interface IAwcMetarProvider : IAviationDataProvider<List<MetarObservation>>
    {
    }

    public interface IAwcSigmetProvider : IAviationDataProvider<PolygonFetchResult>
    {
    }

    public interface IAwcGairmetProvider : IAviationDataProvider<PolygonFetchResult>
    {
    }

    public interface IAwcPirepProvider : IAviationDataProvider<List<PirepObservation>>
    {
    }

    public sealed class FaaTfrDataProvider : IFaaTfrProvider
    {
        readonly TfrDataProvider _inner = new TfrDataProvider();

        public string SourceName => _inner.SourceName;
        public TimeSpan CacheTtl { get => _inner.CacheTtl; set => _inner.CacheTtl = value; }

        public Task<(TfrFetchResult Data, LayerSnapshot Snapshot)> FetchAsync(RectLatLng fetchBounds,
            CancellationToken cancel) => _inner.FetchAsync(fetchBounds, cancel);
    }

    public sealed class AwcMetarDataProvider : IAwcMetarProvider
    {
        readonly MetarDataProvider _inner = new MetarDataProvider();

        public string SourceName => _inner.SourceName;
        public TimeSpan CacheTtl { get => _inner.CacheTtl; set => _inner.CacheTtl = value; }

        public Task<(List<MetarObservation> Data, LayerSnapshot Snapshot)> FetchAsync(RectLatLng fetchBounds,
            CancellationToken cancel) => _inner.FetchAsync(fetchBounds, cancel);
    }

    public sealed class AwcSigmetDataProvider : IAwcSigmetProvider
    {
        readonly SigmetDataProvider _inner = new SigmetDataProvider();

        public string SourceName => _inner.SourceName;
        public TimeSpan CacheTtl { get => _inner.CacheTtl; set => _inner.CacheTtl = value; }

        public Task<(PolygonFetchResult Data, LayerSnapshot Snapshot)> FetchAsync(RectLatLng fetchBounds,
            CancellationToken cancel) => _inner.FetchAsync(fetchBounds, cancel);
    }

    /// <summary>Graphic NOTAM polygons from FAA TFR GeoJSON until NMS API credentials are configured.</summary>
    public sealed class FaaGraphicNotamProvider : IFaaNotamProvider
    {
        readonly FaaTfrDataProvider _tfr = new FaaTfrDataProvider();

        public string SourceName => AviationFetch.TfrWfsUrl;
        public TimeSpan CacheTtl { get; set; } = TimeSpan.FromMinutes(3);

        public async Task<(List<AviationNotice> Data, LayerSnapshot Snapshot)> FetchAsync(RectLatLng fetchBounds,
            CancellationToken cancel)
        {
            var (tfr, snap) = await _tfr.FetchAsync(fetchBounds, cancel).ConfigureAwait(false);
            var notices = AviationGeoJson.ToAviationNotices(tfr.Polygons);
            var noticeSnap = new LayerSnapshot
            {
                LayerId = AviationLayerIds.Notam,
                DisplayName = "NOTAMs",
                Health = snap.Health,
                FetchedAtUtc = snap.FetchedAtUtc,
                ExpiresAtUtc = snap.ExpiresAtUtc,
                Source = SourceName + " (graphic)",
                FeatureCount = notices.Count,
                Message = snap.Message
            };
            return (notices, noticeSnap);
        }
    }

    /// <summary>Placeholder for FAA NOTAM Management Service REST/SWIM (access via FAA data portal).</summary>
    public sealed class FaaNotamNmsProvider : IFaaNotamProvider
    {
        public string SourceName => "FAA NOTAM Management Service (not configured)";
        public TimeSpan CacheTtl { get; set; } = TimeSpan.FromMinutes(2);

        public Task<(List<AviationNotice> Data, LayerSnapshot Snapshot)> FetchAsync(RectLatLng fetchBounds,
            CancellationToken cancel)
        {
            return Task.FromResult((new List<AviationNotice>(), new LayerSnapshot
            {
                LayerId = AviationLayerIds.Notam,
                DisplayName = "NOTAMs",
                Health = LayerHealthState.Empty,
                Source = SourceName,
                Message = "Configure FAA NMS credentials to enable live NOTAMs."
            }));
        }
    }

    /// <summary>NASR subscription local cache (shapefile/AIXM import planned).</summary>
    public sealed class FaaNasrAirspaceProvider : IFaaNasrAirspaceProvider
    {
        readonly AirspaceDataProvider _interim = new AirspaceDataProvider();

        public string SourceName => "FAA NASR (interim: OpenAIP export)";
        public TimeSpan CacheTtl { get; set; } = TimeSpan.FromDays(28);

        public Task<(PolygonFetchResult Data, LayerSnapshot Snapshot)> FetchAsync(RectLatLng fetchBounds,
            CancellationToken cancel) =>
            _interim.FetchAsync(fetchBounds, cancel);
    }
}
