using Jellyfin.Plugin.SponsorBlockSegments.Scope;
using Xunit;

namespace Jellyfin.Plugin.SponsorBlockSegments.Tests;

/// <summary>
/// Covers the default pattern against the filenames yt-dlp actually writes, plus the
/// behaviour of a pattern a user has broken.
/// </summary>
public class VideoIdExtractorTests
{
    private const string Default = @"\[([A-Za-z0-9_-]{11})\]";

    [Theory]
    [InlineData(@"C:\Media\Show (2010) S11E29 - Title [_G0KJ_ytnY8].mkv", "_G0KJ_ytnY8")]
    [InlineData(@"/media/Show/Season 01/Show S01E01 [dQw4w9WgXcQ].mkv", "dQw4w9WgXcQ")]
    [InlineData(@"/media/Show/Show [a-b_c-d_e-f].mkv", "a-b_c-d_e-f")]
    public void Finds_the_id_with_the_default_pattern(string path, string expected)
    {
        var extractor = new VideoIdExtractor();
        Assert.True(extractor.TryExtract(path, Default, out var id));
        Assert.Equal(expected, id);
    }

    [Theory]
    [InlineData(@"C:\Media\Show S01E01 - No id here.mkv")]
    [InlineData(@"C:\Media\Show [tooshort].mkv")]
    [InlineData(null)]
    [InlineData("")]
    public void Returns_false_when_there_is_no_id(string? path)
    {
        var extractor = new VideoIdExtractor();
        Assert.False(extractor.TryExtract(path, Default, out _));
    }

    /// <summary>
    /// The id is looked for in the file name, not the whole path, so a folder that happens
    /// to contain a bracketed 11-character string cannot win.
    /// </summary>
    /// <remarks>
    /// Both separators are checked on every platform. Path.GetFileName honours only the
    /// running platform's separator, so this passed on Windows and failed on Linux - where
    /// most servers run - until the split was made platform-independent.
    /// </remarks>
    [Theory]
    [InlineData(@"C:\Media\[aaaaaaaaaaa]\Show S01E01.mkv")]
    [InlineData("/media/[aaaaaaaaaaa]/Show S01E01.mkv")]
    public void Only_the_file_name_is_searched(string path)
    {
        var extractor = new VideoIdExtractor();
        Assert.False(extractor.TryExtract(path, Default, out _));
    }

    /// <summary>
    /// The same path must resolve identically whichever separator it uses.
    /// </summary>
    [Theory]
    [InlineData(@"C:\Media\Show\Show S01E01 [dQw4w9WgXcQ].mkv")]
    [InlineData("/media/Show/Show S01E01 [dQw4w9WgXcQ].mkv")]
    public void Both_separators_behave_the_same(string path)
    {
        var extractor = new VideoIdExtractor();
        Assert.True(extractor.TryExtract(path, Default, out var id));
        Assert.Equal("dQw4w9WgXcQ", id);
    }

    /// <summary>
    /// A bad pattern must fail this item quietly rather than throw into the scan.
    /// </summary>
    [Fact]
    public void Invalid_pattern_is_not_fatal()
    {
        var extractor = new VideoIdExtractor();
        Assert.False(extractor.TryExtract(@"C:\Media\Show [dQw4w9WgXcQ].mkv", "([unclosed", out _));
    }

    [Fact]
    public void Pattern_without_a_group_falls_back_to_the_whole_match()
    {
        var extractor = new VideoIdExtractor();
        Assert.True(extractor.TryExtract(
            @"C:\Media\dQw4w9WgXcQ.mkv", "[A-Za-z0-9_-]{11}", out var id));
        Assert.Equal("dQw4w9WgXcQ", id);
    }
}
