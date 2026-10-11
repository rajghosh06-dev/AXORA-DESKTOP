# AXORA STUDIO-M2-B2A implementation evidence

Date: 2026-10-11. Status: internally verified implementation, ready for external review; not an accepted checkpoint. No staging, commit, push, B2B or B2C.

## Authority and baseline

The B2A task authorizes exactly eleven new and four modified paths. The prior read-only freeze is `AXORA_STUDIO_M2_B2_P0_REPORT.md`, external TEMP directory `AXORA-M2B2-P0-a1mmdap9`; its UTF-8 SHA-256 is `21B2DBD7D7A25E1B97D6C0DF3E8A5560659DB454590D505C056882FAD14A801C`.

Admission was verified locally and independently on the live server: main, HEAD/origin/main/remote main `b091628a1849b39c9f8801f64d4dded50c12445d`, sole parent `248b44b274388731c6c5c20ae8653e04ee8b9031`, subject `feat(studio): checkpoint M2-B1 sandboxed renderer`, ahead/behind 0/0, empty index, clean tracked worktree and no visible untracked source. Initial sandbox DNS restriction was resolved by an authorized read-only remote query before editing.

Archive branch remains `a518f04ba7188a93ec7d7334879c194699db59b9`; tag object `4ea4271029ed7b368b0d86463df264de70f7518b`, peeled commit `a518f04ba7188a93ec7d7334879c194699db59b9`. The unrelated archive file was never opened, hashed, extracted, altered or staged.

## Snapshot and identity contract

`ResumeSnapshotBuilder.Capture` accepts a captured ResumeFile, calls the existing ResumeCodec.Validate, and converts validation failure to safe typed InvalidResumeInput. It neither repairs data nor accesses a store, session, file or runtime. Persistence validation was not copied, forked or relaxed.

The snapshot uses sealed B2-owned records and fresh ImmutableArrays, schema 1 and derivation 1. It preserves ResumeTitle; all ten Header fields; Summary; all eight saved formatting preferences; all seven collections, every entry field and ID; all eight visibility flags and full SectionOrder. Strings are immutable shared values. No original envelope/document/nested persistence record or collection backing storage survives capture. No paths with operational authority, session, store, controls or handles enter the graph. User text that resembles a path remains plain text.

SemanticSha256 covers a versioned canonical encoding of every ResumeDocument value, including hidden content, IDs, raw text, title and preferences. It excludes DocumentId, Revision, timestamps, provenance/recovery and template metadata. SnapshotSha256 binds its own domain, snapshot/derivation versions, DocumentId, Revision, complete semantic payload and derived values. Neither hash includes template choice or render-only font choice.

Only internal constructors admit captured identities/owned canonical data. Canonical byte arrays are private; every public read returns a fresh copy. Returned arrays can be mutated without changing stored bytes or hashes.

## Canonical encoding and schema

Encoding version 1 is explicit, property-ordered JSON; no reflection, dictionary enumeration, default serializer, record ToString, runtime hash or current culture defines identity. The codec's existing reflection-based validation remains validation only. Test-only reflection independently enumerates original fields and performs hostile alias attacks.

Output is ASCII, a strict UTF-8 subset, without BOM, indentation or insignificant whitespace. Printable ASCII except quote/backslash is literal; quote and backslash are escaped. Every control/non-ASCII UTF-16 unit is uppercase `\uXXXX`, with validated surrogate pairs retained as two escapes. Raw Unicode scalars, line endings, NUL, bidi, zero-width, spaces, paths and URL strings are not normalized or sanitized. Integers are invariant decimal without leading zeros; finite doubles use invariant round-trip `R` formatting. No RFC 8785 claim.

Semantic envelope property order: `domain="AXORA.Resume.Semantic"`, `version=1`, `document`.

Snapshot envelope order: `domain="AXORA.Resume.Snapshot"`, `snapshotSchemaVersion`, `derivationVersion`, `documentId`, `revision`, `document`.

Render-data envelope order: `domain="AXORA.Resume.RenderData"`, `renderDataSchemaVersion=1`, `expectedContentPolicyVersion=1`, `snapshotSchemaVersion`, `derivationVersion`, `documentId`, `revision`, `semanticSha256`, `snapshotSha256`, `formattingIntent`, `document`.

