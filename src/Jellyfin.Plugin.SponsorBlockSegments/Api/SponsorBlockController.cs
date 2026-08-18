using Jellyfin.Data.Enums;
using Jellyfin.Plugin.SponsorBlockSegments.Configuration;
using Jellyfin.Plugin.SponsorBlockSegments.Mapping;
using Jellyfin.Plugin.SponsorBlockSegments.Providers;
using Jellyfin.Plugin.SponsorBlockSegments.Scope;
using Jellyfin.Plugin.SponsorBlockSegments.Sources;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.MediaSegments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.SponsorBlockSegments.Api;

/// <summary>
/// Backs the configuration page: browsing the library tree to pick what gets scanned, and
/// previewing what an item would produce before a scan is run.
/// </summary>
[ApiController]
[Route("SponsorBlockSegments")]
[Authorize(Policy = "RequiresElevation")]
public class SponsorBlockController : ControllerBase
{
    private readonly ILibraryManager _libraryManager;
    private readonly SponsorBlockSegmentProvider _provider;
    private readonly ChapterSegmentSource _chapters;
    private readonly ApiSegmentSource _api;
    private readonly VideoIdExtractor _videoIds;
    private readonly CategoryMap _categories;
    private readonly ScopeResolver _scope;
    private readonly SegmentCache _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="SponsorBlockController"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="provider">The segment provider.</param>
    /// <param name="chapters">The chapter source.</param>
    /// <param name="api">The API source.</param>
    /// <param name="videoIds">The video id extractor.</param>
    /// <param name="categories">The category mapping.</param>
    /// <param name="scope">The scope resolver.</param>
    /// <param name="cache">The API cache.</param>
    public SponsorBlockController(
        ILibraryManager libraryManager,
        SponsorBlockSegmentProvider provider,
        ChapterSegmentSource chapters,
        ApiSegmentSource api,
        VideoIdExtractor videoIds,
        CategoryMap categories,
        ScopeResolver scope,
        SegmentCache cache)
    {
        _libraryManager = libraryManager;
        _provider = provider;
        _chapters = chapters;
        _api = api;
        _videoIds = videoIds;
        _categories = categories;
        _scope = scope;
        _cache = cache;
    }

    /// <summary>
    /// A library, series or season offered in the scope picker.
    /// </summary>
    public class TreeNode
    {
        /// <summary>
        /// Gets or sets the item id.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether this node is already opted in.
        /// </summary>
        public bool Selected { get; set; }

        /// <summary>
        /// Gets or sets how many children it has, for display.
        /// </summary>
        public int ChildCount { get; set; }
    }

    /// <summary>
    /// One row of the preview: what a chapter would become.
    /// </summary>
    public class PreviewRow
    {
        /// <summary>
        /// Gets or sets the start position, in seconds.
        /// </summary>
        public double Start { get; set; }

        /// <summary>
        /// Gets or sets the end position, in seconds.
        /// </summary>
        public double End { get; set; }

        /// <summary>
        /// Gets or sets the SponsorBlock category.
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Jellyfin segment type it maps to, or "ignored".
        /// </summary>
        public string SegmentType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets where it was read from.
        /// </summary>
        public string Origin { get; set; } = string.Empty;
    }

