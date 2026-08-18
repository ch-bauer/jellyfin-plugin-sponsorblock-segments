using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.SponsorBlockSegments.Configuration;

namespace Jellyfin.Plugin.SponsorBlockSegments.Mapping;

/// <summary>
/// Resolves a SponsorBlock category to the Jellyfin segment type configured for it.
/// </summary>
public sealed class CategoryMap
{
    private readonly object _gate = new();
    private Dictionary<SponsorBlockCategory, SegmentAction>? _map;

    // The configuration object the table was built from. Saving from the dashboard hands
    // back a whole new object rather than mutating this one, so comparing the reference is
    // what tells the cache it is looking at stale data.
    private PluginConfiguration? _builtFrom;

    /// <summary>
    /// What this category should become, according to the current configuration.
    /// </summary>
    /// <param name="category">The SponsorBlock category.</param>
    /// <returns>The configured action.</returns>
    public SegmentAction ActionFor(SponsorBlockCategory category)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            return SegmentAction.Ignore;
        }

        lock (_gate)
        {
            if (_map is null || !ReferenceEquals(config, _builtFrom))
            {
                _map = new Dictionary<SponsorBlockCategory, SegmentAction>();

                // Last row wins, so a duplicated category in a hand-edited configuration
                // file resolves to something rather than throwing.
                foreach (var row in config.Mappings)
                {
                    _map[row.Category] = row.Action;
                }

                _builtFrom = config;
            }

            return _map.TryGetValue(category, out var action) ? action : config.DefaultAction;
        }
    }

    /// <summary>
    /// The Jellyfin segment type for an action, or null when the action is to ignore it.
    /// </summary>
    /// <param name="action">The configured action.</param>
    /// <returns>The segment type, or null.</returns>
    public static MediaSegmentType? ToSegmentType(SegmentAction action) => action switch
    {
        SegmentAction.Commercial => MediaSegmentType.Commercial,
        SegmentAction.Intro => MediaSegmentType.Intro,
        SegmentAction.Outro => MediaSegmentType.Outro,
        SegmentAction.Preview => MediaSegmentType.Preview,
        SegmentAction.Recap => MediaSegmentType.Recap,
        SegmentAction.Unknown => MediaSegmentType.Unknown,
        _ => null
    };

    /// <summary>
    /// Convenience for the provider: category straight to segment type, null when ignored.
    /// </summary>
    /// <param name="category">The SponsorBlock category.</param>
    /// <returns>The segment type, or null.</returns>
    public MediaSegmentType? TypeFor(SponsorBlockCategory category) =>
        ToSegmentType(ActionFor(category));

    /// <summary>
    /// Drops the cached table so the next lookup rebuilds it.
    /// </summary>
    public void Invalidate()
    {
        lock (_gate)
        {
            _map = null;
            _builtFrom = null;
        }
    }
}
