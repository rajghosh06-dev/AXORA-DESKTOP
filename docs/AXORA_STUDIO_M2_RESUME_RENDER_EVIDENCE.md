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

---

## B2B recovery and implementation — 2026-10-11

Published B2A baseline: `9ec7b3224c72ff93d4478ef8940576676df2cd6b`, subject `feat(studio): checkpoint M2-B2A resume render data`, parent `b091628a1849b39c9f8801f64d4dded50c12445d`. The original B2B admission was clean. This continuation deliberately admitted the interrupted, dirty implementation: 9 new and 3 modified allowlisted files, empty index, main/HEAD/origin/live remote at the baseline, ahead/behind 0/0. The intervening authorized publication command reported Everything up-to-date; it published no B2B changes. No reset, restore, checkout, clean, stash, regeneration or baseline overwrite was used for recovery.

Before recovery edits, exact current copies of all 13 allowed paths (including the unchanged B2A evidence document) were preserved outside Git at `C:\Users\rajghosh\AppData\Local\Temp\AXORA-M2B2B-PARTIAL-RECOVERY`. `partial-inventory.json` records every byte length, SHA-256 and changed flag; the review pack includes the complete inventory and preserved sources. Twelve paths were dirty; the thirteenth was unchanged evidence. Recovery template hashes were Simple `0B4190C2EDC3A523E8F918CA1E51488C98489E0068E1970BFA859C6A18428002` / 4218 bytes and Technical `32D0756C1272586FBEECE16F0CCA42882AD2A1FAE97EC38C61B2823624C0821B` / 4246 bytes. These are historical hashes, superseded by the final resource pins below.

The interrupted work was not accepted. Its first build passed, but initial native authentication used an incompatible observer sharing mode; that was repaired before interruption. Historical HTML observation first failed because `--features html` preceded `compile`: exit 2, 157 diagnostic bytes. The parser-only reproduction retained in `historical-html-cli-diagnosis.json` reports `unexpected argument '--features'` and the suggestion to put it under `compile`; it loads no document. The historical command shape was `typst.exe --features html compile --format html --root <owned input> --ignore-system-fonts --package-path <owned packages> --package-cache-path <owned cache> --diagnostic-format short <owned main.typ> -`. Moving the flag corrected CLI admission. A later HTML attempt omitted the header AlignElem, producing 22 missing-field assertions; a block-scoped header had already replaced it in the recovered files. HTML later observed literal strings, but remained unsuitable as the final proof because it is a different layout target. Two broader historical runs exceeded the unchanged 120-second group limit. The last interrupted batched run completed with 4387 passed / 6 failed / blocked 0: a delete-pending formatting operation, an incorrect descendant-helper expectation, and final-authentication exception handling. All historical logs remain separate from final passing evidence.

Recovery repaired final authentication in the B2B compiler: `InvalidDataException` derives from SystemException rather than IOException, so it must be caught explicitly during final reauthentication to permit identity-bound cleanup. The published B1 Expected filter and native process source were left unchanged. The descendant helper exits its root normally while leaving a pipe-holding child; it is not a timeout fixture. Its empty output is rejected as RendererFailed after actual settlement, with no candidate. Delete-pending remains a failure, with closed admission; it was not hidden by a retry or weakened rule. The final repeat runs passed every formatting operation. Independent field-level native observations and cancellation during cleanup were added before the final freeze. A nullable DACL test warning was also repaired within the allowlist.

## Reconstructed completion checklist

| Area | Recovered state | Final evidence |
| --- | --- | --- |
| Catalog/resource embedding/pins | Implemented; final acceptance partial | Exactly two resources, bounded integrity/selection/alias controls; source and embedded hashes agree |
| Sealed package/identity | Implemented | Exact two roles, private-copy mutation tests, independent job-identity recomputation in external audit |
| Workspace extension | Implemented; hostile completion partial | Two-file attacks, supervision/final hash failures, child denial, partial creation, cleanup and final empty operations namespace |
| Compiler/candidate | Implemented; final authentication FAILED | Explicit exception repair; real failure, cancellation-at-promotion and ownership checks pass |
| P1 literal semantics | Historical HTML FAILED, later partial | Replaced acceptance mechanism with native paged metadata query using exact final templates and canonical JSON |
| P3 font/glyph | Preflight and cmap evidence partial | Repeated native inventory, PDF compilation and independent native outline comparison after final template freeze |
| P4 AAP | Implemented, interrupted-run evidence existed but unaccepted | Repeated calibrated read/open trap and both final templates, zero loopback requests |
| Compile matrix/P5/builds/regression | Incomplete; latest interrupted run FAILED | Final targeted 6113; all seven regression groups pass; fresh builds 0 errors |
| Evidence/freeze/review pack | NOT STARTED at recovery | This append, final 13-path inventory, protected hashes and independently verified external review payloads |

## Final resources and catalog