Document order: `resumeTitle`, `header`, `summary`, `formatting`, `education`, `experiences`, `skillCategories`, `projects`, `certifications`, `achievements`, `responsibilities`, `visibility`, `sectionOrder`; derived encodings add `visibleSectionOrder`. Within each object, properties follow the explicit statements in ResumeSemanticCanonicalizer.Document. Every current persistence scalar maps to the corresponding lower-initial fixed key; visibility maps ShowSummary etc. into the eight named flags. Semantic encoding excludes derived helpers; snapshot/render-data encodings add bulletLines, skill entries and displayEndDate.

Enums have explicit fixed tokens: onePage/twoPages/threePages/fourPlusPages; segoeUI/calibri/arial/timesNewRoman/georgia; compact/standard/relaxed; summary/education/experience/skills/projects/certifications/achievements/responsibilities. User strings never define property names. The data contains no template program, command, environment, privileged path or action API.

Render data returns privately owned bytes, actual length and uppercase SHA-256. Both canonical snapshot and render data enforce 4 MiB before each write. Exact 4 MiB and plus-one tests exercise both typed limit dispositions without truncation. Input values that violate original persistence bounds are rejected by the existing codec first.

## Derived rules

CRLF is one bullet delimiter; lone CR/LF are delimiters. Empty, whitespace-only, marker/number/markdown/code-fence tokens are retained. Empty raw text derives one empty token. Raw narrative remains separately hashed.

Skills split only on literal comma, retaining empty tokens. Only ASCII space and TAB are trimmed at derived-item edges; quotes, Unicode spaces, newlines and path/markup text remain data. This is not a quoted-CSV parser.

IsCurrent derives the app-owned Present display token, retaining raw EndDate in semantic data and both identities. Noncurrent dates remain literal. VisibleSectionOrder filters SectionOrder by visibility alone; semantic data and all original collection ordering remain intact. Contact labels and URL values remain independent, without inferred href/mailto/tel.

Split counts are checked before allocating output collections, with a shared 100,201-item ceiling. No sampling or clamping.

The optional expected-content matching manifest is intentionally not implemented in B2A. Its reserved policy version is 1 and future ceiling 131,072 items / 4 MiB. No NFC normalization or matching-window generation is performed here. Expected-side generation/extraction remains a later authorized B2C task using these preserved values.

## Formatting and font limitations

HonorSavedPreference is the default. Each of SegoeUI, Calibri, Arial, TimesNewRoman and Georgia yields UnsupportedFormattingPreference with safe code FontUnavailable. Neither formatting resolution nor render-data generation silently chooses an alternative.

The sole explicit opt-in is ExplicitEmbeddedSerifV1, declared intended family Libertinus Serif, with RequiresFontResolutionProof=true. Requested saved preference is preserved. This is a finite formatting intent only: B2A does not prove actual font resolution, embedding, style selection or glyph availability. That remains B2B P3.

One/Two/ThreePage map to targets 1/2/3; FourPlusPages maps to null upper target. No pagination, target-success guarantee, truncation or content dropping is implemented. Compact maps to body/leading/gap 10 pt/0.55 em/0.35 em; Standard 11/0.65/0.5; Relaxed 12/0.8/0.7. Margin retains the exact validated finite double, including one-ULP distinctions; physical units/layout remain later. All three booleans retain exact values. Validated #RRGGBB parses to exact RGB bytes, without source interpolation.

## Targeted and hostile validation

The dedicated STUDIO-M2-RESUME-RENDER group contains 26 implemented B2A cases and **3,980 passing assertions**. No future B2B/B2C case is registered. Coverage includes:

- Every original semantic scalar: 71 independently enumerated single-field mutations change both hashes; hidden fields, visibility and every collection/order are separately tested.
- Excluded envelope metadata, ID/revision binding and template independence; distinct structural/domain encodings; raw normalization/newline/control distinctions.
- B2-owned nested records, original array/record replacements, explicit caller-owned backing-array attacks, live ResumeSession edits after capture, and returned-byte mutations.
- Exact bullet and skill edge cases, current-date semantics and preserved raw values.
- Twenty-one adversarial payload classes placed throughout Header, Summary, all seven collections and URL-like fields, with parsed JSON scalar/collection equality. A 16,384-character token remains complete. All data is synthetic.
- NUL, control characters, ZWJ/ZWNJ, bidi, Hindi, emoji, combining marks, syntax, drive/UNC/device/traversal/package-looking strings; no code/path/action inference.
- All saved fonts, explicit finite choice, every target/density mapping, margins 0.1/0.65/next-double/3, booleans and mixed-case RGB. Exact numeric bits roundtrip.
- Thread-local invariant, en-US, de-DE and hi-IN cultures, restored afterward; UTC conversions from -08:00/+05:30/+14:00 leave bytes unchanged. No global machine settings changed.
- Original invalid envelope/Unicode/collection/formatting cases reuse ResumeCodec; per-field/per-collection/aggregate boundaries and bounded writer failures.

