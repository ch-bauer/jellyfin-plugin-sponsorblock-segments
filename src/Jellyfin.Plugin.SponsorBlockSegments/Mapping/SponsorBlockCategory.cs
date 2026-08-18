namespace Jellyfin.Plugin.SponsorBlockSegments.Mapping;

/// <summary>
/// The SponsorBlock categories, as the API names them and as yt-dlp writes them into a
/// chapter title.
/// </summary>
/// <remarks>
/// Kept as an enum rather than loose strings so the configuration can carry one entry per
/// category and the mapping table cannot drift out of sync with what the parser produces.
/// </remarks>
public enum SponsorBlockCategory
{
    /// <summary>
    /// Paid promotion. Chapter label "Sponsor".
    /// </summary>
    Sponsor = 0,

    /// <summary>
    /// Unpaid or self promotion - the channel's own merchandise, socials, other videos.
    /// Chapter label "Unpaid/Self Promotion".
    /// </summary>
    SelfPromo = 1,

    /// <summary>
    /// Reminders to like, subscribe or follow. Chapter label "Interaction Reminder".
    /// </summary>
    Interaction = 2,

    /// <summary>
    /// Intermission or intro animation. Chapter label "Intermission/Intro Animation".
    /// </summary>
    Intro = 3,

    /// <summary>
    /// Endcards and credits. Chapter label "Endcards/Credits".
    /// </summary>
    Outro = 4,

    /// <summary>
    /// A recap of earlier material or a preview of what is coming.
    /// Chapter label "Preview/Recap".
    /// </summary>
    Preview = 5,

    /// <summary>
    /// Tangents and jokes that add no information. Chapter label "Filler Tangent".
    /// </summary>
    Filler = 6,

    /// <summary>
    /// A non-music section of a music video. Chapter label "Non-Music Section".
    /// </summary>
    MusicOffTopic = 7,

    /// <summary>
    /// The single point the video is "about". Chapter label "Highlight".
    /// </summary>
    PoiHighlight = 8,

    /// <summary>
    /// Greetings and hooks before the content proper. Chapter label "Hook/Greetings".
    /// </summary>
    Hook = 9
}
