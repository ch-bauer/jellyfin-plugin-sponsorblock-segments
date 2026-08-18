using Jellyfin.Plugin.SponsorBlockSegments.Configuration;
using Jellyfin.Plugin.SponsorBlockSegments.Mapping;
using Jellyfin.Plugin.SponsorBlockSegments.Scope;
using Jellyfin.Plugin.SponsorBlockSegments.Sources;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.MediaSegments;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model;
using MediaBrowser.Model.MediaSegments;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SponsorBlockSegments.Providers;

/// <summary>
/// Turns SponsorBlock data into Jellyfin media segments.
/// </summary>
/// <remarks>
/// Every segment found is emitted. That is the difference from the chapter analyzer in
/// Intro Skipper, which returns a single segment per analysis mode per episode and drops a
/// match whose neighbour matches the same mode - fine for a television episode with one
/// intro and one credits roll, but it cannot represent a video carrying seven interspersed
/// sponsor reads.
/// <para>
/// Categories are matched on the whole label rather than by substring, so a
/// "Preview/Recap" chapter cannot be read as a sponsor merely because every one of these
/// chapters is prefixed "[SponsorBlock]:".
/// </para>
/// </remarks>
public sealed class SponsorBlockSegmentProvider : IMediaSegmentProvider
{
    private readonly IItemRepository _itemRepository;
    private readonly ScopeResolver _scope;
    private readonly ChapterSegmentSource _chapters;
    private readonly ApiSegmentSource _api;
    private readonly VideoIdExtractor _videoIds;
    private readonly CategoryMap _categories;
    private readonly ILogger<SponsorBlockSegmentProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SponsorBlockSegmentProvider"/> class.
    /// </summary>
    /// <param name="itemRepository">Instance of the <see cref="IItemRepository"/> interface.</param>
    /// <param name="scope">The scope resolver.</param>
    /// <param name="chapters">The chapter source.</param>
    /// <param name="api">The API source.</param>
    /// <param name="videoIds">The video id extractor.</param>
    /// <param name="categories">The category mapping.</param>
    /// <param name="logger">The logger.</param>
    public SponsorBlockSegmentProvider(
        IItemRepository itemRepository,
        ScopeResolver scope,
        ChapterSegmentSource chapters,
        ApiSegmentSource api,
        VideoIdExtractor videoIds,
        CategoryMap categories,
        ILogger<SponsorBlockSegmentProvider> logger)
    {
        _itemRepository = itemRepository;
        _scope = scope;
        _chapters = chapters;
        _api = api;
        _videoIds = videoIds;
        _categories = categories;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "SponsorBlock Segments";

    /// <inheritdoc />
    public ValueTask<bool> Supports(BaseItem item)
    {
        // Called for every item the scan considers, so this stays a pure in-memory check:
        // no chapter read, no file access, no network.
        if (item is not IHasMediaSources)
        {
            return ValueTask.FromResult(false);
        }

        return ValueTask.FromResult(_scope.IsInScope(item));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MediaSegmentDto>> GetMediaSegments(
        MediaSegmentGenerationRequest request,
        CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || !config.Enabled)
        {
            return Array.Empty<MediaSegmentDto>();
        }

        var item = _itemRepository.RetrieveItem(request.ItemId);
        if (item is null || !_scope.IsInScope(item))
        {
            return Array.Empty<MediaSegmentDto>();
        }

        var raw = await CollectAsync(item, config, cancellationToken).ConfigureAwait(false);
        if (raw.Count == 0)
        {
            return Array.Empty<MediaSegmentDto>();
        }

        var minimumTicks = (long)(Math.Max(0, config.MinimumSegmentSeconds) * TimeSpan.TicksPerSecond);
        var segments = new List<MediaSegmentDto>(raw.Count);

        foreach (var candidate in raw)
        {
            if (!candidate.IsValid || candidate.EndTicks - candidate.StartTicks < minimumTicks)
            {
                continue;
            }

            var type = _categories.TypeFor(candidate.Category);
            if (type is null)
            {
                continue;
            }

            if (config.SkipOverlappingExisting && OverlapsExisting(request, candidate))
            {
                continue;
            }

            segments.Add(new MediaSegmentDto
            {
                Id = Guid.NewGuid(),
                ItemId = item.Id,
                Type = type.Value,
                StartTicks = candidate.StartTicks,
                EndTicks = candidate.EndTicks
            });
        }

        _logger.LogDebug(
            "SponsorBlock Segments produced {Count} segment(s) for {Path}",
            segments.Count,
            item.Path);

        return segments;
    }

    private async Task<IReadOnlyList<RawSegment>> CollectAsync(
        BaseItem item,
        PluginConfiguration config,
        CancellationToken cancellationToken)
    {
        if (config.SourceMode != SegmentSourceMode.ApiOnly)
        {
            var fromChapters = _chapters.GetSegments(item);
            if (fromChapters.Count > 0)
            {
                return fromChapters;
            }

            if (config.SourceMode == SegmentSourceMode.ChaptersOnly)
            {
                return Array.Empty<RawSegment>();
            }
        }

        if (!_videoIds.TryExtract(item.Path, config.VideoIdPattern, out var videoId))
        {
            _logger.LogDebug("No video id in {Path}, so the API cannot be asked", item.Path);
            return Array.Empty<RawSegment>();
        }

        return await _api.GetSegmentsAsync(videoId, cancellationToken).ConfigureAwait(false);
    }

    private static bool OverlapsExisting(MediaSegmentGenerationRequest request, RawSegment candidate)
    {
        foreach (var existing in request.ExistingSegments)
        {
            if (candidate.StartTicks < existing.EndTicks && existing.StartTicks < candidate.EndTicks)
            {
                return true;
            }
        }

        return false;
    }
}
