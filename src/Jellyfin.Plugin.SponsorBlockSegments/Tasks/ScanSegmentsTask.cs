using Jellyfin.Data.Enums;
using Jellyfin.Plugin.SponsorBlockSegments.Configuration;
using Jellyfin.Plugin.SponsorBlockSegments.Scope;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaSegments;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SponsorBlockSegments.Tasks;

/// <summary>
/// Scans only the items that have been opted in, rather than the whole server.
/// </summary>
/// <remarks>
/// Jellyfin's own <c>Media Segment Scan</c> walks every library, which on a large server is
/// a long job to run for the sake of one opted-in series. This narrows the work to the
/// scope configured here.
/// <para>
/// It deliberately goes through <see cref="IMediaSegmentManager.RunSegmentPluginProviders"/>
/// rather than writing segments itself. That keeps each library's own provider settings -
/// which providers are enabled, and in what order - authoritative, and it is the only way
/// to add segments without disturbing another provider's: the manager's delete method
/// removes every provider's segments for an item, not just this plugin's.
/// </para>
/// </remarks>
public class ScanSegmentsTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly IMediaSegmentManager _segmentManager;
    private readonly ScopeResolver _scope;
    private readonly ILogger<ScanSegmentsTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScanSegmentsTask"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="segmentManager">The media segment manager.</param>
    /// <param name="scope">The scope resolver.</param>
    /// <param name="logger">The logger.</param>
    public ScanSegmentsTask(
        ILibraryManager libraryManager,
        IMediaSegmentManager segmentManager,
        ScopeResolver scope,
        ILogger<ScanSegmentsTask> logger)
    {
        _libraryManager = libraryManager;
        _segmentManager = segmentManager;
        _scope = scope;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Scan SponsorBlock segments";

    /// <inheritdoc />
    public string Key => "SponsorBlockSegmentsScan";

    /// <inheritdoc />
    public string Category => "SponsorBlock Segments";

    /// <inheritdoc />
    public string Description =>
        "Creates media segments for the libraries, series and seasons opted in on the "
        + "plugin's configuration page, without walking the rest of the server. Runs the "
        + "segment providers each library has enabled, so it respects their order and any "
        + "that are switched off.";

    /// <inheritdoc />
    /// <remarks>
    /// No default trigger. The scan is cheap for a small scope but it is still work, and a
    /// task that starts itself the day it is installed is a surprise; add a trigger from
    /// Dashboard - Scheduled Tasks if it should run on a schedule.
    /// </remarks>
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => Array.Empty<TaskTriggerInfo>();

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || !config.Enabled)
        {
            _logger.LogInformation("SponsorBlock Segments is switched off; nothing scanned");
            progress.Report(100);
            return;
        }

        var items = CollectScopedItems(config);
        if (items.Count == 0)
        {
            _logger.LogInformation(
                "Nothing is opted in, so there is nothing to scan. Add a library, series or "
                + "season on the plugin's configuration page.");
            progress.Report(100);
            return;
        }

        _logger.LogInformation("Scanning {Count} item(s) in scope", items.Count);

        var done = 0;
        var written = 0;
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_segmentManager.IsTypeSupported(item))
            {
                try
                {
                    await _segmentManager.RunSegmentPluginProviders(
                        item,
                        _libraryManager.GetLibraryOptions(item),
                        config.ForceOverwriteOnScan,
                        cancellationToken).ConfigureAwait(false);
                    written++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // One bad item must not end the run.
                    _logger.LogWarning(ex, "Could not scan {Path}", item.Path);
                }
            }

            done++;
            progress.Report(done * 100.0 / items.Count);
        }

        _logger.LogInformation("Scanned {Written} of {Total} item(s)", written, items.Count);
        progress.Report(100);
    }

    /// <summary>
    /// Every playable item under something that has been opted in, without duplicates.
    /// </summary>
    private List<BaseItem> CollectScopedItems(PluginConfiguration config)
    {
        var seen = new HashSet<Guid>();
        var items = new List<BaseItem>();

        foreach (var entry in config.Scope)
        {
            var parent = _libraryManager.GetItemById(entry.ItemId);
            if (parent is null)
            {
                _logger.LogDebug(
                    "Scope entry {Name} ({Id}) is no longer in the library",
                    entry.Name,
                    entry.ItemId);
                continue;
            }

            var children = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Episode, BaseItemKind.Movie },
                Recursive = true,
                Parent = parent
            });

            foreach (var child in children)
            {
                // The resolver is still consulted: a season may sit under a series that is
                // not opted in, and an entry may since have been narrowed.
                if (seen.Add(child.Id) && _scope.IsInScope(child))
                {
                    items.Add(child);
                }
            }
        }

        return items;
    }
}
