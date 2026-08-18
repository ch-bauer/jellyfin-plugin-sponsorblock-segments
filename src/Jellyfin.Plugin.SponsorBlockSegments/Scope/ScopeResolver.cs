using Jellyfin.Plugin.SponsorBlockSegments.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.SponsorBlockSegments.Scope;

/// <summary>
/// Decides whether an item has been opted in to scanning.
/// </summary>
/// <remarks>
/// An allowlist, deliberately: with nothing configured this provider produces nothing at
/// all, so installing it cannot put SponsorBlock segments on ordinary television.
/// <para>
/// The check runs for every item the scan considers, so the entry set is held in memory
/// and rebuilt only when the configuration object it was built from has been replaced -
/// the same approach the Next Up Cleanup exclusion store uses, and for the same reason: a
/// save from the dashboard hands back a whole new configuration object, which a cache
/// keyed on anything else has no way of noticing.
/// </para>
/// </remarks>
public sealed class ScopeResolver
{
    private readonly ILibraryManager _libraryManager;
    private readonly object _gate = new();

    private HashSet<Guid>? _libraries;
    private HashSet<Guid>? _series;
    private HashSet<Guid>? _seasons;
    private PluginConfiguration? _builtFrom;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScopeResolver"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    public ScopeResolver(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    /// <summary>
    /// True when this item sits under something that has been opted in.
    /// </summary>
    /// <param name="item">The item being considered.</param>
    /// <returns>Whether it is in scope.</returns>
    public bool IsInScope(BaseItem? item)
    {
        if (item is null)
        {
            return false;
        }

        var config = Plugin.Instance?.Configuration;
        if (config is null || !config.Enabled)
        {
            return false;
        }

        lock (_gate)
        {
            Rebuild(config);

            if (_libraries!.Count == 0 && _series!.Count == 0 && _seasons!.Count == 0)
            {
                return false;
            }

            if (_seasons!.Contains(item.Id) || _series!.Contains(item.Id) || _libraries!.Contains(item.Id))
            {
                return true;
            }

            if (item is Episode episode)
            {
                if (episode.SeasonId != Guid.Empty && _seasons.Contains(episode.SeasonId))
                {
                    return true;
                }

                if (episode.SeriesId != Guid.Empty && _series.Contains(episode.SeriesId))
                {
                    return true;
                }
            }

            if (item is Season season && season.SeriesId != Guid.Empty && _series.Contains(season.SeriesId))
            {
                return true;
            }

            return InAllowedLibrary(item);
        }
    }

    /// <summary>
    /// Drops the cached sets so the next check rebuilds them.
    /// </summary>
    public void Invalidate()
    {
        lock (_gate)
        {
            _libraries = null;
            _series = null;
            _seasons = null;
            _builtFrom = null;
        }
    }

    private void Rebuild(PluginConfiguration config)
    {
        if (_libraries is not null && ReferenceEquals(config, _builtFrom))
        {
            return;
        }

        _libraries = config.Scope.Where(e => e.Kind == ScopeKind.Library).Select(e => e.ItemId).ToHashSet();
        _series = config.Scope.Where(e => e.Kind == ScopeKind.Series).Select(e => e.ItemId).ToHashSet();
        _seasons = config.Scope.Where(e => e.Kind == ScopeKind.Season).Select(e => e.ItemId).ToHashSet();
        _builtFrom = config;
    }

    private bool InAllowedLibrary(BaseItem item)
    {
        if (_libraries!.Count == 0)
        {
            return false;
        }

        // GetCollectionFolders walks up to the library roots the item actually belongs to,
        // which is cheaper and more reliable than comparing path prefixes.
        foreach (var folder in _libraryManager.GetCollectionFolders(item))
        {
            if (_libraries.Contains(folder.Id))
            {
                return true;
            }
        }

        return false;
    }
}
