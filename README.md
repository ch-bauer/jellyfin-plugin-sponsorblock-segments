<div align="center">
  <img src="images/icon.png" alt="SponsorBlock Segments for Jellyfin" width="128" />
  <h1>SponsorBlock Segments for Jellyfin (Proof of Concept)</h1>
</div>

> [!CAUTION]
> **This is a proof of concept, written with AI.** It is purely for testing, and there are
> many items that are known to be incorrect or broken. It is not advisable to use this on a
> non-test server.
>
> For this reason it is offered as is, with **no guarantee of support, bug fixes, or
> troubleshooting**.
>
> **It is NOT recommended to fork or build on top of this plugin!**

Turns SponsorBlock data into real Jellyfin **media segments**, so sponsors, intros, recaps
and self-promotion get a skip button — every one of them, in files that carry several.

It reads the `[SponsorBlock]:` chapters yt-dlp embeds and falls back to the SponsorBlock
API for files that have none. **Nothing is scanned until you opt a library, series or
season in**, so installing it cannot put SponsorBlock segments on ordinary television, and
it writes no segments of its own until you do.

## Why the existing plugins fall short here

Both alternatives work well on what they were built for. Neither handles a YouTube video
carrying seven interspersed sponsor reads.

**[Intro Skipper](https://github.com/intro-skipper/intro-skipper)** already recognises these
chapters — it has an *Enable SponsorBlock chapter detection* option, and
`TryGetSponsorBlockChapterLabel` strips the exact `[SponsorBlock]:` prefix yt-dlp writes.
But it is built for broadcast television: one intro and one credits roll per episode. From
`IntroSkipper/Analyzers/ChapterAnalyzer.cs`:

- `FindMatchingChapter` returns `Segment?` — **one segment per analysis mode per episode**.
  Seven chapters cannot become seven segments.
- A match whose neighbouring chapter also matches the same mode is discarded outright
  (`LogIgnoringAdjacentMatch`).
- `_ambiguousSponsorBlockChapterLabels` — which holds `intermission/intro animation` and
  `preview/recap` — is `.Union()`ed into the **Commercial** set *only*, so an intro and a
  recap both come out labelled an advert.

On a real 44-minute file with seven SponsorBlock chapters it produced exactly **one**
segment: `00:05.486 → 01:33.440`, typed Commercial. That is the Preview/Recap chapter,
mistyped, with the `Sponsor` chapter before it suppressed as an adjacent match. The regex
fields cannot rescue it either — they are anchored `(^|\s)(Intro|…)(\s|$)`, and in
`Intermission/Intro Animation` the word is bounded by `/`.

**[Chapter Segments](https://github.com/jellyfin/jellyfin-plugin-chapter-segments)** emits
every matching chapter, which is the right shape, but classifies by loose substring regex
and knows nothing of SponsorBlock categories. Its stock commercial pattern is
`break|ad|advertisement|intermission|advert|commercial` — a bare `ad` matches any chapter
name containing those two letters anywhere.

The trap either way: **every one of these chapters is prefixed `[SponsorBlock]:`**, so any
pattern written to catch sponsors matches all of them. Add `sponsor` to a commercial regex
and, because Commercial is tested before Preview and Recap, it swallows every other type.

## How it works

An `IMediaSegmentProvider`, which is what puts it in each library's provider list and lets
the server order it against the others.

Categories are matched on the **whole label** after the prefix is stripped — never by
substring. `Preview/Recap` resolves to the preview category because it *is* the preview
category, and cannot be read as a sponsor. Every segment found is emitted; there is no
one-per-mode cap and no adjacent-match suppression.

Historical yt-dlp spellings are recognised too — `Music: Non-Music Section`,
`Interaction Reminder (Subscribe)`, `Tangents/Jokes`, `Preview/Recap/Hook` — because a
library built over several years carries chapters written by several yt-dlp versions.

## Category mapping

Jellyfin has five segment types, so several categories share one. Every row is editable,
and *Ignore* produces no segment and leaves the chapter alone.

| SponsorBlock category | Chapter label | Default |
| --- | --- | --- |
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

## Where segments come from

**Embedded chapters** by default, which needs no network and is exactly what was written at
download time.

> Jellyfin's `ChapterInfo` stores **only** a start position — there is no chapter end time
> in the database. Each segment therefore ends where the next chapter begins, and the last
> ends at the item runtime. This is why the filler chapters yt-dlp writes between marked
> segments matter: they are what terminates the segment before them. Delete them and a
> segment stretches to the start of the next marked one.

**The SponsorBlock API** is the fallback for files with no such chapters. It returns exact
start *and* end times, so API-sourced segments do not depend on filler chapters. It needs a
YouTube video id in the filename; the pattern is configurable and defaults to
`\[([A-Za-z0-9_-]{11})\]`, matching yt-dlp's `… [dQw4w9WgXcQ].mkv`.

Answers are cached, and **misses are cached for longer than hits** — in a YouTube library a
large minority of videos have nothing submitted, and without negative caching every scan
asks about all of them again, which is the slowest possible way to learn nothing.

The id is found by splitting on both path separators rather than
`Path.GetFileName`, which honours only the running platform's. On Linux — where most
servers run — a Windows-style path would otherwise be treated as one long file name, and a
bracketed eleven-character string in any parent directory read as the video id.

## What gets scanned

An **allowlist** of libraries, series and seasons. With nothing selected the plugin
produces no segments at all. A series covers every season of it; add individual seasons to
narrow it.

The check runs for every item a scan considers, so the entry set is held in memory and
rebuilt only when the configuration object it was built from has been replaced — a save
from the dashboard hands back a whole new object, which a cache keyed on anything else has
no way of noticing.

## Priority against other providers

Nothing to configure here. Jellyfin already handles it per library, under
**Libraries → (library) → Media Segment Providers**, where providers are enabled, disabled
and ordered. `MediaSegmentManager.RunSegmentPluginProviders` sorts by
`LibraryOptions.MediaSegmentProviderOrder`, with unlisted providers last.

Segments are stored **per provider**, so this plugin's and another's coexist rather than
overwrite — ordering controls execution order, not precedence. **To use this instead of
Intro Skipper, disable Intro Skipper for that library.** The *skip segments that overlap
another provider's* option is only useful when deliberately running both.

## Configuration

**Dashboard → Plugins → SponsorBlock Segments.** Pick the libraries, series or seasons to
scan, set the category mapping, then run a scan.

The scan must follow a library scan, because the plugin reads chapters out of Jellyfin's
database rather than off disk.

### Preview

The configuration page can show what a scan **would** store for a single episode: every
marked range, the category it was read as, the segment type it maps to, and which source it
came from. Rows that map to *Ignore*, or fall under the minimum length, are greyed and
counted separately.

It resolves sources in the same order the provider does, so what it shows is what a scan
stores — and it works whether or not the item has been opted in, which is the point: check
the mapping before committing to a scan.

### Scanning

Two ways:

- **Scheduled Tasks → Scan SponsorBlock segments** — this plugin's own task. It walks only
  what is in scope instead of every library, which matters when one opted-in series sits in
  a large server. It has **no default trigger**; add one there if it should run on a
  schedule.
- **Scheduled Tasks → Media Segment Scan** — Jellyfin's own, which covers every library and
  every provider.

Both go through the same server pipeline, so each library's provider settings — which are
enabled, and in what order — still decide what runs. A mapping change needs only a re-scan;
the server rewrites a segment whose times or type differ. *Rebuild segments on every
scheduled scan* is for clearing out segments left by an earlier configuration, and is off by
default because the server's force path deletes **every** provider's segments for an item,
not only this plugin's.

## Installation

Add the repository in **Dashboard → Plugins → Repositories**:

```
https://raw.githubusercontent.com/ch-bauer/jellyfin-plugin-sponsorblock-segments/main/manifest.json
```

Then install **SponsorBlock Segments** from the catalogue and restart. Requires Jellyfin
10.11.

## No skip button appears

- The **Media Segment Scan** has to run after the library scan, and after the plugin is
  enabled for that library.
- Skip buttons need **client-side** media segment support. Jellyfin Web has it; some
  clients do not yet, and will show nothing even though the segments exist server-side.
- Check the item is actually in scope — an empty allowlist produces nothing, by design.
- If another provider is also enabled for the library, you may be looking at its segments
  rather than these.

## Building

```
dotnet build src/Jellyfin.Plugin.SponsorBlockSegments/Jellyfin.Plugin.SponsorBlockSegments.csproj -c Release
dotnet test tests/Jellyfin.Plugin.SponsorBlockSegments.Tests/Jellyfin.Plugin.SponsorBlockSegments.Tests.csproj
```

Targets `net9.0` against Jellyfin 10.11.

## License

MIT — see [LICENSE](LICENSE).