The initial targeted run failed only a test's assumed scalar-count threshold (80); actual inventory is 71. It was corrected before the Phase-1 green gate. Phase 1 then passed 3,868 assertions; additional hostile ordering/backing-array/numeric tests produced the final 3,980. No production failure was hidden.

## Golden vectors

There are 69 frozen triples (207 SHA-256 identities): minimal, full, hidden, reverse order, CR/LF/CSV, adversarial Unicode/control; all 60 target/font/density combinations; and three maximum models. An independently written external Python encoder agrees with all C# triples. Tests contain fixed expected hashes and an explicit minimal semantic byte vector; no golden is generated from production output during tests.

| Vector | Semantic SHA-256 | Snapshot SHA-256 | Render-data SHA-256 |
| --- | --- | --- | --- |
| Minimal | D2F59CB18C70D5C1C770843AF110501621A27F17893740D79FE9ABD52A7C48D0 | 63810D5DA1DF878FA7D58A1E3E395751F35CAFC72018A24C878D71F226DA62A8 | 3BA9A6339563AF563FCA5D7FE3E9B704B7D3B3A4736B52365D7F0A87FA16096B |
| Typical full | 6D49AD763135F7F0469558809C88B51A723B2FD77C319E62A06E90AAD3B1568D | 1C2C95FAD2C926684F459A52C35377AD106FC4A588FBDB750F391DC3DA300FEC | 1B69FE4B0EDAACE57CCFC1A61EA385EF0464C9784E563D7C4F4F752E9E188C33 |
| Max escaping | FE2B10433819E8E1FE1ED8D0EA061AAB8514042C28D2AAA76036204989E1B5EC | B1B4DB68915CD1713E2A182ADAEFD374AD890ABE4B2735E1324364969276D128 | 6DF8D830F3E4F5F04016227CCD00CC6B2136C49F4BB2F84CEADA711698805B3E |

All 69 triples and the reproducible oracle are supplied externally. These identities pin data encodings, not PDF determinism or runtime font behavior.

## Maximum-bound measurements

Each maximum fixture uses exactly 100,000 aggregate semantic UTF-16 units, 200 globally unique entries, and fifty each in Education/Experience/Projects/Skills. Other tests exercise fifty in each of the seven collections separately and reject fifty-one/201-total/100,001-text. Narrative fixtures reach 16,384 units. The legal aggregate prevents all seven collections from simultaneously containing fifty entries.

Final dedicated-run observations, build Debug/x64, this machine:

| Maximum fixture | Semantic bytes | Snapshot bytes | Render-data bytes | Derived line/item total | Allocated bytes | Elapsed ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Newline expansion | 586304 | 870921 | 871543 | 93730 | 31029296 | 38.2066 |
| Comma expansion | 118404 | 403021 | 403643 | 93730 | 12217792 | 23.9223 |
| Escaped controls | 586304 | 1151661 | 1152283 | 150 | 48515224 | 46.0059 |

Allocated bytes are current-thread cumulative allocations for capture plus data writing, not peak resident memory. Timings are observations, not performance guarantees. Combined line/item counts retain empty tokens. External oracle expansion audit separates the counts while proving equal canonical identities.

Conservative bound argument: original aggregate text contributes at most 600,000 escaped bytes. Derived text adds no more than that, except at most 200 constant Present tokens (1,400 bytes); delimiters/empty derived items contribute at most 3*100,201 syntax bytes. Fewer than 2,000 fixed-key scalar properties, bounded IDs already counted in aggregate, seven bounded collections and eight section tokens add less than 100,000 bytes of fixed syntax/metadata, leaving total below 2 MiB even under this deliberately loose double counting. Render envelope adds less than 4 KiB. Therefore 4 MiB remains conservative without rejecting a legitimate maximum. Each write still enforces the hard cap; no bound was silently increased.

