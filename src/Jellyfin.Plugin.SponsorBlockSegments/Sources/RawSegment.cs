using Jellyfin.Plugin.SponsorBlockSegments.Mapping;

namespace Jellyfin.Plugin.SponsorBlockSegments.Sources;

/// <summary>
/// Where a segment was read from.
/// </summary>
public enum SegmentOrigin
{
    /// <summary>
    /// A chapter embedded in the media file.
    /// </summary>
    Chapter = 0,

    /// <summary>
    /// The SponsorBlock API.
    /// </summary>
    Api = 1
}

/// <summary>
/// A segment as read from a source, before the category has been mapped to a Jellyfin
/// segment type.
/// </summary>
/// <param name="Category">The SponsorBlock category.</param>
/// <param name="StartTicks">Start position, in ticks.</param>
/// <param name="EndTicks">End position, in ticks.</param>
/// <param name="Origin">Where it came from.</param>
public readonly record struct RawSegment(
    SponsorBlockCategory Category,
    long StartTicks,
    long EndTicks,
    SegmentOrigin Origin)
{
    /// <summary>
    /// Gets the length of the segment.
    /// </summary>
    public TimeSpan Duration => TimeSpan.FromTicks(Math.Max(0, EndTicks - StartTicks));

    /// <summary>
    /// Gets a value indicating whether the segment covers a positive span of time.
    /// </summary>
    public bool IsValid => EndTicks > StartTicks && StartTicks >= 0;
}
