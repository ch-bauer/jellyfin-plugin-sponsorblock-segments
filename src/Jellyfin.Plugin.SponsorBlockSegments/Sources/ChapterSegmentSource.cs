using Jellyfin.Plugin.SponsorBlockSegments.Mapping;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SponsorBlockSegments.Sources;

/// <summary>
/// Reads segments from the <c>[SponsorBlock]:</c> chapters yt-dlp embeds in the file.
/// </summary>
/// <remarks>
/// Jellyfin's <see cref="ChapterInfo"/> carries only a start position - there is no end
/// time in the database - so each segment ends where the next chapter begins, and the last
/// chapter ends at the item's runtime. That is why the filler chapters yt-dlp writes
/// between marked segments matter: they are what terminates the segment before them.
/// Deleting them would stretch a segment to the start of the next marked one.
/// </remarks>
public sealed class ChapterSegmentSource
{
    private readonly IChapterRepository _chapterRepository;
    private readonly ILogger<ChapterSegmentSource> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChapterSegmentSource"/> class.
    /// </summary>
    /// <param name="chapterRepository">Instance of the <see cref="IChapterRepository"/> interface.</param>
    /// <param name="logger">The logger.</param>
    public ChapterSegmentSource(
        IChapterRepository chapterRepository,
        ILogger<ChapterSegmentSource> logger)
    {
        _chapterRepository = chapterRepository;
        _logger = logger;
    }

    /// <summary>
    /// Reads every SponsorBlock chapter of an item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The segments found, in chapter order.</returns>
    public IReadOnlyList<RawSegment> GetSegments(BaseItem item)
    {
        IReadOnlyList<ChapterInfo> chapters;
        try
        {
            chapters = _chapterRepository.GetChapters(item.Id);
        }
        catch (Exception ex)
        {
            // One unreadable item must not fail the whole scan.
            _logger.LogDebug(ex, "Could not read chapters for {Path}", item.Path);
            return Array.Empty<RawSegment>();
        }

        if (chapters.Count == 0)
        {
            return Array.Empty<RawSegment>();
        }

        var runtimeTicks = item.RunTimeTicks ?? 0;
        var segments = new List<RawSegment>(chapters.Count);

        for (var i = 0; i < chapters.Count; i++)
        {
            var chapter = chapters[i];

            if (!ChapterLabelParser.TryParseChapter(chapter.Name, out var category))
            {
                continue;
            }

            var start = chapter.StartPositionTicks;

            // The next chapter's start is the only end time available. When this is the
            // last chapter, fall back to the runtime; if that is unknown too there is
            // nothing sensible to use and the segment is dropped rather than guessed.
            long end;
            if (i + 1 < chapters.Count)
            {
                end = chapters[i + 1].StartPositionTicks;
            }
            else if (runtimeTicks > start)
            {
                end = runtimeTicks;
            }
            else
            {
                _logger.LogDebug(
                    "Dropping trailing chapter {Name} of {Path}: no next chapter and no runtime to end it at",
                    chapter.Name,
                    item.Path);
                continue;
            }

            var segment = new RawSegment(category.Value, start, end, SegmentOrigin.Chapter);
            if (segment.IsValid)
            {
                segments.Add(segment);
            }
        }

        return segments;
    }

    /// <summary>
    /// True when the item has at least one SponsorBlock chapter, so the caller knows
    /// whether it needs to fall back to the API.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>Whether any SponsorBlock chapter is present.</returns>
    public bool HasSponsorBlockChapters(BaseItem item)
    {
        try
        {
            return _chapterRepository.GetChapters(item.Id)
                .Any(c => ChapterLabelParser.IsSponsorBlockChapter(c.Name));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read chapters for {Path}", item.Path);
            return false;
        }
    }
}
