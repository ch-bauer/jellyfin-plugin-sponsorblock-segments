using Jellyfin.Plugin.SponsorBlockSegments.Mapping;
using Xunit;

namespace Jellyfin.Plugin.SponsorBlockSegments.Tests;

/// <summary>
/// The label table is the whole reason this plugin exists, so it is pinned down hard:
/// every category yt-dlp can write must resolve, and nothing else may.
/// </summary>
public class ChapterLabelParserTests
{
    [Theory]
    [InlineData("[SponsorBlock]: Sponsor", SponsorBlockCategory.Sponsor)]
    [InlineData("[SponsorBlock]: Unpaid/Self Promotion", SponsorBlockCategory.SelfPromo)]
    [InlineData("[SponsorBlock]: Interaction Reminder", SponsorBlockCategory.Interaction)]
    [InlineData("[SponsorBlock]: Intermission/Intro Animation", SponsorBlockCategory.Intro)]
    [InlineData("[SponsorBlock]: Endcards/Credits", SponsorBlockCategory.Outro)]
    [InlineData("[SponsorBlock]: Preview/Recap", SponsorBlockCategory.Preview)]
    [InlineData("[SponsorBlock]: Filler Tangent", SponsorBlockCategory.Filler)]
    [InlineData("[SponsorBlock]: Non-Music Section", SponsorBlockCategory.MusicOffTopic)]
    [InlineData("[SponsorBlock]: Highlight", SponsorBlockCategory.PoiHighlight)]
    [InlineData("[SponsorBlock]: Hook/Greetings", SponsorBlockCategory.Hook)]
    public void Parses_every_yt_dlp_label(string chapter, SponsorBlockCategory expected)
    {
        Assert.True(ChapterLabelParser.TryParseChapter(chapter, out var category));
        Assert.Equal(expected, category);
    }

    /// <summary>
    /// The failure that motivated the plugin: a substring matcher reads the
    /// "[SponsorBlock]:" prefix every chapter carries and calls a recap an advert.
    /// Whole-label matching must not.
    /// </summary>
    [Fact]
    public void Preview_is_not_mistaken_for_a_sponsor()
    {
        Assert.True(ChapterLabelParser.TryParseChapter("[SponsorBlock]: Preview/Recap", out var category));
        Assert.Equal(SponsorBlockCategory.Preview, category);
        Assert.NotEqual(SponsorBlockCategory.Sponsor, category);
    }

    [Theory]
    [InlineData("Some ordinary chapter")]
    [InlineData("Chapter 1")]
    [InlineData("")]
    [InlineData(null)]
    public void Ignores_chapters_without_the_prefix(string? chapter)
    {
        Assert.False(ChapterLabelParser.TryParseChapter(chapter, out _));
        Assert.False(ChapterLabelParser.IsSponsorBlockChapter(chapter));
    }

    [Fact]
    public void Unknown_label_with_the_prefix_is_not_guessed_at()
    {
        Assert.True(ChapterLabelParser.IsSponsorBlockChapter("[SponsorBlock]: Something New"));
        Assert.False(ChapterLabelParser.TryParseChapter("[SponsorBlock]: Something New", out _));
    }

    [Theory]
    [InlineData("sponsor", SponsorBlockCategory.Sponsor)]
    [InlineData("selfpromo", SponsorBlockCategory.SelfPromo)]
    [InlineData("music_offtopic", SponsorBlockCategory.MusicOffTopic)]
    [InlineData("poi_highlight", SponsorBlockCategory.PoiHighlight)]
    public void Parses_api_category_names(string label, SponsorBlockCategory expected)
    {
        Assert.True(ChapterLabelParser.TryParseLabel(label, out var category));
        Assert.Equal(expected, category);
    }

    /// <summary>
    /// A library built over several years carries chapters from several yt-dlp versions.
    /// </summary>
    [Theory]
    [InlineData("[SponsorBlock]: Music: Non-Music Section", SponsorBlockCategory.MusicOffTopic)]
    [InlineData("[SponsorBlock]: Interaction Reminder (Subscribe)", SponsorBlockCategory.Interaction)]
    [InlineData("[SponsorBlock]: Tangents/Jokes", SponsorBlockCategory.Filler)]
    [InlineData("[SponsorBlock]: Preview/Recap/Hook", SponsorBlockCategory.Preview)]
    public void Parses_historical_spellings(string chapter, SponsorBlockCategory expected)
    {
        Assert.True(ChapterLabelParser.TryParseChapter(chapter, out var category));
        Assert.Equal(expected, category);
    }
}