| Template ID / version | Logical embedded resource | UTF-8 bytes | SHA-256 |
| --- | --- | ---: | --- |
| axora.simple / 1 | Axora.Studio.Templates.Resume.SimpleV1.typ | 4496 | 3C58FE12E0AB0A5B65E232FDD6F6F78D29D28678E10DA4F2B02BD7F81EC8D229 |
| axora.technical / 1 | Axora.Studio.Templates.Resume.TechnicalV1.typ | 4524 | 70EDA24913317FB3FDB0CB06DCD3E60CC342ECFD79F4B05CE08EB9EFF325E6D2 |

Both manifests pin render-data schema 1, minimum Typst 0.15.1, renderer compatibility contract 1, and `LibertinusSerifRegularBoldV1`. Display/category metadata and finite formatting capabilities are immutable. The csproj adds only an ItemGroup containing these two explicit resource declarations/logical names. Tests authenticate embedded bytes against these source-byte pins. Catalog loading checks the exact prefixed identity set, duplicate/missing/unexpected names, exact bounded length, extra bytes and SHA-256 before retaining private copies. There is no loose template fallback, custom resource authority or Resume-controlled resource name. Missing, short, oversized, altered and aliased identities are rejected. Caller copies cannot mutate either resource.

Unknown IDs/versions and invalid finite choices are typed UnsupportedTemplate. Unpaired persisted metadata is typed InvalidPresentationSelection. Null/null resolves Simple for rendering only; no persistence write occurs. The explicit embedded-serif opt-in retains the saved requested font, and HonorSavedPreference remains unsupported rather than silently substituting a saved system font.

## Complete source capability and call-graph review

Every line of both final sources was reviewed. Their sole file-consuming call is the exact literal `json("resume.json")`, once per program. Both immediately check AXORA.Resume.RenderData and schema/derivation versions, then the fixed font choice/family. There are no imports, includes, package modules, read/csv/yaml/xml/cbor/toml/image/bibliography/plugin/eval calls, dynamic source/path generation, font paths, process calls or link/action primitives. A tokenized structural audit supplements the manual review; the data property `x.link` is a string, not a `link()` capability.

Call inventory: json and assert establish fixed input/schema; rgb/page/text/smartquote/par/block establish finite layout; literal uses string replace/split/enumerate, linebreak, static metadata and text; row uses filter/len/join, literal and parbreak; bullets uses linebreak or a fixed bullet plus literal/parbreak; section-title uses a fixed names dictionary, upper, literal and an optional static line; fixed loops visit the canonical visible sections and saved collections. Dictionary lookup is confined to validated section enum tokens and app-owned title/visibility maps. No value selects a callable, file, package, label, selector, argument vector or resource identifier. Static metadata has domain AXORA.Resume.LiteralV1, the identical lexical string argument passed immediately to text, and the finite regular/bold weight. It creates no label, path, link or action. The test query's selector is the fixed app-owned `metadata` selector; Resume data never supplies it.

Both templates are self-contained A4 one-column block programs. Header/contact hierarchy, all eight sections, visibility, SectionOrder, collection order, derived bulletLines/skill entries/DisplayEndDate and exact finite formatting inputs are consumed. Technical differs by accented project titles and bold technology rows while retaining saved order. Margin uses inches, body size points, leading/gaps em, RGB exact bytes; divider, centered-header and uppercase-title booleans are honored. TargetPages remains a hint only: no forced page count, truncation, shrinking or content deletion. Hidden sections are intentionally absent visually and remain in snapshot data. URL-like fields go through literal text. The intentional clickable-action allowlist is empty; actual PDF annotation/action inspection remains B2C.

## P1 — native paged literal observation

Preferred option A was implemented instead of relying on HTML. The exact final production template emits invisible static metadata immediately before `text(..., value)` from the same local value and weight; the same reviewed literal primitive is used by both programs. It normalizes CRLF/lone CR into explicit display line breaks, preserving all resulting strings as data. It does not normalize or rewrite the frozen snapshot or canonical JSON. Derived empty bullet lines remain intentional line breaks. Static section titles use the same primitive. No separate synthetic template/program is substituted for observation.

Test-only native command: `typst.exe query --root <owned input> --ignore-system-fonts --package-path <owned packages> --package-cache-path <owned cache> --diagnostic-format short --field value <owned main.typ> metadata`. This uses Typst 0.15.1's default paged target and the published B1 AppContainer/Job process path, exact catalog bytes, exact B2A resume.json and empty private package/cache roots. Query exits 0; the observed 52 diagnostic bytes are the CLI's deprecated-query notice, not a failed observation. Only static-domain regular/bold records are admitted. Query bytes are consumed internally and discarded; logs report disposition, count and hashes rather than raw Resume values.

The native observation proves all current visible field arguments, hidden/reordered sections, collection order, derived lines/skills/Present and supported syntax-looking payloads. Sixteen supported attacker classes cross every field family and URL field through both templates. Four payloads per synthetic document amortize native startup while retaining every class/family/template combination; independent field-suffix checks prevent a single surviving family from masking another missing field. NUL/bidi/zero-width cases receive typed unsupported outcomes. Test jobs run at most four independent operations concurrently with synchronized assertion recording; production compiler admission remains one active operation per instance. The unchanged dispatcher deadline remains 120 seconds. Production PDF compiles and paged-query observations are separately logged. This proves literal dataflow and basic layout intent, not extracted PDF text, complete page layout or PDF validity.