## Builds and regression

Fresh MSBuild Rebuild commands for Studio and Studio.Tests used Debug/x64 with normal XAML compilation. Both exited 0 with **0 errors**. Existing warnings remain: seven MVVMTK0045 for Studio; eight for Tests including existing CS0436. Protected warning sources were not edited. No project, package, solution or toolchain change was made.

The original four-group M1 runner passed and validates the seven-group manifest. Standalone existing renderer runner passed 157 assertions with only its manifest expectation changed. Full M2 passed all seven groups exactly once:

| Group | Passing assertions |
| --- | ---: |
| STUDIO-H0 | 207 |
| STUDIO-M1-FLASHCARDS | 172 |
| STUDIO-M1-EXPORT | 289 |
| STUDIO-M1-READALOUD | 156 |
| STUDIO-M2-RESUME | 484 |
| STUDIO-M2-RENDERER | 157 |
| STUDIO-M2-RESUME-RENDER | 3980 |
| Total | **5445** |

Every final ledger has complete=true, failed/missing/duplicate/unknown/blocked=0 and exactly one selected/executed group per process. All published floors are preserved. Twelve malformed argument/group probes exited 2, emitted the exact rejection message, and ran no test ledger. The existing native-child dispatch branch is unchanged.

One earlier full-M2 attempt reused the standalone renderer's single-use native root. B1 correctly rejected existing runtime creation with Win32 error 80; no renderer case executed. That failed log remains in the review pack. Existing `run-m2-renderer.ps1 -PrepareOnly` then provisioned a fresh external root, and a complete M2 rerun passed. No protected source change, ACL repair or deletion of the prior root was used to obtain green results.

B2A acceptance itself never invoked Typst. Only the explicitly required unchanged B1 synthetic regression fixtures ran the authenticated runtime; no Resume was rendered.

## Protected paths, privacy and final freeze

Before editing, 341 tracked WinUI/QA source, UI, project, solution, configuration and script files were SHA-256 inventoried. Final comparison permits only the four dispatcher/runner modifications: all other 337 are unchanged. All existing M2-A production, B1 production, UI/routes, project/package/toolchain paths remain unchanged. Exact Git status additionally excludes changes anywhere else, including MaterialUI/Tools/Mind/W5. The review pack includes all baseline/current hashes, byte lengths and exact authored hashes.

Final source scope is exactly **15 paths: 11 new, 4 modified**, all from the user allowlist. No path 16. The index is empty and all work remains unstaged. main, HEAD, origin/main and live remote main remain the published B1 hash above, ahead/behind 0/0; archive refs are unchanged. Build outputs are ignored generated evidence, not authored payloads. No binaries, PDF, fonts or templates are in the authored inventory/review payload.

Tests use clearly synthetic data and example.invalid addresses only. Public errors/logs expose safe codes, property labels, counts and hashes rather than Resume values. Test-only filesystem paths are synthetic/uncreated or existing regression-owned TEMP paths. Build/repository paths are provenance, not private user document inputs. No real Resume or user document was opened. No raw secrets are included.

External evidence directory: `%TEMP%\AXORA-M2B2A-IMPLEMENTATION`. Review deliverables: `%TEMP%\AXORA-M2B2A-POST-IMPLEMENTATION-AUDIT.zip` and `%TEMP%\AXORA-M2B2A-CHATGPT-REVIEW.txt`. Full current sources, baseline copies of the four modified files, complete bounded diff, schema, goldens, measurement/ledger/dispatch/privacy/protected-hash records and final authored SHA-256 inventory are included. They are verified again after creation.

Bounded self-review: BLOCKER 0 / HIGH 0 / MEDIUM 0. External acceptance remains pending. LOW/deferred: cumulative allocations could be optimized after representative-device measurement; strict versioned encoding changes require reviewed vector/version changes; font/glyph proof belongs to B2B; expected-content matching/parser/worker validation belongs to B2C.

## Explicit exclusions

NO Typst template implemented. NO real Resume PDF rendered. NO font-availability proof. NO PDF validation. NO preview. NO export. NO UI integration. NO ATS. NO LaTeX/Tectonic. NO schema/persistence redesign. NO staging, commit or push. NO B2B/B2C work.

STUDIO-M2-B2A IMPLEMENTATION PASS — READY FOR EXTERNAL REVIEW