    /// <summary>
    /// The libraries on the server.
    /// </summary>
    /// <returns>The libraries.</returns>
    [HttpGet("Libraries")]
    public ActionResult<IEnumerable<TreeNode>> GetLibraries()
    {
        var selected = SelectedIds(ScopeKind.Library);

        var nodes = _libraryManager.GetVirtualFolders()
            .Select(f => new TreeNode
            {
                Id = f.ItemId,
                Name = f.Name,
                Selected = Guid.TryParse(f.ItemId, out var id) && selected.Contains(id)
            })
            .OrderBy(n => n.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return Ok(nodes);
    }

    /// <summary>
    /// The series in a library.
    /// </summary>
    /// <param name="libraryId">The library id.</param>
    /// <returns>The series.</returns>
    [HttpGet("Series")]
    public ActionResult<IEnumerable<TreeNode>> GetSeries([FromQuery] Guid libraryId)
    {
        var parent = _libraryManager.GetItemById(libraryId);
        if (parent is null)
        {
            return NotFound();
        }

        var selected = SelectedIds(ScopeKind.Series);

        var nodes = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Series },
            Recursive = true,
            Parent = parent
        })
        .Select(s => new TreeNode
        {
            Id = s.Id.ToString(),
            Name = s.Name,
            Selected = selected.Contains(s.Id)
        })
        .OrderBy(n => n.Name, StringComparer.CurrentCultureIgnoreCase)
        .ToList();

        return Ok(nodes);
    }

    /// <summary>
    /// The seasons of a series.
    /// </summary>
    /// <param name="seriesId">The series id.</param>
    /// <returns>The seasons.</returns>
    [HttpGet("Seasons")]
    public ActionResult<IEnumerable<TreeNode>> GetSeasons([FromQuery] Guid seriesId)
    {
        var parent = _libraryManager.GetItemById(seriesId);
        if (parent is null)
        {
            return NotFound();
        }

        var selected = SelectedIds(ScopeKind.Season);

        var nodes = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Season },
            Recursive = true,
            Parent = parent
        })
        .Select(s => new TreeNode
        {
            Id = s.Id.ToString(),
            Name = s.Name,
            Selected = selected.Contains(s.Id)
        })
        .OrderBy(n => n.Name, StringComparer.CurrentCultureIgnoreCase)
        .ToList();

        return Ok(nodes);
    }

    /// <summary>
    /// The episodes of a series or season, for the preview picker.
    /// </summary>
    /// <param name="parentId">The series or season id.</param>
    /// <returns>The episodes.</returns>
    [HttpGet("Episodes")]
    public ActionResult<IEnumerable<TreeNode>> GetEpisodes([FromQuery] Guid parentId)
    {
        var parent = _libraryManager.GetItemById(parentId);
        if (parent is null)
        {
            return NotFound();
        }

        var nodes = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Episode },
            Recursive = true,
            Parent = parent
        })
        .Select(e => new TreeNode
        {
            Id = e.Id.ToString(),
            Name = Label(e)
        })
        .ToList();

        return Ok(nodes);
    }

    private static string Label(BaseItem item)
    {
        if (item is MediaBrowser.Controller.Entities.TV.Episode ep
            && ep.ParentIndexNumber.HasValue && ep.IndexNumber.HasValue)
        {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "S{0:D2}E{1:D2} - {2}",
                ep.ParentIndexNumber.Value,
                ep.IndexNumber.Value,
                ep.Name);
        }

        return item.Name;
    }

    /// <summary>
    /// Adds a library, series or season to the allowlist.
    /// </summary>
    /// <param name="itemId">The item.</param>
    /// <param name="kind">What kind of item it is.</param>
    /// <returns>No content.</returns>
    [HttpPost("Scope")]
    public ActionResult AddScope([FromQuery] Guid itemId, [FromQuery] ScopeKind kind)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            return NotFound();
        }

        if (config.Scope.Any(e => e.ItemId == itemId))
        {
            return NoContent();
        }

        var item = _libraryManager.GetItemById(itemId);

        config.Scope.Add(new ScopeEntry
        {
            ItemId = itemId,
            Kind = kind,
            Name = item?.Name,
            ParentName = (item as MediaBrowser.Controller.Entities.TV.Season)?.SeriesName
        });

        Save();
        return NoContent();
    }

    /// <summary>
    /// Removes an entry from the allowlist.
    /// </summary>
    /// <param name="itemId">The item.</param>
    /// <returns>No content.</returns>
    [HttpDelete("Scope")]
    public ActionResult RemoveScope([FromQuery] Guid itemId)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            return NotFound();
        }

        foreach (var entry in config.Scope.Where(e => e.ItemId == itemId).ToList())
        {
            config.Scope.Remove(entry);
        }

        Save();
        return NoContent();
    }

    /// <summary>
    /// What this item would produce, without running a scan.
    /// </summary>
    /// <param name="itemId">The item.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>One row per SponsorBlock chapter found.</returns>
    [HttpGet("Preview")]
    public async Task<ActionResult<IEnumerable<PreviewRow>>> Preview(
        [FromQuery] Guid itemId,
        CancellationToken cancellationToken)
    {
        var item = _libraryManager.GetItemById(itemId);
        if (item is null)
        {
            return NotFound();
        }

        // Deliberately resolves the sources directly rather than going through the
        // provider, so the preview still works for an item that has not been opted in yet
        // - which is the whole point of looking before committing. The order matches what
        // the provider would do, so what is shown is what a scan would store.
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        IReadOnlyList<Sources.RawSegment> raw = Array.Empty<Sources.RawSegment>();

        if (config.SourceMode != SegmentSourceMode.ApiOnly)
        {
            raw = _chapters.GetSegments(item);
        }

        if (raw.Count == 0 && config.SourceMode != SegmentSourceMode.ChaptersOnly
            && _videoIds.TryExtract(item.Path, config.VideoIdPattern, out var videoId))
        {
            raw = await _api.GetSegmentsAsync(videoId, cancellationToken).ConfigureAwait(false);
        }

        var minimumTicks = (long)(Math.Max(0, config.MinimumSegmentSeconds) * TimeSpan.TicksPerSecond);

        var rows = raw.Select(r => new PreviewRow
        {
            Start = TimeSpan.FromTicks(r.StartTicks).TotalSeconds,
            End = TimeSpan.FromTicks(r.EndTicks).TotalSeconds,
            Category = r.Category.ToString(),
            SegmentType = r.EndTicks - r.StartTicks < minimumTicks
                ? "skipped (too short)"
                : _categories.TypeFor(r.Category)?.ToString() ?? "ignored",
            Origin = r.Origin.ToString()
        }).ToList();

        return Ok(rows);
    }

    /// <summary>
    /// Empties the API response cache.
    /// </summary>
    /// <returns>No content.</returns>
    [HttpPost("ClearCache")]
    public ActionResult ClearCache()
    {
        _cache.Clear();
        return NoContent();
    }

    private HashSet<Guid> SelectedIds(ScopeKind kind)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            return new HashSet<Guid>();
        }

        return config.Scope.Where(e => e.Kind == kind).Select(e => e.ItemId).ToHashSet();
    }

    private void Save()
    {
        Plugin.Instance?.SaveConfiguration();
        _scope.Invalidate();
        _categories.Invalidate();
    }
}
