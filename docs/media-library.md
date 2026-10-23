# Built-in background library

The desktop library contains 272 choices, all at 1280 × 720:

- 80 researched themes (60 core, 20 optional), each with one AI-generated photograph-style image, one painted illustration and one abstract image: 240 still images.
- 10 original procedural graphics, each in daylight and night lighting: 20 still images. Rain has stronger, larger visible drops and ripples.
- The original five-song pilot: 10 still images now grouped by their actual subjects (mountain paths, wheat, eagles, sea and lakes) under the regular photo and abstract styles. There is no separate sample category. Their IDs and file paths are preserved so saved backgrounds still open; historical pilot files outside the app library are unchanged.
- Two additional style pilots: a soft watercolour prayer scene and a sculpted gold crown. The new style filters currently contain one sample each; see [style review and prompts](search-and-style-review.md).

`assets/media/catalog.json` is the app manifest. Files live under `themes/`, `generic/` and `legacy/`. Installed builds preserve these folders under `Media/`. No cloud storage, image-search API, website scraping or image download is needed to browse or search this library.

The background gallery has a visible scrollbar and a large blue, centered loaded/total count. Its initial batch includes one extra row to make scrolling discoverable. Resizing recalculates columns and the batch size. Scrolling down appends the next batch; earlier images and the selected background stay in place. Filtering starts a fresh batch. Each image has a single light-gray caption strip with its title and style; the tooltip retains the full title when space is tight. Browsing and resizing do not run the embedding model.

## Static backgrounds and PowerPoint

The library, previews and exported presentations use static backgrounds. Animation controls, playback code, MP4 files and animated WebP previews have been removed from the app. Old saved animation flags are ignored while background selections are preserved. Cancelling the background picker preserves the current selection.

Users select one of 64 colours from an 8×8 swatch palette. Font, size, bold, colour and horizontal/vertical alignment belong to each tab. There is no shaded panel behind the lyrics. Each song's opening title slide includes copyright and CCLI text at 60% of the title size. The editor now fixes the camera guide at the lower right, 40% width and height with 2% margins and a white border. The view selector, resize handles and camera dragging are unavailable; opening a service resets its preview layouts to this default. Text uses the upper half. Long credits can reduce title and credit sizes together. The illustrated worship leader remains a preview-only camera placeholder and is never exported.

The lower-left preview toolbar controls horizontal flip and image opacity over black. Drag the slide background to pan, use the mouse wheel or −/＋ buttons to zoom from 100% to 400%, and click the percentage to reset framing. Pan is bounded so it cannot expose empty edges; zoom in before panning. These changes save separately for each tab and apply to all its slides, thumbnails and exported 720p backgrounds. Text stays editable and unaffected. A padded light-gray `[section]` label appears on the raw slide and in the PowerPoint at the lower right, within the fixed camera footprint. Thumbnail badges additionally show the slide index.

The Traditional/Simplified switch sits at the top right. Escape leaves an input without altering its text. With focus outside an input, Page Up/Down cycles saved services, Left/Right cycles tabs and Up/Down cycles slides. Tab starts at the song name and follows the editor fields; Shift+Tab reverses direction. Ctrl+Tab inserts indentation in the lyrics field.

PowerPoint embeds each selected image and retains editable native text and bundled font embedding. No video relationships or animation timelines are written. No video player, FFmpeg or WebView is required.

The camera-placement illustration is a separate bundled UI asset, `assets/ui/worship-leader-guitar.png`, edited with the built-in image-generation tool from the user's cartoon-stage reference. The leader plays an acoustic guitar and sings into a stand microphone on a bright white stage, wearing a visible gold cross necklace. There is no podium or wall cross. The prompt and provenance are saved alongside it. This preview artwork is embedded in the application, is not part of the 272-image background library, and never enters exported PowerPoint slides. The earlier warm-stage artwork is retained in the source folder but is no longer bundled.

Package/schema checks are automated. Native PowerPoint playback and Mac rendering still require review on those applications/devices; neither is installed on the Windows development host.

