using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.SponsorBlockSegments.Mapping;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SponsorBlockSegments.Sources;

/// <summary>
/// Fetches segments from sponsor.ajay.app for a YouTube video id.
/// </summary>
/// <remarks>
/// Used for items with no embedded SponsorBlock chapters. Unlike the chapter source this
/// gets exact start and end times, so API-sourced segments do not depend on the filler
/// chapters yt-dlp writes.
/// </remarks>
public sealed class ApiSegmentSource
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SegmentCache _cache;
    private readonly ILogger<ApiSegmentSource> _logger;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiSegmentSource"/> class.
    /// </summary>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    /// <param name="cache">The response cache.</param>
    /// <param name="logger">The logger.</param>
    public ApiSegmentSource(
        IHttpClientFactory httpClientFactory,
        SegmentCache cache,
        ILogger<ApiSegmentSource> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    private sealed class ApiSegment
    {
        [JsonPropertyName("segment")]
        public double[]? Segment { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }
    }

    /// <summary>
    /// Asks the API for the segments of one video.
    /// </summary>
    /// <param name="videoId">The YouTube video id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The segments, empty when the video has none or the request failed.</returns>
    public async Task<IReadOnlyList<RawSegment>> GetSegmentsAsync(
        string videoId,
        CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            return Array.Empty<RawSegment>();
        }

        if (_cache.TryGet(videoId, out var cached))
        {
            return cached;
        }

        var categories = JsonSerializer.Serialize(new[]
        {
            "sponsor", "selfpromo", "interaction", "intro", "outro",
            "preview", "filler", "music_offtopic", "poi_highlight", "hook"
        });

        var url = string.Format(
            CultureInfo.InvariantCulture,
            "{0}/api/skipSegments?videoID={1}&categories={2}",
            config.ApiBaseUrl.TrimEnd('/'),
            Uri.EscapeDataString(videoId),
            Uri.EscapeDataString(categories));

        try
        {
            var client = _httpClientFactory.CreateClient(NamedClient.Default);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, config.ApiTimeoutSeconds));

            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);

            // 404 is the documented answer for "nothing submitted for this video", not a
            // fault - it is cached like any other result, for longer.
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                Remember(videoId, Array.Empty<RawSegment>(), config.ApiNegativeCacheHours);
                return Array.Empty<RawSegment>();
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug(
                    "SponsorBlock API returned {Status} for {VideoId}",
                    (int)response.StatusCode,
                    videoId);
                return Array.Empty<RawSegment>();
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var parsed = JsonSerializer.Deserialize<ApiSegment[]>(body, _json) ?? Array.Empty<ApiSegment>();

            var segments = new List<RawSegment>(parsed.Length);
            foreach (var entry in parsed)
            {
                if (entry.Segment is not { Length: 2 } span)
                {
                    continue;
                }

                if (!ChapterLabelParser.TryParseLabel(entry.Category, out var category))
                {
                    continue;
                }

                var segment = new RawSegment(
                    category.Value,
                    (long)(span[0] * TimeSpan.TicksPerSecond),
                    (long)(span[1] * TimeSpan.TicksPerSecond),
                    SegmentOrigin.Api);

                if (segment.IsValid)
                {
                    segments.Add(segment);
                }
            }

            Remember(
                videoId,
                segments,
                segments.Count == 0 ? config.ApiNegativeCacheHours : config.ApiCacheHours);

            return segments;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A network problem is not cached: the next scan should try again rather than
            // remember an outage for three days.
            _logger.LogDebug(ex, "SponsorBlock API request failed for {VideoId}", videoId);
            return Array.Empty<RawSegment>();
        }
    }

    private void Remember(string videoId, IReadOnlyList<RawSegment> segments, int hours) =>
        _cache.Set(videoId, segments, TimeSpan.FromHours(Math.Max(0, hours)));
}
