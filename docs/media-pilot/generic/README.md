# Generic mood backgrounds

Open `index.html` in Edge or Chrome to compare six original backgrounds, each with **daylight and night versions**. The gallery opens in daylight mode; use **日光 / 夜色** to switch. Each version has a PNG and a silent five-second MP4 loop. These are mood choices, independent of song titles, lyric keywords or the embedding taxonomy.

New daylight images and clips are **1280 × 720 (720p), 16:9**. Their soft, light palettes use dark `#22374b` sample lyrics. The original night assets retain their 1920 × 1080 resolution and white sample lyrics. Switching lighting updates the displayed image/video, sample text colour and download links together; it retains the selected mood, animation/pause setting, sample text and guide. Downloads contain backgrounds only, so choose the matching text colour in the slide editor.

| Background | Mood | Daylight palette | Relative motion |
| --- | --- | --- | --- |
| 輕雪 / Soft snow | Calm, fresh | Ice blue, snow white | 1 / 4 |
| 微雨 / Gentle rain | Calm, refreshing | Morning mist, mint | 2 / 4 |
| 暖光浮游 / Golden lights | Warm, joyful | Cream, honey gold | 2 / 4 |
| 自在飛翔 / Paper flight | Hopeful, joyful | Sky blue, warm gold | 3 / 4 |
| 花瓣微風 / Petal breeze | Joyful, fresh | Blush, lavender | 3 / 4 |
| 流光 / Living ribbons | Energized, joyful | Sea-glass green, soft gold | 4 / 4 |

The highest motion rating is still restrained. Snow and rain fall, small ripples expand, warm light particles rise, paper birds glide, petals drift, and layered ribbons flow. There are no cuts, flashes, lyrics or music. Most movement stays below the upper-half lyric area. The gallery provides mood filters, pause, still/animation mode, full screen, downloads, editable sample text and a half-slide guide. It uses still images by default when the device requests reduced motion and pauses videos outside the visible gallery area.

All artwork and animation are procedural Canvas graphics created for this project, extending the existing graphic-background renderer. No image search, stock media, external AI service or image-generation call was used. The editable source is `../../../experiments/media-pilot/generic-render.cjs`; descriptive design settings, seeds, moods and relative motion levels are in `catalog.json`. Assets can be regenerated offline using Node, `@napi-rs/canvas` and FFmpeg.

Regenerate the daylight set with `node experiments/media-pilot/generic-render.cjs --daylight`, then rebuild the page with `node experiments/media-pilot/generic-gallery.cjs` (from the repository root with the dependencies above available). Daylight files are `graphic-daylight.png` and `loop-daylight.mp4` in each scene folder. The original `graphic.png` and `loop.mp4` are preserved. Catalog schema 2 keeps the original file references and dimensions, adds `lighting_modes` with dimensions and lyric colours, and stores each scene's new files under `variants.daylight`.

Every MP4 contains 150 frames at 30 fps, H.264/yuv420p, with no audio track. The source frame at 5 seconds exactly matches the frame at zero; the duplicate endpoint is omitted. `render-validation.json` and `render-validation-daylight.json` record that equality. `validation.json` records independent decoding, file hashes, loop-seam measurements and sampled contrast for each mode's text colour. `gallery-validation.json` records browser playback and control checks for both modes.

Use the PNG as a slide background. Use the MP4 as a video object behind the lyrics, with automatic start and looping enabled. These clips have been checked in Edge on Windows; PowerPoint and macOS playback have not been tested. The app's video export has not been changed. Player behavior can affect the pause at a loop or slide change.

The assets remain local. No source push, release upload, application rebuild or app screenshot was performed for this addition.
