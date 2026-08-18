using System.Collections.ObjectModel;
using Jellyfin.Plugin.SponsorBlockSegments.Mapping;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.SponsorBlockSegments.Configuration;

/// <summary>
/// What a SponsorBlock category becomes in Jellyfin. Mirrors
/// <c>MediaSegmentType</c>, plus a value for "produce nothing".
/// </summary>
public enum SegmentAction
{
    /// <summary>
    /// No segment. The chapter stays in the file and is simply not turned into
    /// anything skippable.
    /// </summary>
    Ignore = 0,

    /// <summary>
    /// A commercial - the usual home for sponsors and self promotion.
    /// </summary>
    Commercial = 1,

    /// <summary>
    /// An intro.
    /// </summary>
    Intro = 2,

    /// <summary>
    /// An outro.
    /// </summary>
    Outro = 3,

    /// <summary>
    /// A preview of what is coming.
    /// </summary>
    Preview = 4,

    /// <summary>
    /// A recap of what came before.
    /// </summary>
    Recap = 5,

    /// <summary>
    /// A segment of no particular type. Skippable, but clients label it generically.
    /// </summary>
    Unknown = 6
}

/// <summary>
/// Where a segment may be read from.
/// </summary>
public enum SegmentSourceMode
{
    /// <summary>
    /// Use embedded chapters, and fall back to the API only for items that have none.
    /// </summary>
    ChaptersThenApi = 0,

    /// <summary>
    /// Embedded chapters only. No network access of any kind.
    /// </summary>
    ChaptersOnly = 1,

    /// <summary>
    /// The SponsorBlock API only. Ignores whatever is embedded.
    /// </summary>
    ApiOnly = 2
}

/// <summary>
/// What a scope entry points at.
/// </summary>
public enum ScopeKind
{
    /// <summary>
    /// A whole library.
    /// </summary>
    Library = 0,

    /// <summary>
    /// One series, every season of it.
    /// </summary>
    Series = 1,

    /// <summary>
    /// A single season.
    /// </summary>
    Season = 2
}

/// <summary>
/// One library, series or season that has been opted in to scanning.
/// </summary>
public class ScopeEntry
{
    /// <summary>
    /// Gets or sets the item this entry points at.
    /// </summary>
    public Guid ItemId { get; set; }

    /// <summary>
    /// Gets or sets what kind of item that is.
    /// </summary>
    public ScopeKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the item's name at the time it was added, so the list reads
    /// properly without a library lookup.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the parent series name, for season entries.
    /// </summary>
    public string? ParentName { get; set; }
}

/// <summary>
/// One row of the category mapping table.
/// </summary>
public class CategoryMapping
{
    /// <summary>
    /// Gets or sets the SponsorBlock category.
    /// </summary>
    public SponsorBlockCategory Category { get; set; }

    /// <summary>
    /// Gets or sets what it becomes.
    /// </summary>
    public SegmentAction Action { get; set; }
}

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Master switch. When false the provider reports that it supports nothing, so the
    /// scan skips it entirely and no stored segments are touched.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Where segments are read from.
    /// </summary>
    public SegmentSourceMode SourceMode { get; set; } = SegmentSourceMode.ChaptersThenApi;

    /// <summary>
    /// The libraries, series and seasons that are scanned. This is an allowlist: with it
    /// empty nothing is scanned at all, so a mixed library cannot pick up SponsorBlock
    /// segments on ordinary television by accident.
    /// </summary>
    /// <remarks>
    /// Settable on purpose. A get-only collection is serialised by the XML writer but
    /// silently dropped by the dashboard's JSON configuration endpoint, so entries written
    /// that way vanish without an error.
    /// </remarks>
    public Collection<ScopeEntry> Scope { get; set; } = new();

    /// <summary>
    /// What each SponsorBlock category becomes. Rows missing from this collection fall
    /// back to <see cref="DefaultAction"/>.
    /// </summary>
    public Collection<CategoryMapping> Mappings { get; set; } = Defaults();

    /// <summary>
    /// The action used for a category with no row of its own.
    /// </summary>
    public SegmentAction DefaultAction { get; set; } = SegmentAction.Ignore;

    /// <summary>
    /// The regular expression that pulls a YouTube video id out of a file path, used by
    /// the API source. The first capturing group is the id.
    /// </summary>
    public string VideoIdPattern { get; set; } = @"\[([A-Za-z0-9_-]{11})\]";

    /// <summary>
    /// Base address of the SponsorBlock API.
    /// </summary>
    public string ApiBaseUrl { get; set; } = "https://sponsor.ajay.app";

    /// <summary>
    /// How long an API answer is reused before being asked for again.
    /// </summary>
    public int ApiCacheHours { get; set; } = 24;

    /// <summary>
    /// How long to remember that a video has no segments at all. Worth its own setting:
    /// in a YouTube library a large minority of videos have nothing submitted, and without
    /// this every scan asks about all of them again.
    /// </summary>
    public int ApiNegativeCacheHours { get; set; } = 72;

    /// <summary>
    /// Seconds to wait on an API request before giving up on it.
    /// </summary>
    public int ApiTimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Drop a segment that would overlap one another provider has already stored. Off by
    /// default, which is the right setting when this provider is used instead of another
    /// rather than alongside it.
    /// </summary>
    public bool SkipOverlappingExisting { get; set; }

    /// <summary>
    /// Ignore a segment shorter than this. Guards against a stray one-frame submission
    /// turning into a skip button. Zero disables the check.
    /// </summary>
    public double MinimumSegmentSeconds { get; set; } = 1.0;

    /// <summary>
    /// The mapping table as shipped.
    /// </summary>
    /// <returns>One row per category.</returns>
    public static Collection<CategoryMapping> Defaults() => new()
    {
        new CategoryMapping { Category = SponsorBlockCategory.Sponsor, Action = SegmentAction.Commercial },
        new CategoryMapping { Category = SponsorBlockCategory.SelfPromo, Action = SegmentAction.Commercial },
        new CategoryMapping { Category = SponsorBlockCategory.Interaction, Action = SegmentAction.Commercial },
        new CategoryMapping { Category = SponsorBlockCategory.Intro, Action = SegmentAction.Intro },
        new CategoryMapping { Category = SponsorBlockCategory.Hook, Action = SegmentAction.Intro },
        new CategoryMapping { Category = SponsorBlockCategory.Outro, Action = SegmentAction.Outro },
        new CategoryMapping { Category = SponsorBlockCategory.Preview, Action = SegmentAction.Recap },
        new CategoryMapping { Category = SponsorBlockCategory.Filler, Action = SegmentAction.Ignore },
        new CategoryMapping { Category = SponsorBlockCategory.MusicOffTopic, Action = SegmentAction.Ignore },
        new CategoryMapping { Category = SponsorBlockCategory.PoiHighlight, Action = SegmentAction.Ignore }
    };
}
