using Jellyfin.Plugin.SponsorBlockSegments.Mapping;
using Jellyfin.Plugin.SponsorBlockSegments.Providers;
using Jellyfin.Plugin.SponsorBlockSegments.Scope;
using Jellyfin.Plugin.SponsorBlockSegments.Sources;
using MediaBrowser.Controller;
using MediaBrowser.Controller.MediaSegments;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.SponsorBlockSegments;

/// <summary>
/// Registers the plugin's services with the server's dependency injection container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        // Singletons because each holds a cache keyed on the configuration object it was
        // built from: a save from the dashboard replaces that object, which is how they
        // notice they are stale. A transient registration would throw the cache away on
        // every item and defeat the point.
        serviceCollection.AddSingleton<ScopeResolver>();
        serviceCollection.AddSingleton<CategoryMap>();
        serviceCollection.AddSingleton<VideoIdExtractor>();
        serviceCollection.AddSingleton<SegmentCache>();

        serviceCollection.AddSingleton<ChapterSegmentSource>();
        serviceCollection.AddSingleton<ApiSegmentSource>();

        // Registered twice over one instance: the concrete type so the configuration
        // page's preview endpoint can ask the very same object what it would produce,
        // and the interface because that is what puts it in the list the server hands to
        // MediaSegmentManager - and therefore what makes it appear in each library's
        // media segment provider settings, where it gets ordered against, or used instead
        // of, another provider.
        serviceCollection.AddSingleton<SponsorBlockSegmentProvider>();
        serviceCollection.AddSingleton<IMediaSegmentProvider>(
            sp => sp.GetRequiredService<SponsorBlockSegmentProvider>());
    }
}
