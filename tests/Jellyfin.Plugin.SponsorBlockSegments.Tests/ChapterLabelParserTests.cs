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

    // yt-dlp merges two overlapping SponsorBlock segments into one chapter and joins the
    // labels with a comma. Every case below was taken from a real library scan; before
    // the split they all fell through to the default action and produced no segment.
    [Theory]
    [InlineData("[SponsorBlock]: Sponsor, Intermission/Intro Animation", SponsorBlockCategory.Sponsor)]
    [InlineData("[SponsorBlock]: Intermission/Intro Animation, Sponsor", SponsorBlockCategory.Intro)]
    [InlineData("[SponsorBlock]: Sponsor, Preview/Recap", SponsorBlockCategory.Sponsor)]
    [InlineData("[SponsorBlock]: Preview/Recap, Highlight", SponsorBlockCategory.Preview)]
    [InlineData("[SponsorBlock]: Preview/Recap, Intermission/Intro Animation", SponsorBlockCategory.Preview)]
    [InlineData("[SponsorBlock]: Hook/Greetings, Intermission/Intro Animation", SponsorBlockCategory.Hook)]
    [InlineData("[SponsorBlock]: Unpaid/Self Promotion, Sponsor", SponsorBlockCategory.SelfPromo)]
    public void A_merged_chapter_takes_the_first_recognised_label(string chapter, SponsorBlockCategory expected)
    {
        Assert.True(ChapterLabelParser.TryParseChapter(chapter, out var category));
        Assert.Equal(expected, category);
    }

    [Fact]
    public void An_unrecognised_leading_part_does_not_stop_the_rest_being_read()
    {
        Assert.True(ChapterLabelParser.TryParseChapter("[SponsorBlock]: Something New, Sponsor", out var category));
        Assert.Equal(SponsorBlockCategory.Sponsor, category);
    }

    [Fact]
    public void A_compound_of_nothing_known_is_still_rejected()
    {
        Assert.False(ChapterLabelParser.TryParseChapter("[SponsorBlock]: Something New, Another Thing", out _));
    }

    // The whole-label lookup has to run before the split, or a label that legitimately
    // held a comma would be read as a compound of its own fragments.
    [Fact]
    public void The_whole_label_is_matched_before_any_splitting()
    {
        Assert.True(ChapterLabelParser.TryParseChapter("[SponsorBlock]: Music: Non-Music Section", out var category));
        Assert.Equal(SponsorBlockCategory.MusicOffTopic, category);
    }

    // The reason this class does not use substring matching: every chapter carries the
    // prefix, so a "sponsor" substring test matches all of them. Splitting must not
    // reintroduce that.
    [Fact]
    public void Splitting_does_not_let_the_prefix_be_read_as_a_category()
    {
        Assert.False(ChapterLabelParser.TryParseLabel("[SponsorBlock], Nonsense", out _));
    }
}
