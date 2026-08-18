# SponsorBlock Segments

A Jellyfin media segment provider that turns SponsorBlock data into real, skippable
segments — with per-series control over what gets scanned and a category mapping you
choose.

## Why

Existing options fall short on a YouTube-sourced library:

**Intro Skipper** reads `[SponsorBlock]:` chapters, but is built for broadcast television —
one intro and one credits roll per episode. Verified in `ChapterAnalyzer.cs`:

- `FindMatchingChapter` returns `Segment?` — **one segment per analysis mode per episode**.
- A match whose neighbouring chapter matches the same mode is dropped
  (`LogIgnoringAdjacentMatch`).
- `_ambiguousSponsorBlockChapterLabels` — including `intermission/intro animation` and
  `preview/recap` — is unioned into **Commercial only**.

On a real file with seven SponsorBlock chapters it produced exactly **one** segment,
`00:05.486 → 01:33.440`, typed Commercial: the Preview/Recap chapter, mistyped, with the
Sponsor chapter before it suppressed as an adjacent match.

**Chapter Segments** emits every matching chapter, but classifies by loose substring regex,
knows nothing of SponsorBlock categories, and offers no scope control. Its default
commercial pattern contains a bare `ad`, which matches any chapter name containing those
two letters.

This plugin emits **every** segment, matches on the **whole** category label, and scans
only what you opt in.

## Behaviour

Categories are matched on the complete label after stripping the `[SponsorBlock]:` prefix —
never by substring. That is the core fix: since every one of these chapters carries that
prefix, any pattern written to catch sponsors matches all of them, which is how a recap
ends up labelled an advert.

Default mapping, all editable:

| Category | Chapter label | Default |
|---|---|---|
| `sponsor` | Sponsor | Commercial |
| `selfpromo` | Unpaid/Self Promotion | Commercial |
| `interaction` | Interaction Reminder | Commercial |
| `intro` | Intermission/Intro Animation | Intro |
| `hook` | Hook/Greetings | Intro |
| `outro` | Endcards/Credits | Outro |
| `preview` | Preview/Recap | Recap |
| `filler` | Filler Tangent | ignored |
| `music_offtopic` | Non-Music Section | ignored |
| `poi_highlight` | Highlight | ignored |

Historical yt-dlp spellings (`Music: Non-Music Section`, `Interaction Reminder (Subscribe)`,
`Tangents/Jokes`, `Preview/Recap/Hook`) are recognised too, since a library built over
several years carries chapters from several yt-dlp versions.

## Sources

**Embedded chapters** (default) need no network. Jellyfin's `ChapterInfo` stores only a
start position — there is no end time in the database — so each segment ends where the next
chapter begins, and the last ends at the item runtime.

> This is why the filler chapters yt-dlp writes between marked segments matter: they are
> what terminates the segment before them. Deleting them stretches a segment to the start of
> the next marked one.

**SponsorBlock API** is the fallback for files with no such chapters. It returns exact start
and end times, so API-sourced segments do not depend on filler chapters. It needs a YouTube
video id in the filename; the pattern is configurable and defaults to `\[([A-Za-z0-9_-]{11})\]`,
matching yt-dlp's `… [dQw4w9WgXcQ].mkv`.

Answers are cached, misses for longer than hits — in a YouTube library a large minority of
videos have nothing submitted, and without negative caching every scan asks about all of
them again.

## Scope

An allowlist of libraries, series and seasons. With nothing selected the plugin produces no
segments at all, so installing it cannot put SponsorBlock segments on ordinary television.
A series covers all its seasons; add individual seasons to narrow it.

## Priority over other providers

Nothing to configure in this plugin. Jellyfin handles it per library:

*Libraries → (library) → Media Segment Providers* — enable, disable and order them there.
`MediaSegmentManager` sorts by `LibraryOptions.MediaSegmentProviderOrder`, with unlisted
providers last.

Segments are stored **per provider**, so this plugin's and another's coexist rather than
overwrite. To use this one instead of Intro Skipper, disable Intro Skipper for that library.
The *Skip segments that overlap another provider's* option is only useful when deliberately
running both.

## Configuration

Dashboard → Plugins → SponsorBlock Segments. Pick libraries/series/seasons, set the category
mapping, then run *Scheduled Tasks → Media Segment Scan*.

## Building

```
dotnet build src/Jellyfin.Plugin.SponsorBlockSegments/Jellyfin.Plugin.SponsorBlockSegments.csproj -c Release
dotnet test tests/Jellyfin.Plugin.SponsorBlockSegments.Tests/Jellyfin.Plugin.SponsorBlockSegments.Tests.csproj
```

Targets `net9.0` against Jellyfin 10.11.