Windows and Apple Silicon use ONNX Runtime 1.31.0. Intel Mac uses the matching managed/native 1.23.2 pair, the last release verified here with an official Intel Mac binary ([upstream release](https://github.com/microsoft/onnxruntime/releases/tag/v1.23.2)). The publish step fails if the Mac native model library is absent. Native Mac inference still needs execution on a Mac.

## Local matching, only on request

Opening **Edit Background** matches the current song title and lyrics. A blank song does not trigger inference. Results are cached in memory for up to 128 distinct normalized SHA-256 keys. Section tags and presentation-only spacing are ignored; English word boundaries remain distinct. Reopening unchanged content, changing its font or switching Chinese scripts reuses the result without inference. Changed content receives a new key. Requests are serialized so simultaneous identical searches reuse the first completed match; cancelled/failed work is not cached. The first uncached match initializes the model on a worker thread. The dialog shows an extracting-themes-and-keywords overlay immediately. Model initialization uses an indeterminate bar; chunk encoding, theme ranking and keyword preparation report a percentage of completed work units, not an elapsed-time estimate. App startup, typing, switching songs and filtering metadata do not invoke it. Lyrics are never sent to an inference service or logged.

Typing custom Chinese or English terms and pressing **Search** or Enter embeds those terms and searches all 80 predefined themes. Custom text is preserved in the field and uses a separate cache key from song matching. Google remains a separate, explicitly clicked browser link.

The highest-ranked theme supplies its label plus three curated visual terms from `assets/media/search-terms.json`. The editable field shows Chinese only, using the app's selected Traditional or Simplified script. English equivalents are added only when constructing the Google link, from the current terms rather than a stale match. Unknown custom terms are kept as entered. This uses curated vocabulary, not keyword generation or translation by the embedding model.

The Google Images link opens the default browser only when clicked. It requires Christian/Bible context, the first subject group and background intent. Chinese/English equivalents within each group use `OR`; the remaining three visual details form a separate OR group. Generic light or mood terms cannot replace the subject. Commas, Chinese list separators, semicolons and newlines separate groups; slash or explicit OR separates alternatives. Both Chinese scripts are included, with no result-language restriction. Wide-aspect and 2MP filters are requested, but Google controls their behaviour and pixel count does not guarantee each dimension. The literal resolution is not a required search word. [Google Advanced Image Search](https://www.google.com/advanced_image_search?hl=en) documents OR, size and aspect-ratio controls. Lyrics are not sent to Google. Users import chosen images through file, URL, drop or paste; no image-search API is used.

The model is `sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2`, pinned to revision `e8f8c211226b894fcb81acc59f3b34ba3efd5f42`. The quantized ONNX weights occupy 118,453,870 bytes; the SentencePiece tokenizer is approximately 5 MB. `scripts/prepare-model.ps1` downloads and SHA-256-verifies build inputs. Installers bundle them for offline runtime use; weights are excluded from ordinary Git because they exceed GitHub's per-file limit.

Matching normalizes Traditional Chinese characters to Simplified Chinese using an OpenCC-derived character table, removes section tags and repeated lines, and encodes chunks of at most 126 content tokens. This is character normalization, not full phrase-aware translation. The XLM-R token IDs and pooled vectors are tested against Hugging Face tokenizer/ONNX fixtures. Mean-pooled, normalized embeddings have 384 dimensions.

The song title contributes 15% and lyrics 85% to the document representation. Theme ranking combines document/chunk similarity, explicit visual cues and a title cue bonus. Optional rare themes need an explicit cue. A diversity penalty selects four themes, displaying their available media styles. The 80 theme vectors are computed at build time; the model does not generate new pictures or claim to understand every song perfectly. Users choose the final background.

## Reproduction and provenance

All 240 themed images were made through the built-in OpenAI image-generation tool. Exact prompt sets are in `assets/media/generation-prompts.json`; per-image provenance and SHA-256 are in `assets/media/provenance/`. Original tool PNGs are preserved outside the repo; selected 720p JPEGs are in the workspace. No commercial worship publisher's artwork or lyrics are bundled.

Local development experiments prepared the prompts, normalized selected outputs, and rendered the procedural day/night artwork. These historical experiments are not distributed with this release. The checked-in catalog, assets and tests validate the application library; no generation scripts or animation assets are required to build the app.

Scene vectors, the normalization table and parity fixtures were prepared from the pinned model and are checked in. The 80 scenes came from research on a 1,318-song corpus, whose individual annotations are not all manually verified. The research corpus remains in the local development workspace and is not distributed with the application or this source release.
