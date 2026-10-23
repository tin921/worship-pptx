# Built-in background library

The desktop library contains 272 choices, all at 1280 × 720:

- 80 researched themes (60 core, 20 optional), each with one AI-generated photograph-style image, one painted illustration and one abstract image: 240 still images.
- 10 original procedural graphics, each in daylight and night lighting: 20 still images. Rain has stronger, larger visible drops and ripples.
- The original five-song pilot, two choices per song: 10 still images, resized to 720p for the application. Historical pilot files outside the app library are unchanged.
- Two additional style pilots: a soft watercolour prayer scene and a sculpted gold crown. The new style filters currently contain one sample each; see [style review and prompts](search-and-style-review.md).

`assets/media/catalog.json` is the app manifest. Files live under `themes/`, `generic/` and `legacy/`. Installed builds preserve these folders under `Media/`. No cloud storage, image-search API, website scraping or image download is needed to browse or search this library.

## Static backgrounds and PowerPoint

The library, previews and exported presentations use static backgrounds. Animation controls, playback code, MP4 files and animated WebP previews have been removed from the app. Old saved animation flags are ignored while background selections are preserved. Cancelling the background picker preserves the current selection.

Users select one of 64 colours from an 8×8 swatch palette. Font, size, bold, colour, horizontal/vertical alignment and preview composition belong to each tab. There is no shaded panel behind the text. Each song's opening title slide includes copyright and CCLI text at 60% of the title size; subsequent slides contain lyrics only. Six quick views replace the old full-slide/PiP toggle: four corners at 40% width/height with 2% margins, full slide and full camera. Drag the PiP or its white edges/square handles, or edit X/Y position and scale percentages. Camera and slide can swap foreground/background roles. Camera PiP at the top puts text in the bottom half, and camera PiP at the bottom puts text in the top half; a slide PiP or a full-screen view uses the full text region. Long credits can reduce title and credit sizes together. The illustrated worship leader wearing a cross necklace is a preview-only camera placeholder; it and the edit controls never enter PowerPoint. Export keeps the slide background at full size and native text in the derived region.

PowerPoint embeds each selected image and retains editable native text and bundled font embedding. No video relationships or animation timelines are written. No video player, FFmpeg or WebView is required.

The camera-placement illustration is a separate bundled UI asset, `assets/ui/worship-leader-guitar.png`, edited with the built-in image-generation tool from the user's cartoon-stage reference. The leader plays an acoustic guitar and sings into a stand microphone on a bright white stage, wearing a visible gold cross necklace. There is no podium or wall cross. The prompt and provenance are saved alongside it. This preview artwork is embedded in the application, is not part of the 272-image background library, and never enters exported PowerPoint slides. The earlier warm-stage artwork is retained in the source folder but is no longer bundled.

Package/schema checks are automated. Native PowerPoint playback and Mac rendering still require review on those applications/devices; neither is installed on the Windows development host.

Windows and Apple Silicon use ONNX Runtime 1.31.0. Intel Mac uses the matching managed/native 1.23.2 pair, the last release verified here with an official Intel Mac binary ([upstream release](https://github.com/microsoft/onnxruntime/releases/tag/v1.23.2)). The publish step fails if the Mac native model library is absent. Native Mac inference still needs execution on a Mac.

## Local matching, only on request

Opening **Edit Background** invokes `MediaSearch.MatchAsync` with the current song title and lyrics. A blank song does not trigger inference. The first match initializes the model on a worker thread; later matches reuse it. The dialog shows an extracting-themes-and-keywords overlay immediately. Model initialization uses an indeterminate bar; chunk encoding, theme ranking and keyword preparation report a percentage of completed work units, not an elapsed-time estimate. App startup, typing, switching songs and filtering metadata do not invoke it. Lyrics are never sent to an inference service or logged.

The highest-ranked theme supplies its label plus three curated visual terms from `assets/media/search-terms.json`. Each group pairs Chinese with English, using short curated English subjects for the 80 themes. Original lyrics determine which language appears first and which Chinese script to use; the app's selected script also applies to the displayed field. Section tags are ignored; title language is the fallback for empty lyrics. This is local script detection and curated vocabulary, not keyword generation or translation by the embedding model.

The Google Images link opens the default browser only when clicked. It requires Christian/Bible context, the first subject group and background intent. Chinese/English equivalents within each group use `OR`; the remaining three visual details form a separate OR group. Generic light or mood terms cannot replace the subject. Commas, Chinese list separators, semicolons and newlines separate groups; slash or explicit OR separates alternatives. Both Chinese scripts are included, with no result-language restriction. Wide-aspect and 2MP filters are requested, but Google controls their behaviour and pixel count does not guarantee each dimension. The literal resolution is not a required search word. [Google Advanced Image Search](https://www.google.com/advanced_image_search?hl=en) documents OR, size and aspect-ratio controls. Lyrics are not sent to Google. Users import chosen images through file, URL, drop or paste; no image-search API is used.

The model is `sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2`, pinned to revision `e8f8c211226b894fcb81acc59f3b34ba3efd5f42`. The quantized ONNX weights occupy 118,453,870 bytes; the SentencePiece tokenizer is approximately 5 MB. `scripts/prepare-model.ps1` downloads and SHA-256-verifies build inputs. Installers bundle them for offline runtime use; weights are excluded from ordinary Git because they exceed GitHub's per-file limit.

Matching normalizes Traditional Chinese characters to Simplified Chinese using an OpenCC-derived character table, removes section tags and repeated lines, and encodes chunks of at most 126 content tokens. This is character normalization, not full phrase-aware translation. The XLM-R token IDs and pooled vectors are tested against Hugging Face tokenizer/ONNX fixtures. Mean-pooled, normalized embeddings have 384 dimensions.

The song title contributes 15% and lyrics 85% to the document representation. Theme ranking combines document/chunk similarity, explicit visual cues and a title cue bonus. Optional rare themes need an explicit cue. A diversity penalty selects four themes, displaying their available media styles. The 80 theme vectors are computed at build time; the model does not generate new pictures or claim to understand every song perfectly. Users choose the final background.

## Reproduction and provenance

All 240 themed images were made through the built-in OpenAI image-generation tool. Exact prompt sets are in `assets/media/generation-prompts.json`; per-image provenance and SHA-256 are in `assets/media/provenance/`. Original tool PNGs are preserved outside the repo; selected 720p JPEGs are in the workspace. No commercial worship publisher's artwork or lyrics are bundled.

Local development experiments prepared the prompts, normalized selected outputs, and rendered the procedural day/night artwork. These historical experiments are not distributed with this release. The checked-in catalog, assets and tests validate the application library; no generation scripts or animation assets are required to build the app.

Scene vectors, the normalization table and parity fixtures were prepared from the pinned model and are checked in. The 80 scenes came from research on a 1,318-song corpus, whose individual annotations are not all manually verified. The research corpus remains in the local development workspace and is not distributed with the application or this source release.
