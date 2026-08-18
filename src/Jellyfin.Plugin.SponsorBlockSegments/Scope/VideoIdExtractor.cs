using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.SponsorBlockSegments.Scope;

/// <summary>
/// Pulls a YouTube video id out of a file path, for the API source.
/// </summary>
/// <remarks>
/// The pattern is configurable because there is no single convention: yt-dlp writes
/// <c>… [dQw4w9WgXcQ].mkv</c> by default, TubeArchivist names files after the id alone,
/// and hand-built libraries do their own thing. The compiled regex is cached and rebuilt
/// only when the pattern string changes, since this runs once per item per scan.
/// </remarks>
public sealed class VideoIdExtractor
{
    private readonly object _gate = new();
    private Regex? _regex;
    private string? _builtFrom;

    /// <summary>
    /// Finds the video id in a path.
    /// </summary>
    /// <param name="path">The media file path.</param>
    /// <param name="pattern">The configured regular expression.</param>
    /// <param name="videoId">The id, when one was found.</param>
    /// <returns>Whether an id was found.</returns>
    public bool TryExtract(
        string? path,
        string? pattern,
        [NotNullWhen(true)] out string? videoId)
    {
        videoId = null;

        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        var regex = Compile(pattern);
        if (regex is null)
        {
            return false;
        }

        Match match;
        try
        {
            match = regex.Match(Path.GetFileName(path));
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }

        if (!match.Success)
        {
            return false;
        }

        // Prefer the first capturing group so a pattern can match brackets it does not
        // want to keep; fall back to the whole match for a pattern with no group.
        var value = match.Groups.Count > 1 && match.Groups[1].Success
            ? match.Groups[1].Value
            : match.Value;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        videoId = value;
        return true;
    }

    private Regex? Compile(string pattern)
    {
        lock (_gate)
        {
            if (_regex is not null && string.Equals(pattern, _builtFrom, StringComparison.Ordinal))
            {
                return _regex;
            }

            try
            {
                // A user-supplied pattern gets a timeout: a bad one should fail this item,
                // not hang the scan.
                _regex = new Regex(
                    pattern,
                    RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(250));
                _builtFrom = pattern;
            }
            catch (ArgumentException)
            {
                _regex = null;
                _builtFrom = pattern;
            }

            return _regex;
        }
    }
}
