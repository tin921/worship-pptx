# Background search and style review

22 October 2026. Local review build based on v0.92; not a published release.

## Search link

The query now has three requirements: Christian/Bible context, the main subject, and background intent. Chinese and English equivalents use OR within a concept. The remaining three visual details form a separate OR group; lighting or atmosphere cannot substitute for the main subject.

The editable field contains four groups, such as `屈膝禱告 / kneeling prayer、屈膝剪影 / kneeling silhouette、雙手合十 / praying hands、柔和晨光 / soft dawn`. The first group is the subject. A slash or OR separates alternatives, and a comma or Chinese enumeration comma separates groups. Traditional and Simplified variants are included in the link. No result-language restriction is added. Arbitrary typed words are not automatically translated: curated bilingual terms cover the 80 built-in themes, and users can edit both halves.

Google remains responsible for result relevance and language ranking. Its requested large/wide image filters do not guarantee a specific width and height; check downloaded files for at least 1280 × 720. The link sends the editable search terms, not the original lyrics. It only opens Google when clicked.

Reference: [Google Advanced Image Search](https://www.google.com/advanced_image_search?hl=en) documents OR, exact phrases, size and aspect-ratio controls.

## Matching assessment

Current matching uses the local multilingual MiniLM encoder, combining lyric chunks and the title, then applying cue boosts and diversity selection. It returns four themes. The displayed keywords come from curated terms attached to the highest-ranked theme; they are not free-form keyword extraction from the lyric. Adding bilingual terms does not require a larger embedding model.

This is a useful suggestion system, not a validated automatic choice. The earlier [1,318-song study](research/chinese-worship-corpus/report.md) found that 60 themes covered substantially more configured cues than 31, but 80 added little additional cue coverage. The frozen ranker also overselected kneeling prayer. Those measurements were lexical diagnostics, not human relevance accuracy, and do not constitute an accuracy score for today's application.

The current app still has no calibrated confidence threshold, always returns four candidates, and uses core/optional membership rather than requiring explicit evidence for every specific symbol. A crown, wedding or tomb can be misleading when only broad religious meaning matches. Priority improvements are contextual evidence for concrete motifs, distinguishing mood from lyric subject, an explicit uncertain/mood fallback, and a human-reviewed set of real song-to-background judgements. The ranking formula is unchanged in this update.

## Inventory and new styles

Before this update: 270 images — 240 themed images (80 themes × photography, illustration and abstract), 20 generic day/night stills, and 10 earlier samples. This is sufficient breadth for a first library. More layout, brightness and mood choices for frequent themes are more useful next than many near-duplicate theme names. Image composition is not currently part of the embedding ranking.

Two original 1280 × 720 samples bring the local app inventory to 272:

- **柔光水彩 / Soft watercolour:** prayer figure in the lower left, pale open sky for dark lyrics; `kneeling_prayer-watercolour`.
- **立體象徵 / Sculpted symbolism:** a gold crown on stone against a calm dark backdrop for white lyrics; `crown-sculpted`.

These are two pilot assets, not two full 80-theme sets. Both appear in the app's style filter and in results for their respective theme. A camera inset in the lower-right corner leaves the main subjects visible.

Generated with the built-in Image Generation tool, using the user-provided prayer/crown screenshot as a subject and composition reference. No screenshot lettering or interface was carried into the assets. Original generated files and 720p samples are in `docs/style-proposals/`; runtime copies are under `assets/media/themes/`. FFmpeg was used only to resize the generated outputs to 1280 × 720; no video or animation was added. The exact prompts are saved alongside the samples.