Reference basis: [Typst metadata](https://typst.app/docs/reference/introspection/metadata/), [query](https://typst.app/docs/reference/introspection/query/), and the [pinned 0.15.1 CLI arguments](https://raw.githubusercontent.com/typst/typst/v0.15.1/crates/typst-cli/src/args.rs). The pinned implementation retains query with a paged default; current documentation also describes its eval successor. Production uses neither query nor eval: it uses fixed PDF compile arguments.

## P3 — actual fonts, outlines and conservative glyph policy

Every final native run authenticates Typst 0.15.1 executable SHA-256 `081217A463ADB006F8894B44227FE4B9C9E91FC85F5463D5948E8370DB9BB31E`. It is externally provisioned/test-owned, never PATH-selected or copied into Git. Native `fonts --ignore-system-fonts --variants` runs under B1 ownership and reports embedded Libertinus Serif Normal 400/700/600 and Italic 400/700/600. The final programs use only Normal regular 400 and bold 700, with fallback false, ligatures false, hyphenation false and smart quotes disabled. No italic/bold-italic is requested. Neither system-font discovery nor custom font paths/staging/download occurs.

Inventory alone was supplemented with actual native paged outlines from both FINAL templates. A synthetic one-page header/summary probe uses bold and regular with ASCII, é/Ā/ñ, combining e-acute, bullet, en/em dash, £/¥/€/©/™. Through the same fixed native boundary, `compile --format svg` emits bounded stdout only. An independent external fontTools 4.60.1 verifier decodes the authenticated executable's embedded CFF faces in memory and compares actual SVG glyph contours at the template's 11/19 point sizes. Every one of 36 unique glyph outlines per template matches the selected regular/bold faces; zero unmatched, no .notdef. Both faces have discriminating outlines, and deliberately translated contours are rejected. Contour normalization removes only empty initial moves, explicit implicit-closing edges and mathematically zero-length segments; tolerance is 0.0011 point. No SVG/font/PDF binary is embedded in the review pack: font proof contains hashes, matching glyph names/counts and verifier source only. The analysis library was installed into the external evidence folder, not the app/project/environment, and is not bundled in the review payload.

The executable contains regular/bold cmap coverage of 2382/2185 codepoints, intersection 2185, independently decoded in memory by external and C# checks. Fixed face offsets are 35963824/36300956 in this authenticated executable. Native outline equality establishes the relationship between these selected faces and actual shaping for the probes. Cmap presence is NOT treated as proof of arbitrary script fidelity: production additionally limits admission to Latin U+0020–024F, combining U+0300–036F, Latin U+1E00–1EFF, punctuation U+2000–206F, currency U+20A0–20CF and symbols U+2100–214F, intersected with both cmaps and excluding control/format/private/unassigned characters. TAB/CR/LF are explicit display exceptions. Hebrew/Cyrillic are conservatively rejected despite some cmap coverage. These ranges are an admission policy, not a universal shaping claim.

Both real PDF compiles and paged literal queries preserve the supported synthetic probe values. Hindi/Devanagari, emoji, missing rupee ₹, NUL/bidi/zero-width values are typed UnsupportedRenderContent before native candidate promotion. Snapshot/data stay unchanged; no stripping, replacement or fallback is used. Only visible rendered strings are examined; hidden semantic/raw identity fields remain preserved. Independent PDF text extraction, detailed glyph/page fidelity and multilingual expansion remain B2C or later reviewed work. [Typst text/font selection](https://typst.app/docs/reference/text/text/) and the [pinned SVG glyph exporter](https://raw.githubusercontent.com/typst/typst/v0.15.1/crates/typst-svg/src/text.rs) inform these deliberately bounded claims.

## Sealed package, native compilation and candidate promotion

TrustedRenderPackage has a private constructor and finite snapshot/template/font factory, two private cloned byte arrays, fixed main.typ/resume.json roles, exact lengths and hashes, 64 KiB/4 MiB bounds. It accepts no dictionary, filename, path, raw source, arbitrary collection or font/image resource. RenderJobSha256 hashes ordered AXORA.Resume.RenderJob/version 1 metadata binding template ID/version/source hash, SnapshotSha256, RenderDataSha256 and explicit font/profile. Identical inputs are deterministic; template choice changes job identity without changing B2A snapshot/data identities. External audit independently recomputes both printed job identities from their metadata.

The separate internal workspace factory adds only the two-file package path. The original one-file B1 factory and 4 KiB validation body remain unchanged; its cleanup path only sees a null added data handle. Original B1 regression remains 157. Package creation retains no-write/no-delete-share handles with identity-bound deletion authority; observer handles must share existing write/delete access without granting additional writers. It checks ownership/DACL, non-reparse attributes, single-link identity, exact bounded size, reopened volume/file index and SHA for both roles, plus exactly six namespace entries: input, main.typ, resume.json, scratch, packages and cache. All three directories start empty. The published process calls validation before launch, during supervision and after exit; the compiler reauthenticates again after actual native tree/readers settle.

ResumeTypstCompiler takes only ResumeRenderSnapshot, finite ResumeTemplateChoice and explicit ResumeRenderFontChoice plus cancellation. Its production verifier/process are fixed; internal test infrastructure is not a raw compile API. Fixed args retain root, ignore-system-fonts, owned package/cache paths, short diagnostics and binary stdout. Job assignment precedes resume; AppContainer and zero capabilities are actual native token evidence. Output is bounded at 16 MiB, stderr at the existing 128 KiB, compile at 60 seconds, version at 5 seconds. No output PDF path exists.

Promotion requires authentic package, successful native compile, settled process tree/readers, zero capabilities/Job/AppContainer, exit 0 and nonempty bounded output, final workspace authentication, successful identity deletion and absent namespace. Only then are bytes cloned into internal UnvalidatedResumePdf; native bytes are cleared. Failed/empty-output/canceled/timed-out/security/authentication/cleanup operations return no candidate. Cancel during cleanup after actual native output is explicitly tested; cancellation is checked again under admission arbitration. Unresolved cleanup closes admission permanently and shutdown retains failure; repeated shutdown is stable. Candidate copy mutation cannot change private bytes/hash. Operation/document/revision/snapshot/template/data/job/runtime/font/PDF hashes and length bind the actual input and output. ArtifactStatus is `UNVALIDATED PDF CANDIDATE`. There is no public save path, preview, export or validated/safe authority.

Real compile matrix, both templates: minimal, typical full, hidden/reordered, supported regular/bold Latin/symbols, syntax-looking strings, 16 attacker classes across all families, 24 target/density combinations spanning margins 0.1/0.65/3 and all relevant booleans/accent, plus 16384-character unbroken supported summary. No enormous maximum PDF or exact page count is required. Output evidence contains only operation/hash/size/metadata. Original 26 B2A cases and 3980 assertions remain unchanged; the extended group has 56 required cases.

## P4 — calibrated AAP access observation

Final templates were frozen before repeated positive/negative runs. A fresh external synthetic sentinel receives an AAP read/execute ACE at owned creation, with no explicit renderer SID ACE; no preexisting ACL is changed. The standard renderer AppContainer actually reads SYNTHETIC-AAP in the positive control. An owner-held FileShare.None read handle then calibrates the same child helper: a real external-read attempt fails at open with exit 107. While that trap remains held, attacker strings naming the sentinel and an owned loopback URL traverse every field family through both final programs. Both complete with zero diagnostic bytes; no loopback requests occur. A host connection calibrates/drains the owned listener first. Safe diagnostics expose only counters/codes; the synthetic marker is not exposed by production diagnostics. A supplementary raw-byte marker absence check is explicitly not PDF parsing or the no-read proof.

The causal proof combines an actual readable positive control, a calibrated exclusive-open failure, final successful executions and a complete no-read/no-recovery source/call-graph inventory. This is not ETW/ProcMon tracing and does not assert that all ambient AAP reads are denied. An arbitrary program could catch an open failure; these fixed reviewed programs have no such file-consuming or recovery branch. Package imports are absent, caches remain empty/authenticated, and the native token has zero network capabilities. The known standard AppContainer AAP ambient-read surface still exists; the trusted final template/data boundary is what prevents Resume data from invoking it. Only the owned synthetic sentinel/directory/listener is removed; no final trap artifact remains.

## P5 — two-file attacks and failure settlement

For each fixed role: pinned writes, delete/recreate, rename/namespace replacement and symbolic-link attempts are denied; attempted hard-link creation is denied or single-link authentication rejects it; same-owner pinned-handle tamper fails hash authentication. Pinned input also denies in-place junction mutation. Unknown third file/directory artifacts fail namespace validation and cannot be silently recursively deleted; removing only the test artifact permits owned cleanup. Actual AppContainer probes cannot add a file or obtain write/delete/owner/DACL control on either existing role. Test-only owner-handle corruption immediately before native resume exercises supervision and final reauthentication for both files, returning UnexpectedArtifacts/no candidate with complete cleanup.

Partial creation after main, after data and after full package/before execution unwinds without candidate or residual operation. Cleanup while an actual owned native child is alive refuses deletion; cancel settles tree/readers first. Retained zero-access share handles on EACH file force typed DeletePending, no candidate, closed admission and failed shutdown; releasing only the synthetic handle permits final owned cleanup. Native hang/flood/stderr/crash and root-exit/pipe-holding-descendant probes return no candidate and settle; the descendant case records actual process count at least two. Injected settlement failures and real cleanup faults also cannot promote bytes. Final acceptance asserts the entire owned operations namespace is empty, with no live, pending or unknown operation entry.

## Final builds, regression, scope and privacy

Fresh Studio and Studio.Tests Debug/x64 Rebuilds exited 0, zero errors, normal XAML compilation. Final logs: build-final-studio.log and build-final-tests-strengthened.log. Only existing warnings remain: Studio seven MVVMTK0045; Tests the same seven plus existing CS0436. The temporary B2B CS8604 warning was fixed. The dedicated runner passed 6113 assertions; existing renderer runner passed 157; the four-group M1 runner passed. Final full M2 passed all seven groups exactly once:

| Group | Passing assertions |
| --- | ---: |
| STUDIO-H0 | 207 |
| STUDIO-M1-FLASHCARDS | 172 |
| STUDIO-M1-EXPORT | 289 |
| STUDIO-M1-READALOUD | 156 |
| STUDIO-M2-RESUME | 484 |
| STUDIO-M2-RENDERER | 157 |
| STUDIO-M2-RESUME-RENDER | 6113 |
| Total | 7578 |

All final ledgers: complete=true, failed/missing/duplicate/unknown/blocked=0. Manifest remains exactly seven groups. Twelve malformed dispatch probes exit 2, emit the exact rejection message and no ledger. Neither dispatch nor runner scripts were modified. Full M2 reruns use a fresh provisioning root rather than reusing the renderer runner's single-use root. Native query timings are machine observations only, not a product latency guarantee; the external report includes bounded native durations and actual candidate size ranges.

Final scope is exactly 13 authored paths: 9 new, 4 modified, all allowlisted, unstaged, index empty. The existing 616-file protected baseline is rehashed and checked with zero mismatches; exact Git scope independently excludes any other tracked/untracked source changes. All B2A/M2-A production, B1 native sandbox/process/Job/verifier/coordinator/session/bootstrap/app, UI/routes, Program/runners, solution/package/toolchain and unrelated projects remain frozen. The evidence document's original 17675-byte B2A prefix is preserved byte-for-byte. main/HEAD/origin/live remote remain the published B2A hash, ahead/behind 0/0; frozen local and remote archive refs are unchanged. No B2B stage, commit or push occurs. The unrelated archive is not opened, hashed, extracted, modified, removed or staged.

Only synthetic Resume data and regression-owned TEMP resources are used. Production diagnostics retain codes/counts rather than Resume strings or raw private stderr. Native output buffers are internal and cleared. Query/SVG are test observations, never preview/export features. External review ZIP/TXT contain source, bounded diff, baseline copies, recovery hashes, call/capability audit, native metadata, font proof, AAP/P5 evidence, builds, ledgers, dispatch controls, privacy/protected audits and final SHA inventories. They contain no runtime executable, PDF bytes, font files or real Resume. Deliverables are `C:\Users\rajghosh\AppData\Local\Temp\AXORA-M2B2B-POST-IMPLEMENTATION-AUDIT.zip` and `C:\Users\rajghosh\AppData\Local\Temp\AXORA-M2B2B-CHATGPT-REVIEW.txt`; payload bytes and hashes are verified after generation.

Hostile final reread: BLOCKER 0 / HIGH 0 / MEDIUM 0 within this internal B2B boundary. LOW/deferred: conservative Latin admission intentionally rejects other scripts; cmap plus probe outlines does not replace per-document B2C fidelity; deprecated query is a pinned-runtime test mechanism with a later migration cost; repeated hashing and private copies have a bounded performance cost; standard AppContainer ambient AAP reads remain a known surface requiring fixed trusted programs; install/provisioning, PDF structural/action/text/page validation and real UI flows remain later separately authorized work. External acceptance is still pending.

PDF bytes are UNVALIDATED. NO PDF parser or validation worker. NO independent PDF validation. NO preview. NO Save As/export. NO UI integration. NO ATS claim. NO LaTeX/Tectonic. NO custom/user templates. NO persistence change. NO staging, commit, push or B2C.

STUDIO-M2-B2B IMPLEMENTATION PASS — READY FOR EXTERNAL REVIEW


## 2026-10-14 - B2B-R1 deterministic shaping refinement (fast-track A1 in progress)

The previous TEMP-only Phase-A contract closure stopped before source edits: 68 synthetic candidates / 86 pages, with 184 of 188 base-cmap glyph comparisons matching and four legitimate f.short contextual mismatches. The prior exact source-program experiment matched all 192 subset programs including .notdef. H1 and M1 were open; that history is retained rather than represented as an accepted checkpoint.

The subsequent fast-track task explicitly authorized a TEMP calt=0 proof, then a minimal five-path B2B-R1 reopening. Authenticated GSUB calt reaches lookups 49 and 50 in both Regular/Bold faces. Source-derived contexts exercise f.short and Q.u with punctuation, spaces and boundaries; f_f/q.sc branches have no admitted base-cmap input under the fixed non-ligature, non-small-caps profile. Eighteen original-template positive controls actually select both authenticated alternates in both roles.

A0 PASS: the original 68 cases plus 38 additional shaping/scalar cases produced 106 candidates / 156 pages using authenticated Typst 0.15.1 through the unchanged V2 AppContainer/Job compiler. The 100 accepted candidates passed 136,240 raw-show-code/resource/CID/CFF-program/Unicode/advance checks (11,788 unique chain records); zero calt-selected alternate programs remain. Six TAB/decomposed-combining cases are explicitly typed B2C_V1_UNSUPPORTED_CONTENT. Precomposed Latin is separately proved; no persisted text is normalized. Decomposed e + U+0301 still selects one authenticated eacute source glyph with a two-scalar ToUnicode sequence, so a general shaping engine is intentionally excluded from v1. Profile unchanged; all matrix compiler operations settled and cleaned.

R1 changes only both global text configurations by adding features: (calt: 0), the exact catalog lengths/hashes, this chronology, and the reviewed-source regression guard. Template IDs/versions remain axora.simple / 1 and axora.technical / 1. Font family, regular/bold roles, sizes, colors, layout, data capabilities, visibility, ordering, links, ligatures:false, fallback:false, hyphenate:false and smartquote-disabled semantics remain fixed.

| Template | Final UTF-8 bytes | Final source SHA-256 |
| --- | ---: | --- |
| SimpleV1 | 4517 | 02EB1F658B5BC682A69903941D7B58BBBD8C00E24C09BE251C62055A39254805 |
| TechnicalV1 | 4545 | E4AC679E817B4C9EA87142477525AB19B470980D6F718F3B4A4FB9D48DAB6B26 |

Untouched Debug/x64 Studio + Studio.Tests rebuild exited 0. The untouched ResumeRender ledger is complete with 6,113 passed / zero failed, missing, duplicate, unknown or blocked. Existing MVVMTK0045 AOT-source-generator warnings and WindowsAppSDK-generated CS0436 NativeMethods collision warnings are pre-existing; no protected ViewModel/SDK source was changed. R1 P1/P3/P4, real final-resource compiles, catalog integrity/ownership/candidate identity and Renderer regressions are pending at this chronology entry. H1/M1 closure and B2C production implementation remain gated; no B2C artifact authority is claimed.

External A0/recovery evidence: %TEMP%/AXORA-M2B2C-FASTTRACK-20261014-232705. Only synthetic fixture data; no real Resume, preview, Save As, export, UI route, ATS, PDF/A, accessibility, universal multilingual or LaTeX/Tectonic claim. No staging, commit, push or B3.


A1 acceptance completed against the FINAL R1 embedded resources: dedicated ResumeRender runner complete / 6,115 passed / zero failed, missing, duplicate, unknown or blocked; unchanged Renderer runner complete / 157 passed with the same zero-failure ledger fields. Final resource tamper/ownership and candidate-identity cases, P1 literal-data observations, P3 embedded font/runtime proof, P4 calibrated AAP-sentinel negative proof and actual Simple/Technical compiles all execute within that complete named-case group. Both Debug/x64 builds exited 0; warning codes match the untouched baseline. The five-file R1 gate is PASS; this does not close H1/M1 or authorize B2C production code before A2/A3 pass.

## 2026-10-15 accelerated recovery: A1 final freeze and B2C1 foundation draft

The prior missing-default-runtime stop is preserved in external recovery records. Five existing copies in the three known AXORA TEMP labs authenticated to the exact approved Typst 0.15.1 executable hash. The P6 runtime-source copy was reused, without download or unrestricted execution, at `%LOCALAPPDATA%/AXORA/DevTestAssets/Typst/0.15.1/typst.exe`. Frozen runners use their existing `-RuntimeSource` interface; no script, B1 policy or production Typst verifier change was made.

Fresh A1 Debug/x64 Studio and Studio.Tests Rebuilds exited zero. Targeted ResumeRender: 6115; Renderer: 157; M1: 207/172/289/156. The fresh full seven-group M2 run completed exactly once per group with H0 207, Flashcards 172, Export 289, ReadAloud 156, Resume 484, Renderer 157 and ResumeRender 6115. Every ledger is complete with zero failed/missing/duplicate/unknown/blocked entries. Twelve malformed-dispatch probes passed. All five recovered A1 raw identities remained exact at freeze. Retaining template version 1 identifies the persisted presentation choice; exact source hash is independently authenticated and included in render-job and candidate identity, with no persisted PDF or render-output cache in the reviewed pipeline.

A1 FINAL REGRESSION PASS authorizes only the expressly revised B2C1 foundation sequence. H1 and M1 remain OPEN and mandatory before B2C3 validated-artifact promotion. No previous hostile font/PDF corpus, incomplete oracle suite or visibility attack suite is resumed.

B2C1 DRAFT (acceptance pending): fixed self-contained win-x64 .NET 9 worker; PdfPig exactly 0.1.16 only in worker; Windows SDK projections; fixed binary foundation IPC with exact EOF, limits, versions and request/PDF/identity/bundle hashes; build-generated host-embedded manifest; exact-file-set and same-handle bundle authentication; explicit fresh-only provisioning; empty read-only operation workspaces; unchanged V2 AppContainer and Job objects; bounded duplex settlement and closed admission after unresolved cleanup. Dependency load/one bounded native raster is a foundation observation, not structural, font, content or all-page validation. No ValidatedResumePdf, artifact promotion, preview/export/UI route or public PDF trust authority is introduced.

External task evidence: `%TEMP%/AXORA-B2C1-ACCELERATED-20261015-192724`. Implementation and final integration/build/test/audit gates are pending; this draft entry is not an accepted checkpoint. Original A0/A1 history remains intact. No staging, commit, push, B2C2/B2C3 or B3.
## 2026-10-15 B2C1 restore recovery and final foundation acceptance

The accelerated draft's worker restore failure is retained: NU1505 duplicated Microsoft.Windows.SDK.NET.Ref downloads at 9.0.20 and 10.0.26100.57; NU1102 rejected nonexistent Windows targeting pack 9.0.20. A complete 16-source recovery copy, raw hashes, full patch, Git state and accepted A1 evidence were captured before repair in `%TEMP%/AXORA-B2C1-RESTORE-RECOVERY-20261015-204033`. The accepted pre-B2C1 A1 full regression remains 7580 assertions plus twelve malformed dispatch probes. The four frozen A1 code/template files remain byte-identical; prior evidence text remains intact.

Actual .NET SDK 9.0.318 MSBuild evaluation demonstrated the global RuntimeFrameworkVersion override being applied to the Windows reference as well as .NET. The worker removes that global override and the unnecessary WindowsSdkPackageVersion override. SDK self-contained patch selection resolves Microsoft.NETCore.App.Runtime.win-x64 9.0.20 and Microsoft.Windows.SDK.NET.Ref 10.0.26100.57, with no direct SDK-ref PackageReference, ignored sources, floating package, SDK upgrade or warning suppression. PdfPig remains exactly [0.1.16] in the worker alone. Resolved official package SHA-256 identities and independently verified SHA-512 receipts are retained externally. The build checks its resolved versions and fixed Windows x64/multi-file/non-AOT/non-trimmed shape rather than silently relabeling different runtime bytes.

The original nested Visual Studio build used a different Roslyn compiler from direct SDK publish, changing worker DLL/PDB identities. Studio now publishes the worker through the installed .NET host from NetCoreRoot, without PATH tool lookup; WinUI retains its normal compiler/XAML pipeline. A final fresh SDK Rebuild/Publish reproduces the exact canonical worker manifest SHA-256 ABDE46D6FE0A5A3158962D4ABD7220DFFD656E1CAE7A3E794EB611CDA93312DC. The bundle has 203 flat files and 110819663 total bytes. Every name/length/hash was independently checked. PE-resource inspection verifies Studio embeds that exact generated authority. There is no adjacent mutable manifest authority or circular worker-to-Studio dependency. Worker, Studio and Studio.Tests Debug/x64 builds pass with zero errors. The temporary generated-task CS0162 warning was corrected in source; only existing MVVMTK0045/CS0436 warnings remain.

Foundation implementation uses bounded, versioned binary frames with exact EOF, owned PDF copies, checked lengths and request/operation/PDF/input-identity/bundle binding. Responses contain only finite internal dependency observations. Zero-measurement success and unknown statuses/codes are rejected. Native isolation indicators default to false; a live-process count overrides an inconsistent settled flag. Runtime authentication retains exact file/ancestor pins through settlement, rejects set/length/hash/reparse/hardlink mismatches and provisions only a fresh explicit destination. The host uses unchanged V2 AppContainer and B1 Job objects, suspended launch, Job assignment and token inspection before resume, zero explicit capabilities, fixed executable/arguments/environment, bounded duplex streams, cancellation and settlement. Unresolved input/process/cleanup ownership preserves resources and closes admission. No unrestricted fallback or production startup provisioning is added.

The twelve existing named B2C1 cases all pass in the TEMP isolated harness: 103 assertions, zero failed/missing/duplicate/unknown/blocked cases. An initial targeted run exposed the test helper's omitted InvalidDataException classification; the expected rejection type was added without weakening production behavior. An initial full run then exceeded the unchanged 120-second group deadline while repeatedly copying the full runtime for negative namespaces. Negative set/pin/first-metadata identity fixtures now require the exact production rejection code and avoid redundant 111 MB copies; real complete-bundle provisioning and both genuine B2B candidate/native dependency probes remain. No case, assertion floor or timeout was removed or relaxed. Per-case machine timings and both failed and final passing logs are preserved.

Final dedicated ResumeRender: 6227 assertions; Renderer: 157; M1: H0 207 / Flashcards 172 / Export 289 / ReadAloud 156. The final post-B2C1 full M2 run executes all seven groups exactly once: H0 207, Flashcards 172, Export 289, ReadAloud 156, Resume 484, Renderer 157, ResumeRender 6227; total 7692. Every ledger is complete with zero failed/missing/duplicate/unknown/blocked entries. All twelve malformed dispatch probes pass with exit 2, exact rejection text and no test ledger. Program.cs and all published runners remain unchanged; fresh preparation contexts use the reverified persistent Typst test asset and its exact approved 081217A4...9BB31E hash in the same shell as dependent execution.

B2C1 is FOUNDATION ONLY. PdfPig load and one bounded in-memory Windows native raster demonstrate dependency integration, not structural, used-font, visible-content or all-page validity. No ValidatedResumePdf type, promotion path, preview/export/save destination, public trust authority or UI route exists. H1 remains OPEN; M1 remains OPEN. The restricted hostile font/PDF corpus, incomplete oracle suite and visibility attack suite were not resumed. No B2C2/B2C3/B3 work occurred. Manual source/dependency review found no remaining BLOCKER/HIGH/MEDIUM within this foundation contract; this is not independent external acceptance or a PDF-safety verdict.

Remaining LOW operational debt: reviewed SDK/runtime versions require explicit future update review; installer/update/uninstall and UI integration remain outside this private foundation; intentionally quarantined unknown-ownership resources require explicit operator recovery and are never silently deleted. Native durations are machine observations, not device-wide latency guarantees.

The combined authored union remains exactly sixteen paths, all unstaged. All unrelated tracked hashes and the four frozen A1 sources are unchanged; B1 native/verifier/workspace, B2A, B2B compiler/candidate, UI/routes, legacy/MaterialUI/Tools/Mind, dispatcher/runners and global toolchain/configuration are protected. Main/HEAD/origin/main/live remote remain 7c57cf89a66b5aa922d904d15789fd2803164fed, ahead/behind 0/0, index empty, archive refs unchanged. No reset/restore/clean/stash/stage/commit/push or protected ZIP interaction.

External review deliverables: `%TEMP%/AXORA-B2C1-RESTORE-RECOVERY-REVIEW.txt` and `%TEMP%/AXORA-B2C1-RESTORE-RECOVERY-AUDIT.zip`. They include all sixteen current sources, before-repair and baseline copies, full bounded diff, restore/build/version/manifest evidence, targeted and seven-group ledgers, failure chronology, protected hashes and final Git identities. They exclude runtime executables/DLLs, font/PDF binaries and real user Resume content.

AXORA A1+B2C1 IMPLEMENTATION PASS — READY FOR EXTERNAL REVIEW. This disposition covers the non-validating foundation only. STOP before B2C2/B2C3/B3.

## 2026-10-15 A1+B2C1 targeted acceptance reconciliation

Admission independently reverified main/HEAD/origin/main/live remote at `7c57cf89a66b5aa922d904d15789fd2803164fed`, ahead/behind 0/0, empty index and exactly eight tracked modifications plus eight untracked sources. All sixteen admission hashes matched the previously passing implementation ZIP receipt and the latest read-only recovery. The four frozen A1 code/template files remain byte-identical. Current source, retained raw ledgers and inert assembly metadata were inspected; no blocked font/PDF corpus was resumed.

The assertion increase is fully reconciled: 6115 + 9 + 103 = 6227. The unchanged `pure-value-graph` case reflects public instance properties of all non-enum/non-exception types in the snapshot model namespace. `PdfFoundationResult` adds nine generated public record properties despite its internal type visibility; the raw property assertions increase from 122 to 131. Existing assertions therefore total 6124 before the twelve foundation cases add 103. Both source counting logic and PE metadata independently confirm the nine properties; no assertion is unexplained.

A concrete MEDIUM test defect was found in the optimized identity-negative fixtures: later empty bundle stubs could supply a length rejection with the same expected message if SHA comparison were missing, masking the intended same-length tamper. Only `ResumePdfValidationTests.cs` was corrected: length/same-length cases share one fully provisioned authenticated bundle and restore the first original metadata file before each mutation. Set/hardlink fixtures retain their earlier phase-specific namespaces. An independent hash inventory of the actual retained same-length fixture proves all 203 lengths match, 202 untouched file hashes authenticate, and only the intended first metadata hash differs. No production security code, dependency, case/assertion count, timeout, or frozen A1 source changed.

Because this correction changed executable test source, fresh Debug/x64 Worker, Studio, Studio.Tests and TEMP isolated-harness builds were run and exited zero. Only existing MVVMTK0045 and generated WindowsAppSDK CS0436 warnings remain. Fresh isolated foundation: 12 cases / 103 assertions. Dedicated ResumeRender: 6227; Renderer: 157; M1: 207/172/289/156. Fresh full M2: Resume 484, H0 207, Flashcards 172, Export 289, ReadAloud 156, Renderer 157, ResumeRender 6227; total 7692, seven groups exactly once, complete with zero failed/missing/duplicate/unknown/blocked fields. Twelve malformed-dispatch probes pass. Approved persistent Typst identity and fresh native preparation contexts remain recorded; the original 120-second group deadline was unchanged.

The source audit distinguishes actual native worker/Typst operations, actual bundle/workspace verification, controlled process simulations, and non-executable filesystem fixtures. The original timed-out full run remains incomplete historical evidence; optimized failure-predicate equivalence was not accepted until the masking defect was repaired. Native flood/memory-pressure/descendant/deadline failure coverage is not claimed from the controlled unit cases. Remaining LOW items cover quarantine reclamation, synchronous prelaunch I/O timing, reviewed version/provisioning updates, and those bounded empirical-coverage limits.

External handoff: `%TEMP%/AXORA-A1-B2C1-ACCEPTANCE-REVIEW.txt` and `%TEMP%/AXORA-A1-B2C1-ACCEPTANCE-EVIDENCE.zip`; working evidence `%TEMP%/AXORA-A1-B2C1-ACCEPTANCE-20261015-230947`. The resolved MEDIUM finding, source locations, exact final source hashes, full patch, fixture-isolation proof, actual/simulated classifications, historical/fresh ledger provenance and protected/Git audits are included. H1 OPEN; M1 OPEN; NO VALIDATED PDF; NO B2C2/B2C3/B3; NO preview/export/UI. This is a bounded acceptance candidate, not independent PDF validity or product completion. All sixteen paths remain unstaged; no commit or push. STOP.
