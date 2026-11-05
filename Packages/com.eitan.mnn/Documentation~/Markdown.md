# Managed Markdown rendering

`MNN.Unity` contains the shared `MNNMarkdownDocument` and `MNNMarkdownRenderer` types in
`Runtime/Core/Language/Markdown`. They are pure C# and have no native export, P/Invoke,
MNN model, or Unity UI dependency. Editor tools and Player components can therefore parse
the same reply text and render it into their own controls. `MNNMarkdownRenderer.Render` returns
one `MNNMarkdownRenderResult` containing the document AST, Unity Rich Text, and discovered
image metadata so callers do not repeat parsing for measurement and binding.

The parser follows the CommonMark shape: a first pass recognizes block structure, then a
delimiter stack resolves inline emphasis while code spans and links remain opaque. The
reply surface supports ATX/Setext headings, paragraphs, fenced/indented code, ordered and
unordered lists, block quotes, thematic breaks, emphasis, strong emphasis, inline code,
links and image tokens. Fence lengths and blank lines are preserved; ordered lists retain
their start number; list continuations and empty items remain in their list. Block and inline
images share one destination parser for escapes, balanced parentheses, angle brackets and
optional titles. Unsupported syntax remains readable text. This is a supported subset, not
a full CommonMark/GFM implementation. Image destinations accept only `http`, `https`, and `file`.

The legacy uGUI/IMGUI output uses supported `b`, `i`, `color` and absolute `size` tags.
Links use color; `~~deleted~~` text uses a muted color because legacy Text does not support
underline or strike tags. Literal `<` characters receive an invisible zero-width separator
to prevent source tags from executing, while ampersands remain visible characters. Markdown
entities are decoded outside code. HTML entity escaping such as `&lt;` is not a Unity text
escape. `Render(markdown, baseFontSize)` supplies the caller's font size for heading scales;
the existing overload defaults to 20.

Use `Render(markdown, baseFontSize, MNNMarkdownTheme.Light)` or `.Dark` to match the
host surface. `Render(document, baseFontSize, theme)`, `ToRichText(sourceOrBlock, theme)`
and `RenderSlices(markdown, maxCharacters, baseFontSize, theme)` share the same palette.
The renderer stays independent of Unity UI and does not detect system appearance or mutate
a global theme. Existing overloads retain their dark-theme output. Normal text, headings and
list markers inherit the host control's foreground; the caller must theme that foreground
and its background together. Explicit code, link/image-marker and muted colors have separate
light/dark values, checked at a minimum 4.5:1 contrast against the supported chat and Studio
surfaces, including the glass shader's readable-surface luminance bounds.

SiriAI passes its effective System/Light/Dark preference into the shared renderer and includes
the resulting theme in its message cache. A theme change rebinds the current page without
waiting for another streaming token, preserves reading position, and retains loaded image
textures and their aspect ratios. An unchanged theme reuses render results. MNN Studio follows
`EditorGUIUtility.isProSkin`, refreshes its cached document when the skin changes, and keeps
fenced code literal for selection/copying. Its rich GUIStyles are cached per editor skin.

Fenced code uses its first info-string word for lexical syntax highlighting. There are
21 shared profiles:

| Language family | Fence names |
| --- | --- |
| C# | `cs`, `csharp`, `c#` |
| C/C++ and shaders | `c`, `h`, `cpp`, `c++`, `cc`, `cxx`, `hpp`, `hxx`, `hlsl`, `glsl` |
| JavaScript/TypeScript | `js`, `javascript`, `mjs`, `ts`, `typescript` |
| Python | `py`, `python`, `python3` |
| JSON | `json`, `jsonc` |
| Shell | `sh`, `bash`, `shell`, `zsh` |
| SQL | `sql` |
| Java | `java` |
| Kotlin | `kotlin`, `kt`, `kts` |
| Swift | `swift` |
| Go | `go`, `golang` |
| Rust | `rust`, `rs` |
| Dart | `dart` |
| Lua | `lua` |
| Ruby | `ruby`, `rb` |
| PHP | `php` |
| R | `r` |
| HTML/XML | `html`, `htm`, `xml`, `xhtml`, `svg` |
| CSS | `css` |
| YAML | `yaml`, `yml` |
| TOML | `toml` |

Language aliases ignore case; extra info such as `cs title="file.cs"` does not change the
language. Missing or unknown languages keep the literal base code color. Inline code has
no language marker and retains its base code color.

`MNNMarkdownRenderer.ToCodeRichText(code, language, theme)` exposes the same pure C#
highlighter to other Editor/Player controls. Keywords, strings, numbers, comments and JSON
property names use distinct theme colors. A forward scanner consumes opaque strings/comments
before recognizing words and numbers; keyword lookup compares source ranges without allocating
per-token strings. Immutable profiles own their comment, quote and number rules. Scanners
handle C# verbatim/raw strings, C++ raw strings, Python/Java/Kotlin/Dart/TOML triple quotes,
Go raw strings, Rust/Swift hash-delimited strings, Lua long brackets and Ruby block comments.
Rust lifetimes and escaped identifiers remain distinct from characters and keywords. Nested
comments use an iterative depth counter. Numeric support includes hexadecimal floats,
Rust suffixes and CSS units/colors; range/concatenation dots remain outside numeric tokens.
Markup tags, attributes, comments and CDATA remain escaped literal source. YAML keys,
quoted values and indented block scalars have separate boundaries, and TOML multiline strings
retain their optional content quotes at the closing delimiter.

