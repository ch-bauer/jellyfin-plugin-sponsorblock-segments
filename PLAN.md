# SponsorBlock Segments — plugin plan

A Jellyfin media segment provider that turns SponsorBlock data into real,
skippable Jellyfin segments, with per-series/per-season control over what gets
scanned and a fully editable category mapping.

## Why this exists

Intro Skipper already reads `[SponsorBlock]:` chapters, but it is built for
broadcast TV — one intro and one credits sequence per episode — and cannot
represent a YouTube video with several interspersed segments. Verified against
`IntroSkipper/Analyzers/ChapterAnalyzer.cs`:

1. `FindMatchingChapter` returns `Segment?` — **one segment per analysis mode per
   episode**, so an episode with seven SponsorBlock chapters can never produce
   seven segments.
2. Adjacent-match suppression: if the neighbouring chapter also matches the same
   mode, the current one is dropped (`LogIgnoringAdjacentMatch`).
3. `_ambiguousSponsorBlockChapterLabels` — `intermission/intro animation` and
   `preview/recap` among them — is unioned into **Commercial only**, so an intro
   and a recap both come out labelled as an advert.

Observed on a real file (S11E29, 7 SponsorBlock chapters): Intro Skipper produced
exactly **one** segment, `00:05.486 → 01:33.440`, typed Commercial — the
Preview/Recap chapter, mistyped, with the preceding Sponsor chapter suppressed as
an adjacent match.

The official Chapter Segments plugin emits every matching chapter, but classifies
by loose substring regex, has no notion of SponsorBlock categories, and offers no
scope control.

## Decisions

| Area | Decision |
|---|---|
| Source | Embedded chapters first, SponsorBlock API as fallback |
| Scope | Allowlist — nothing is scanned until opted in, per series or season |
| Mapping | Sensible defaults, every category editable, including "ignore" |
| End times | Next chapter's start; ffprobe only when that is unavailable |

## Constraints discovered in Jellyfin core

- `ChapterInfo` stores **only** `StartPositionTicks`. There is no end time in the
  database, so a chapter-derived segment must end at the next chapter's start.
  This is why yt-dlp's filler chapters are load-bearing and must not be deleted.
- Segments are stored **per provider** (`SegmentProviderId`). Ours and Intro
  Skipper's coexist; ordering does not overwrite. Replacing it means disabling it
  for that library.
- `LibraryOptions.DisabledMediaSegmentProviders` and
  `LibraryOptions.MediaSegmentProviderOrder` already give per-library enable and
  ordering. `MediaSegmentManager.RunSegmentPluginProviders` sorts by that array,
  unlisted providers last. **No plugin work is needed for prioritisation.**
- `MediaSegmentGenerationRequest.ExistingSegments` exposes what other providers
  already stored, which we can use to skip emitting overlaps.

## Layout

```
src/Jellyfin.Plugin.SponsorBlockSegments/
  Plugin.cs                                  BasePlugin<PluginConfiguration>, IHasWebPages
  PluginServiceRegistrator.cs
  Providers/SponsorBlockSegmentProvider.cs   IMediaSegmentProvider
  Sources/ISegmentSource.cs
  Sources/ChapterSegmentSource.cs            [SponsorBlock]: chapters
  Sources/ApiSegmentSource.cs                sponsor.ajay.app
  Sources/SegmentCache.cs                    API responses, TTL + negative caching
  Mapping/SponsorBlockCategory.cs            the 10 categories
  Mapping/CategoryMap.cs                     category -> MediaSegmentType | ignore
  Mapping/ChapterLabelParser.cs              "[SponsorBlock]: X" -> category
  Scope/ScopeResolver.cs                     allowlist evaluation, cached
  Scope/VideoIdExtractor.cs                  configurable filename regex
  Api/SponsorBlockController.cs              library tree + per-item preview
  Configuration/PluginConfiguration.cs
  Configuration/configPage.html
tests/Jellyfin.Plugin.SponsorBlockSegments.Tests/
```

Root: `manifest.json`, `meta.json`, `README.md`, `LICENSE`, `.slnx`,
`.github/workflows/release.yml`, `images/`.

`net9.0`; `Jellyfin.Controller` and `Jellyfin.Model` 10.11.3 with
`ExcludeAssets runtime`; `Nullable` and `ImplicitUsings` enabled;
`InternalsVisibleTo` for the test project; config page as an embedded resource.

## Provider

```csharp
public sealed class SponsorBlockSegmentProvider : IMediaSegmentProvider
{
    public string Name => "SponsorBlock Segments";
    public ValueTask<bool> Supports(BaseItem item);
    public Task<IReadOnlyList<MediaSegmentDto>> GetMediaSegments(
        MediaSegmentGenerationRequest request, CancellationToken ct);
}
```

`Supports` is the cheap gate — allowlist check only, no I/O, no network. It runs
per item, so it follows the `SeriesExclusionStore` pattern: a `HashSet` rebuilt
only when the configuration object reference changes.

`GetMediaSegments` resolves the source, maps categories, and returns every
segment. Order of resolution:

1. Read chapters from `IChapterRepository`. If any carry the `[SponsorBlock]:`
   prefix, use them.
2. Otherwise extract a video id from the filename and query the API.
3. If neither yields anything, return empty — never throw, so one bad item cannot
   fail the scan.

## Category mapping

Defaults, all editable per category in the UI:

| SponsorBlock category | Chapter label | Default type |
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

Matching is by exact label after stripping the prefix — not substring regex — so
`Preview/Recap` can never be mistaken for a sponsor.

## Scope

Allowlist. Nothing is scanned until added. An entry is a library, a series, or a
season, stored by `Guid` with its name cached for display:

```csharp
public class ScopeEntry
{
    public Guid ItemId { get; set; }
    public ScopeKind Kind { get; set; }   // Library | Series | Season
    public string? Name { get; set; }
    public string? SeriesName { get; set; }
}
```

An item is in scope if itself, its season, its series, or its library is listed.
A season entry therefore narrows a series without listing every episode.

## Configuration UI

Standard `pluginConfigurationPage` with `emby-*` controls, plus a scope picker
backed by `SponsorBlockController`:

- `GET  /SponsorBlockSegments/Libraries` — libraries with counts
- `GET  /SponsorBlockSegments/Series?libraryId=` — series in a library
- `GET  /SponsorBlockSegments/Seasons?seriesId=` — seasons in a series
- `POST /SponsorBlockSegments/Scope` — add/remove an entry
- `GET  /SponsorBlockSegments/Preview?itemId=` — the segments that *would* be
  produced, with source and category shown per row

The preview endpoint is the important one: it makes the mapping verifiable before
committing to a scan, which is how the Intro Skipper problem would have been
caught immediately.

## Tests

Pure units, no server required: label parsing for all 10 categories, category
mapping including "ignore", scope resolution across library/series/season,
video-id extraction against real filenames, and end-time derivation including the
last-chapter case.

## Open items

- Plugin GUID to be generated.
- API politeness: request timeout, TTL cache, negative caching for videos with no
  data (72 of the 260 in the current library), and a hard off switch.
- Whether to suppress segments overlapping another provider's, using
  `ExistingSegments`. Off by default; only useful when running alongside Intro
  Skipper rather than instead of it.
- The API returns exact start/end times, so API-sourced segments do not depend on
  filler chapters. Chapter-sourced ones do.
