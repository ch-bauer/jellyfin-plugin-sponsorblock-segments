namespace Jellyfin.Plugin.SponsorBlockSegments.Sources;

/// <summary>
/// Remembers API answers so a rescan does not ask again for every video.
/// </summary>
/// <remarks>
/// A miss is cached as well as a hit, and for longer. In a library built from YouTube a
/// sizeable minority of videos have nothing submitted at all, and without negative caching
/// every scan asks about every one of them - the slowest possible way to learn nothing.
/// </remarks>
public sealed class SegmentCache
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    private sealed record Entry(IReadOnlyList<RawSegment> Segments, DateTimeOffset Expires);

    /// <summary>
    /// Looks up a video id.
    /// </summary>
    /// <param name="videoId">The YouTube video id.</param>
    /// <param name="segments">The cached segments; empty for a cached miss.</param>
    /// <returns>Whether a live entry was found.</returns>
    public bool TryGet(string videoId, out IReadOnlyList<RawSegment> segments)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(videoId, out var entry))
            {
                if (entry.Expires > DateTimeOffset.UtcNow)
                {
                    segments = entry.Segments;
                    return true;
                }

                _entries.Remove(videoId);
            }
        }

        segments = Array.Empty<RawSegment>();
        return false;
    }

    /// <summary>
    /// Stores an answer.
    /// </summary>
    /// <param name="videoId">The YouTube video id.</param>
    /// <param name="segments">What the API returned; may be empty.</param>
    /// <param name="lifetime">How long to keep it.</param>
    public void Set(string videoId, IReadOnlyList<RawSegment> segments, TimeSpan lifetime)
    {
        if (lifetime <= TimeSpan.Zero)
        {
            return;
        }

        lock (_gate)
        {
            _entries[videoId] = new Entry(segments, DateTimeOffset.UtcNow.Add(lifetime));
        }
    }

    /// <summary>
    /// Empties the cache, so the next scan asks the API again.
    /// </summary>
    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
        }
    }

    /// <summary>
    /// Gets the number of entries currently held, for the configuration page.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }
}