Interpolated strings are colored as one string. This lightweight lexer does not implement
a compiler, semantic type analysis, JavaScript/Ruby regular-expression literals, heredocs,
Ruby percent literals, R raw-string delimiters, or embedded JavaScript/CSS/PHP inside HTML.
Complex configuration values receive lexical colors, not schema or full YAML/TOML parsing.
Unfinished streaming tokens remain visible; no source is executed. The renderer never
interprets Markdown or source rich-text tags inside code. Fenced blocks are highlighted
before message slicing, so color scopes remain balanced and multiline token colors survive
chunk boundaries. Slices keep surrogate pairs, CRLF and literal `<` escape pairs together.

Studio stores literal code separately from highlighted display text: the **Copy code** button
uses the original source. Highlighted display strings and GUIStyles are cached until the
message or skin changes. Both themes have syntax-color contrast tests on supported surfaces;
legacy Unity TextGenerator tests verify visible glyph widths and the colors actually emitted.

SiriAI's uGUI adapter converts inline syntax to Unity's safe rich-text subset and gives each
image token a bounded texture slot. Player coroutines and the Editor update poller share
`SiriAIMarkdownImageRequest`. Its core assembly detects `com.unity.modules.unitywebrequest`
with an asmdef version define: when available it uses `UnityWebRequest.Get` to download
bytes, without requiring `com.unity.modules.unitywebrequesttexture`. Without the base request
module, native Players and the Editor use a reused managed `HttpClient` for HTTP(S) and an
asynchronous `FileStream` for file URLs. Texture creation and `ImageConversion.LoadImage`
stay on Unity's main thread and require `com.unity.modules.imageconversion`, already declared
by this package. WebGL without UnityWebRequest retains the alt placeholder because the
managed socket fallback cannot provide browser networking.

Both transports have a 20-second timeout and cancellation. Completed encoded images above
16 MiB are rejected; UnityWebRequest also aborts oversized downloads when polled, which does
not strictly bound peak receive-buffer size. Decoded textures above 16M pixels are rejected
after decoding, so this check does not bound decode-time allocation. Failed or canceled
requests preserve the alt placeholder; stale Editor subscriptions are removed before decoding.

`MNNMarkdownImageCache` owns decoded runtime textures on Unity's main thread. SiriAI's Player
and Editor adapters share one instance, with a default LRU limit of 32 cached URLs and 64 MiB
of estimated native texture bytes. `TryStore(key, texture, out lease)` transfers texture ownership
after argument validation, including on rejection: the cache destroys oversized or redundant
downloads. `TryAcquire(key, out lease)` retains another displayed binding. A binding releases
its lease when hidden, rebound or destroyed; borrowed external textures retain their caller's
ownership. Cached eviction, `Clear`, and idempotent `Dispose` release cache ownership; an image
still displayed by a lease survives until that last lease is disposed. Dispose on the creating
thread, and never manually destroy a texture transferred to the cache.

The estimate uses Unity's reported native allocation with an RGBA mip-size floor. It is
platform-dependent accounting, not an exact GPU-memory measurement. Active leases can retain
evicted images beyond the cache budget; `ResidentEstimatedBytes` and `ActiveLeaseCount` expose
that retention separately from `CachedEstimatedBytes` and `CachedCount`. Applications control
the number of visible image slots. Counters track ownership; Player texture destruction follows
Unity's deferred end-of-frame semantics. SiriAI clears cached ownership on scene unload and disposes
the shared cache on application quit or Editor domain reload; hidden slots restart their binding
when shown again. No encoded-image transport or image decoder was moved into this cache API.
Transcript paging bounds live UI objects. `MNNMarkdownRenderer.RenderSlices` parses a complete
message before splitting rendered text into balanced slices of at most 1024 text units (and
32 lines), excluding generated tags. This preserves code/emphasis/link context and surrogate
pairs across UI boundaries. Images carry marker offsets and are assigned to one slice.
SiriAI caches the current render result per message, font size and theme (at most 256 entries),
replacing the previous streaming snapshot rather than retaining its full history in the cache.

Regression tests live in `Tests/Editor/Language/Markdown` and the SiriAI presentation tests.
Image ownership checks also live in `Tests/Runtime/Language/Markdown`: PlayMode verifies that
eviction preserves displayed leases and final release uses Unity's deferred destruction.
SiriAI's Runtime presentation test hides an in-flight loopback image request and shows the
same URL again, exercising the actual Player coroutine and native texture release.
Markdown parsing checks include per-language streaming-prefix checks, seeded malformed-input sweeps, deep
nested comments, quote/hash/bracket/scalar-marker stress cases, concurrent themes, image-marker
positions, slice boundaries and 120 adapted CommonMark emphasis examples. Runtime-facing
TextGenerator checks exercise colors and glyph widths in both themes for multiple languages;
performance tests report render timings separately from UI layout. The example attribution and license
are recorded in `Tests/Editor/Language/Markdown/CommonMark.NOTICE.md`. SiriAI also verifies
visible output using Unity's actual `TextGenerator`.

This optional-module handling applies to the SiriAI Markdown image subsystem. Other existing
MNN audio/model-download features keep their own declared UnityWebRequest dependencies.

The implementation intentionally avoids adding Markdig or another binary dependency to the
package. Markdig remains the reference point for a full CommonMark/GFM feature set; if tables,
task lists, footnotes, or lossless round-tripping become product requirements, the public AST
boundary is the place to add an opt-in extension without changing the uGUI or MNN interop
layers.
