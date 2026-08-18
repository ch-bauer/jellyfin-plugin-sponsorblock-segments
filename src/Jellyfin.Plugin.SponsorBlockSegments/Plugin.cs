using System.Globalization;
using Jellyfin.Plugin.SponsorBlockSegments.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.SponsorBlockSegments;

/// <summary>
/// The SponsorBlock Segments plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "SponsorBlock Segments";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("684a50b4-3970-44ef-aab0-3a162b415374");

    /// <inheritdoc />
    public override string Description =>
        "Turns SponsorBlock data into Jellyfin media segments, so sponsors, intros, recaps and self-promotion get a skip button. Reads the [SponsorBlock] chapters yt-dlp embeds and falls back to the SponsorBlock API for files without them. Every segment in a file is emitted, each category maps to a segment type you choose, and nothing is scanned until you opt a library, series or season in.";

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.Configuration.configPage.html",
                    GetType().Namespace)
            }
        };
    }
}
