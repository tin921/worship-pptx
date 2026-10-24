# Chinese worship song theme study

**Research date: 21 October 2026. 1,318 deduplicated Chinese songs.**

**Recommendation: use about 60 core visual themes, keep 20 additional motifs available when explicitly supported, and keep song subjects separate from image scenes.** The study supports expanding beyond the original 31. It does not establish that exactly 60 or 80 is optimal, or that a larger catalogue alone makes the current embedding ranker reliable.

[Open the searchable catalogue](index.html) · [Download all song annotations](songs.csv) · [Download the 80 scene candidates](master-scenes.csv) · [Raw comparison measurements](analysis.json)

## What was collected

| Retained source | Songs | Source role |
| --- | ---: | --- |
| [讚美之泉](https://sop.org/songs/) | 534 | Official publisher's Chinese lyric catalogue |
| [小羊詩歌](https://w247.net/catalog/) | 231 | Official publisher's song pages |
| [Simply Worship](https://simplyworship.hk/lyrics/) | 553 | Hong Kong worship reference library, spanning multiple artists |
| **Total** | **1,318** | |

The collection started with 1,683 entries. It excluded 306 same-title/identical-lyric entries, five further near-identical lyric versions, two medleys, and 52 entries with fewer than 30 Chinese lyric characters, including English-only songs. Same-title merging is deliberately conservative: different compositions with the same name may have been omitted. Different Chinese adaptations can remain when both title and lyrics differ; these are song texts, not a verified census of unique underlying compositions.

352 retained songs are explicitly labelled Cantonese, 213 Mandarin, and 11 Taiwanese Hokkien. Another 534 are in the publisher's Chinese catalogue and 208 have Chinese text without an explicit dialect label. The reference-library subset includes works attributed to 約書亞樂團, HKACM, 團契遊樂園, 同心圓, 朱浩權, 鹹蛋音樂事工, 玻璃海樂團 and others. This is a convenience sample of accessible catalogues, **not a popularity ranking or representative survey of all Chinese churches**. Older hymnals, 迦南詩選 and some other ministries are underrepresented.

Each retained song has its name, source link, content-theme summary, two to four keywords, three background suggestions, original source tags where available, and review flags. All were analysed from retrieved Chinese lyric text; there are no title-only entries. Full lyrics remain in the ignored local research cache and are not reproduced in the deliverables.

## What 31, 60 and 80 actually changed

The 80-scene candidate vocabulary was written before evaluating the reserved subset. A stable title hash assigned 1,039 songs to discovery and 279 to holdout. The 60-scene set retains the original 31 and adds the 29 new candidates with the most explicit cue support in the discovery songs. It is therefore a practical expansion of the existing design, **not an optimized best-60 subset**.

The same model, scoring rules and diversity selection were used for all three vocabulary sizes. Results below are from the 279 reserved songs.

| Vocabulary | Songs containing a supported scene cue | Share of the 80-candidate cue occurrences represented | Songs with a cue-supported scene among top 3 suggestions |
| --- | ---: | ---: | ---: |
| 31 scenes | 178 / 279 — **63.80%** | **55.75%** | 155 / 279 — **55.56%** |
| 60 scenes | 229 / 279 — **82.08%** | **96.34%** | 186 / 279 — **66.67%** |
| 80 scenes | 229 / 279 — **82.08%** | **100.00%** | 187 / 279 — **67.03%** |

**These are lexical-support diagnostics, not relevance accuracy.** A cue occurrence means that one song matches at least one configured cue for a scene; one song can contribute to several scenes. The 80-candidate set has 100% of its own cue occurrences by definition. The denominator is an authored vocabulary, not an exhaustive inventory of everything a lyric could evoke. Matches include symbols such as a book for God's word, and can still be contextually inappropriate.

The practical result is that moving from 31 to 60 represented many missing concepts. Moving from 60 to 80 added no additional cue-covered songs in this holdout, and changed the top-three support statistic by only one song. Across the complete corpus, the extra 20 provided at least one new match for six songs. These measurements do not demonstrate a meaningful image-quality advantage for always ranking all 80 equally.

## Important omissions from the first 31

These are corpus-wide counts of songs matching the configured cue words, **not counts of songs primarily about that subject**.

| Added scene / image concept | Matching songs |
| --- | ---: |
| 淚滴與雨窗 | 117 |
| 聖經與書卷 | 106 |
| 寶座與榮光 | 90 |
| 城市與家園 | 90 |
| 屈膝禱告 | 87 |
| 擁抱與懷抱 | 78 |
| 萬國與世界 | 54 |
| 家庭與孩子 | 53 |
| 天使與天軍 | 45 |
| 祭壇與獻祭 | 44 |
| 空墓與復活晨光 | 35 |
| 避難所與城堡 | 33 |
| 冠冕 | 30 |

Frequency alone is insufficient. 麥粒 appeared in only four songs but is central to 《一粒麥子》. 雲柱火柱, 百合花, 紅海開路, 橄欖枝 and other less common motifs should remain available when the text calls for them. Christmas, Easter and Communion imagery also merits explicit support even if a particular sample makes it look uncommon.

## The larger issue: ranking and context

The original 31-scene ranker put warm light first for 36.20% of the holdout. With 60 scenes, kneeling prayer became the first choice for 24.73%. Simply adding themes shifted the generic favourite; it did not solve semantic interpretation.

The 24 deliberately selected diagnostic checks exposed several useful cases:

| Song | What the check showed |
| --- | --- |
| 恩典之路 | A mountain path fits better than an unsupported crown. Explicit imagery must outweigh generic religious similarity. |
| 一粒麥子 | A rare wheat motif should activate from the title and text, even if it falls outside the frequency-selected core. |
| 讓愛走動 | Tears occur in the words, but love and hope are the direction of the song. A sorrowful image can be a poor first choice despite a literal match. |
| 祢名 | The performance instruction 橋段 initially looked like a bridge cue. Section headings and arrangement instructions must be removed. |
| 為我造清潔的心 | A phrase comparing purity to snow can be missed by an incomplete snow alias list. More synonyms can matter more than more themes. |
| 永不止息 | 高峰、山嶺、深洋 need to map to existing mountain/ocean scenes rather than becoming new themes. |
| 片刻的安寧 | The automatic subject labels overemphasized guidance and renewal. The reviewed content summary was corrected to 平安安息；生命反思. |

These checks are diagnostic, not a random sample or an accuracy estimate. [All 24 review notes](spot-checks.json) are included.

## How to use the catalogue

The deliverable distinguishes **the frozen baseline experiment** from **a later refinement of the annotations**:

- `visual_31`, `visual_60`, `visual_80`, `baseline_theme_summary` and the comparison files preserve the original measured ranking.
- `theme_summary` prefers meaningful source-supplied subject tags where available. 929 summaries use those tags; 389 initially relied on model/cue inference, with one of those subsequently corrected during the diagnostic review. Source tags are useful editorial evidence, not exhaustive ground truth.
- `keywords` are observed, recognizable terms ranked using the local model, with title preference and fragment filtering. Most songs have four; a few short texts have fewer.
- `recommended_visuals` and `recommended_scenes` use the **post-hoc** rule: specific objects need explicit lyric/title support; general nature/light backgrounds can be labelled as mood associations. This allows rare motifs without letting unsupported crowns, tombs or wedding scenes dominate.
- `annotation_method`, `review_reasons` and `diagnostic_review_note` expose the evidence and remaining uncertainty. The post-hoc pass flags 582 entries for review; a flag can simply mean there are no existing source subject tags. It is not a count of errors.

The revised suggestions have **not** been evaluated on another independent corpus. Do not apply the frozen holdout numbers to them. No actual Google image results, composition suitability, or licences were evaluated.

## Proposed application structure

Use three separate layers:

1. **Content subjects:** a draft of 36 broad groups, such as grace, praise, trust, redemption, mission and rest. Multiple subjects may apply to one song. This is a design proposal, not a discovered optimal number of clusters.
2. **Image scenes:** 60 core candidates plus the 20 rarer candidates, promoted when supported by the text. Each has a Chinese description, aliases and a short English search phrase.
3. **Mood/composition:** calm, sunrise, night, warm light and similar modifiers. Add slide requirements independently, such as a wide image and space for text in the upper half.

Rank the complete lyric/title meaning against candidate descriptions with embeddings, but also preserve concrete words, their context, and title emphasis. Return three editable alternatives. When the song has no concrete scenery, say that the suggestion is an illustrative mood choice. Avoid treating similarity scores as confidence percentages.

For the next implementation, prioritize Chinese/Cantonese aliases and contextual ranking over adding dozens of near-duplicate scenes. A person should still choose the actual image. Expanding the text vocabulary does not require retraining or enlarging the 118 MB model.

## Model and reproducibility

The run used the existing local ONNX CPU copy of [multilingual MiniLM-L12-v2](https://huggingface.co/sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2), with 118,453,870-byte quantized weights, 384-dimensional embeddings and a 128-token input limit. Model revision: `e8f8c211226b894fcb81acc59f3b34ba3efd5f42`.

Chinese text is normalized to Simplified for matching; displayed annotations use Traditional Chinese. Lyrics are divided into short chunks with repeated lines removed. The document vector combines 85% mean lyric-vector weight with 15% title-vector weight. The frozen scene score is 70% document similarity plus 30% best-chunk similarity, 0.12 per distinct cue up to two, and 0.20 for a title cue, followed by diversity selection. The post-hoc pass has separate, documented rules.

The final baseline pass took about 71 seconds on this PC with an existing embedding cache. This is **not a cold-start performance benchmark** and excludes collection, initial uncached inference, annotation refinement and report production. All inference was local; no paid API was used.

From the repository root:

```powershell
.tools\embedding-env\Scripts\python.exe -X utf8 experiments\run_worship_research.py
```

Packages are pinned in `experiments/requirements-keywords.txt`; the model downloader and manifest record the exact model files and hash. Cached input HTML and full lyrics live under `.tools/worship-research/`, which is ignored by Git. The collection manifest records exclusions, source URLs and HTML hashes. A fresh collection on another date can differ from this snapshot.

Files:

- `index.html`: offline searchable catalogue and comparison view.
- `songs.csv` / `songs.json`: all 1,318 song annotations and provenance.
- `master-scenes.csv` / `master-scenes.json`: 80 visual candidates, core membership, cues and counts.
- `subject-themes.json`: 36 proposed content groups.
- `observed-keywords.csv`: recurring segmented terms before editorial filtering; exploratory and potentially noisy.
- `analysis.json` / `comparison.csv`: frozen measurements and refinement metadata.
- `spot-checks.json`: 24 diagnostic reviews.
- `collection-manifest.json`: retrieval and deduplication provenance.

This work is local research. The application, installer and releases were not updated; no code was pushed and no screenshots were captured.
