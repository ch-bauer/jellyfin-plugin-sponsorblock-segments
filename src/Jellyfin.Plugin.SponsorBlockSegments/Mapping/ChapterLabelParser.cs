using System.Diagnostics.CodeAnalysis;

namespace Jellyfin.Plugin.SponsorBlockSegments.Mapping;

/// <summary>
/// Turns a chapter title written by yt-dlp, or a category name returned by the
/// SponsorBlock API, into a <see cref="SponsorBlockCategory"/>.
/// </summary>
/// <remarks>
/// Matching is on the whole label, not a substring search. That is the point of this
/// class: the plugins that classify these chapters with loose regexes end up reading
/// "Preview/Recap" as an advert, because a pattern meant to catch sponsors also matches
/// the "[SponsorBlock]:" prefix every one of these chapters carries.
/// </remarks>
public static class ChapterLabelParser
{
    /// <summary>
    /// The prefix yt-dlp puts in front of every chapter it creates from SponsorBlock data.
    /// </summary>
    public const string Prefix = "[SponsorBlock]:";

    // Both the human labels yt-dlp writes and the API's own category names, so one table
    // serves the chapter source and the API source. Historical yt-dlp spellings are kept:
    // a library built over several years has chapters from several yt-dlp versions.
    private static readonly Dictionary<string, SponsorBlockCategory> _labels =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Sponsor
            ["Sponsor"] = SponsorBlockCategory.Sponsor,
            ["sponsor"] = SponsorBlockCategory.Sponsor,

            // Unpaid/self promotion
            ["Unpaid/Self Promotion"] = SponsorBlockCategory.SelfPromo,
            ["Self Promotion"] = SponsorBlockCategory.SelfPromo,
            ["selfpromo"] = SponsorBlockCategory.SelfPromo,

            // Interaction reminder
            ["Interaction Reminder"] = SponsorBlockCategory.Interaction,
            ["Interaction Reminder (Subscribe)"] = SponsorBlockCategory.Interaction,
            ["interaction"] = SponsorBlockCategory.Interaction,

            // Intro
            ["Intermission/Intro Animation"] = SponsorBlockCategory.Intro,
            ["Intermission"] = SponsorBlockCategory.Intro,
            ["intro"] = SponsorBlockCategory.Intro,

            // Outro
            ["Endcards/Credits"] = SponsorBlockCategory.Outro,
            ["outro"] = SponsorBlockCategory.Outro,

            // Preview / recap
            ["Preview/Recap"] = SponsorBlockCategory.Preview,
            ["Preview/Recap/Hook"] = SponsorBlockCategory.Preview,
            ["preview"] = SponsorBlockCategory.Preview,

            // Filler
            ["Filler Tangent"] = SponsorBlockCategory.Filler,
            ["Tangents/Jokes"] = SponsorBlockCategory.Filler,
            ["filler"] = SponsorBlockCategory.Filler,

            // Non-music section
            ["Non-Music Section"] = SponsorBlockCategory.MusicOffTopic,
            ["Music: Non-Music Section"] = SponsorBlockCategory.MusicOffTopic,
            ["music_offtopic"] = SponsorBlockCategory.MusicOffTopic,

            // Highlight
            ["Highlight"] = SponsorBlockCategory.PoiHighlight,
            ["poi_highlight"] = SponsorBlockCategory.PoiHighlight,

            // Hook
            ["Hook/Greetings"] = SponsorBlockCategory.Hook,
            ["hook"] = SponsorBlockCategory.Hook
        };

    /// <summary>
    /// True if this chapter title was written from SponsorBlock data.
    /// </summary>
    /// <param name="chapterName">The chapter title.</param>
    /// <returns>Whether the title carries the SponsorBlock prefix.</returns>
    public static bool IsSponsorBlockChapter(string? chapterName) =>
        chapterName is not null &&
        chapterName.TrimStart().StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reads the category out of a chapter title such as
    /// <c>[SponsorBlock]: Unpaid/Self Promotion</c>.
    /// </summary>
    /// <param name="chapterName">The chapter title.</param>
    /// <param name="category">The category, when the title is a known one.</param>
    /// <returns>Whether a category was recognised.</returns>
    public static bool TryParseChapter(
        string? chapterName,
        [NotNullWhen(true)] out SponsorBlockCategory? category)
    {
        category = null;

        if (!IsSponsorBlockChapter(chapterName))
        {
            return false;
        }

        var label = chapterName!.TrimStart()[Prefix.Length..].Trim();
        return TryParseLabel(label, out category);
    }

    /// <summary>
    /// Reads a category from a bare label or API category name, with no prefix.
    /// </summary>
    /// <param name="label">The label, for example <c>Preview/Recap</c> or <c>selfpromo</c>.</param>
    /// <param name="category">The category, when recognised.</param>
    /// <returns>Whether a category was recognised.</returns>
    public static bool TryParseLabel(
        string? label,
        [NotNullWhen(true)] out SponsorBlockCategory? category)
    {
        category = null;

        if (string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        if (_labels.TryGetValue(label.Trim(), out var found))
        {
            category = found;
            return true;
        }

        return false;
    }
}
