"""Reproducible local embedding screening of the deduplicated Chinese corpus.

All labels are automatic research annotations, not human-verified song reviews.
No lyrics are exported. Source subject tags are kept separate from predictions.
"""
from pathlib import Path
from collections import Counter
import csv
import hashlib
import json
import logging
import math
import re
import time

import jieba
import jieba.posseg
import numpy as np
from opencc import OpenCC

from image_keywords import Encoder, unit, normalize, diverse_top, search_link
from worship_taxonomy import SCENES, SUBJECTS, MODIFIERS

ROOT = Path(__file__).resolve().parents[1]
PRIVATE = ROOT / '.tools/worship-research'
OUT = ROOT / 'docs/research/chinese-worship-corpus'
TRAD = OpenCC('s2t')
STOP = set('我们 你们 他们 自己 什么 这里 那里 时候 今日 今天 现在 永远 永永远远 一生 一生一世 一切 所有 全部 一位 一个 每个 每天 每日 不停 不能 不再 只有 唯一 从此 因为 所以 已经 直到 何等 何时 何处 是否 为何 怎么 这样 那样 多少 无论 无法 一样 里面 面前 世上 世间 心里 心中 心间 我的 你的 他的 我要 向你 只要 知道 愿意 感到 让我 使我 为我 为你 为主 你是 我是 如此 更加 最美 充满 不住 万有 万国 全地 天地 有神 主神 我主 耶稣 上帝 基督 天父 耶和华 哈利路亚 阿们 祢的 祂的 求主 愿主 爱和 爱是 所作 所行 且要'.split())

# Used only for an external-reference comparison. These are incomplete human
# tags, not exhaustive ground truth, and do not enter the prediction scores.
TAG_SUBJECTS = {
    '敬拜':'praise', '赞美称谢':'praise', '赞美':'praise', '配得':'praise',
    '感恩':'thanks', '感谢':'thanks', '恩典':'grace', '怜悯':'grace',
    '爱':'love', '爱戴':'love', '爱慕':'love', '亲密':'love', '拥抱':'love',
    '十字架':'cross', '宝血':'cross', '受难':'cross', '救赎':'cross', '救恩':'cross',
    '复活':'resurrection', '复活节诗歌':'resurrection',
    '圣诞':'christmas', '圣诞节诗歌':'christmas', '圣灵':'spirit', '恩膏':'spirit',
    '同在':'presence', '渴慕':'presence', '会晤':'presence', '呼求':'prayer', '祷告':'prayer', '祷告呼求':'prayer',
    '信心':'faith', '信靠':'faith', '信实':'promise', '应许':'promise',
    '跟随':'guidance', '门徒':'guidance', '天路共勉':'guidance',
    '献上':'surrender', '奉献委身':'surrender', '回应':'surrender',
    '悔改':'repentance', '宽恕':'repentance', '转向神':'repentance', '圣哉':'holiness', '谦卑':'holiness',
    '更新':'renewal', '生命':'renewal', '转化':'revival', '盼望':'hope', '等候':'hope',
    '平安':'peace', '安息':'peace', '医治':'comfort', '痛苦':'lament', '受苦':'lament', '哀歌':'lament',
    '软弱':'strength', '能力':'strength', '坚持':'strength',
    '争战':'warfare', '属灵争战':'warfare', '释放':'warfare', '被掳':'warfare', '自由':'warfare', '得胜':'warfare',
    '君王':'sovereignty', '权柄':'sovereignty', '国度':'sovereignty', '创造':'creation',
    '话语':'word', '聆听':'word', '经文诗歌':'word', '供应':'provision', '保护':'provision', '牧人':'provision', '祝福':'provision',
    '尊贵身份':'identity', '拣选':'identity', '天父':'identity', '合一':'unity', '教会':'unity', '群体':'unity',
    '使命':'mission', '佈道':'mission', '布道':'mission', '呼召':'mission',
    '事奉':'service', '公义':'service', '复兴':'revival', '世代':'revival', '城市':'revival', '土地':'revival',
    '末后':'eternity', '荣耀盼望':'eternity', '安息礼拜诗歌':'eternity', '圣餐':'communion',
    '喜乐':'joy', '跳舞':'joy', '家庭':'family', '孩子':'family', '家庭祭坛':'family', '婚礼':'family', '婚礼诗歌':'family',
}


def configured_cues(entry):
    # Avoid some original one-character matches inside unrelated compounds.
    alternatives = {'手':'双手 伸手 牵手 扶持', '风':'风雨 风浪 暴风 狂风', '殿':'圣殿 殿中 圣所',
                    '花':'花朵 鲜花 野花 花开', '火':'火焰 火燃 圣火 烈火', '路':'道路 路途 路上 天路 人生路',
                    '牧':'牧人 牧者 牧场', '海':'海洋 大海 海浪 海边 海水 沧海 波涛',
                    '雨':'雨水 春雨 恩雨 雨露 下雨', '雪':'白雪 雪花 洁白如雪', '羊':'羊群 羔羊 小羊 羊'}
    words = []
    for c in entry[-1].split():
        words.extend(alternatives.get(c, c).split())
    return list(dict.fromkeys(normalize(c) for c in words))


def cues(text, entry):
    return [c for c in configured_cues(entry) if c in text]


def chunks(encoder, lines):
    # Hard split unusually long lines before assembling <=110-token chunks.
    pieces = []
    for line in lines:
        line = normalize(line)
        pieces.extend(line[i:i+65] for i in range(0, len(line), 65))
    return encoder.chunks(pieces)


def dump_csv(name, rows, columns):
    with (OUT/name).open('w', encoding='utf-8-sig', newline='') as f:
        w = csv.DictWriter(f, fieldnames=columns, extrasaction='ignore')
        w.writeheader()
        for row in rows:
            w.writerow({k: '；'.join(str(v) for v in row[k]) if isinstance(row.get(k), list) else row.get(k, '') for k in columns})


def read_cache(encoder):
    p = PRIVATE/'embedding-cache.npz'
    if p.exists():
        with np.load(p, allow_pickle=False) as f:
            encoder.cache = dict(zip(f['texts'].tolist(), f['vectors']))


def save_cache(encoder):
    np.savez_compressed(PRIVATE/'embedding-cache.npz', texts=np.array(list(encoder.cache)), vectors=np.stack(list(encoder.cache.values())))


def main():
    start = time.perf_counter()
    OUT.mkdir(parents=True, exist_ok=True)
    data = json.loads((PRIVATE/'corpus-private.json').read_text(encoding='utf-8'))
    assert len(data) >= 1000, f'Need at least 1,000 distinct Chinese songs; got {len(data)}'
    jieba.setLogLevel(logging.ERROR)
    for entry in SCENES + SUBJECTS:
        for term in entry[-1].split():
            if len(term) >= 2:
                jieba.add_word(normalize(term), freq=10000, tag='n')
    encoder = Encoder()
    read_cache(encoder)
    print(f'Loaded {len(data)} songs; {len(encoder.cache)} cached embeddings', flush=True)
    scene_vectors = encoder.encode([normalize(s[2]) for s in SCENES])
    subject_vectors = encoder.encode([normalize(s[2]) for s in SUBJECTS])
    counts = Counter()
    features = []
    for r in data:
        text = normalize(r['title'] + ' ' + r['lyrics'])
        words = Counter(w for w, pos in jieba.posseg.cut(text) if 2 <= len(w) <= 6
                        and re.fullmatch(r'[\u3400-\u9fff]+', w) and pos[0] in 'nva' and w not in STOP)
        counts.update(words.keys())
        features.append({'words':words, 'text':text, 'scene_cues':[cues(text, s) for s in SCENES]})
    discovery = [i for i, r in enumerate(data) if r['split'] == 'discovery']
    holdout = [i for i, r in enumerate(data) if r['split'] == 'holdout']
    support = np.array([sum(bool(features[i]['scene_cues'][j]) for i in discovery) for j in range(80)])
    extensions = sorted(range(31, 80), key=lambda j: (-support[j], SCENES[j][0]))
    sets = {'31':list(range(31)), '60':list(range(31)) + extensions[:29], '80':list(range(80))}
    result = []
    subject_predictions = []
    source_reference = []
    all_rankings = {k:[] for k in sets}
    for i, (r, f) in enumerate(zip(data, features)):
        chunk_texts = chunks(encoder, r['lines'])
        vectors = encoder.encode(chunk_texts)
        document = unit(.85 * unit(vectors.mean(axis=0)) + .15 * encoder.encode([normalize(r['title'])])[0])
        # Keywords come from observed text. Never invent a lyric phrase.
        word_list = list(f['words'])
        if not word_list:
            word_list = [normalize(r['title'])]
        word_vectors = encoder.encode(word_list)
        idf = np.array([math.log((len(data)+1)/(counts[w]+1)) for w in word_list])
        word_scores = word_vectors @ document + .035*idf + .025*np.log1p([f['words'].get(w,1) for w in word_list])
        wi = diverse_top(word_scores, word_vectors, count=4, weight=.85)
        keywords = [TRAD.convert(word_list[j]) for j in wi]
        semantic = .7*(scene_vectors @ document) + .3*(scene_vectors @ vectors.T).max(axis=1)
        # Fixed shared scoring for 31/60/80. A candidate's score is independent
        # of vocabulary size. Scores are not confidence probabilities.
        literal = np.array([.12*min(2,len(c)) for c in f['scene_cues']])
        title_bonus = np.array([.20 if cues(normalize(r['title']), s) else 0 for s in SCENES])
        hybrid = semantic + literal + title_bonus
        chosen = {}
        for size, indices in sets.items():
            selected = diverse_top(hybrid[indices], scene_vectors[indices], count=3, weight=.85)
            chosen[size] = [indices[j] for j in selected]
            all_rankings[size].append(chosen[size])
        ss = .8*(subject_vectors @ document) + .2*(subject_vectors @ vectors.T).max(axis=1)
        scues = [cues(f['text'],s) for s in SUBJECTS]
        sh = ss + np.array([.08*min(3,len(c)) for c in scues])
        # Explicit subject in the title is especially useful for short songs.
        sh += np.array([.10 if cues(normalize(r['title']), s) else 0 for s in SUBJECTS])
        subject_order = diverse_top(sh, subject_vectors, count=3, weight=.9)
        subjects = [SUBJECTS[j][1] for j in subject_order[:2]]
        subject_predictions.append([SUBJECTS[j][0] for j in subject_order])
        tags = list(r.get('source_tags', []))
        for alt in r.get('alternate_sources', []):
            tags.extend(alt.get('source_tags', []))
        tags = sorted(set(tags))
        reference = sorted(set(TAG_SUBJECTS.get(re.sub(r'[A-Za-z].*','',normalize(t)).strip()) for t in tags) - {None})
        source_reference.append(reference)
        top = chosen['80']
        top60 = chosen['60']
        mods = [label for label, _, terms in MODIFIERS if any(normalize(t) in f['text'] for t in terms.split())]
        review = []
        if not any(f['scene_cues'][j] for j in top):
            review.append('三個畫面均屬語意聯想，無明確意象詞')
        if float(semantic[top[0]]) < .30:
            review.append('最高畫面語意相似度低於0.30（研究門檻）')
        if r['chinese_character_count'] < 60:
            review.append('短歌詞')
        record = {k:r.get(k,'') for k in ('id','title','provider','source','source_kind','album','artist','language','retrieved','split','lyrics_sha256','chinese_character_count')}
        if r.get('catalogue_url'):
            record['album_reference_url'] = record['source']
            record['source'] = r['catalogue_url']
        record.update({
            'analysis_basis':'取得的中文歌詞全文；自動標註',
            'theme_summary':subjects[0] + '；' + subjects[1],
            'themes':subjects, 'theme_ids':[SUBJECTS[j][0] for j in subject_order[:2]],
            'keywords':keywords, 'source_topics':tags,
            'visual_31':[SCENES[j][1] for j in chosen['31']],
            'visual_60':[SCENES[j][1] for j in top60],
            'visual_80':[SCENES[j][1] for j in top],
            'visual_80_ids':[SCENES[j][0] for j in top],
            'literal_visual_cues':[TRAD.convert(c) for j in top for c in f['scene_cues'][j]][:6],
            'modifiers':mods[:3], 'query':SCENES[top60[0]][3], 'google_images_url':search_link(SCENES[top60[0]][3]),
            'top_scene_semantic_score':round(float(semantic[top60[0]]),4),
            'review_reasons':review, 'review_status':'待人工覆核' if review else '自動初步標註',
            'chunk_count':len(chunk_texts), 'alternate_sources':r.get('alternate_sources',[])})
        result.append(record)
        if (i+1)%100 == 0:
            print(f'Analyzed {i+1}/{len(data)} songs in {time.perf_counter()-start:.1f}s', flush=True)
            save_cache(encoder)
    save_cache(encoder)
    comparisons = []
    for split, selected in [('all',list(range(len(data)))), ('discovery',discovery), ('holdout',holdout)]:
        for size, indices in sets.items():
            ranks = [all_rankings[size][i] for i in selected]
            any_80 = [i for i in selected if any(features[i]['scene_cues'])]
            supported = [i for i in selected if any(features[i]['scene_cues'][j] for j in indices)]
            matched = [i for i in selected if any(features[i]['scene_cues'][j] for j in all_rankings[size][i])]
            cue_events = sum(bool(c) for i in selected for c in features[i]['scene_cues'])
            covered_events = sum(bool(features[i]['scene_cues'][j]) for i in selected for j in indices)
            first = Counter(SCENES[r[0]][0] for r in ranks)
            comparisons.append({'split':split,'scene_count':int(size),'songs':len(selected),
                'songs_with_any_80_lexicon_cue':len(any_80), 'songs_with_available_literal_scene':len(supported),
                'literal_catalogue_coverage_percent':round(100*len(supported)/len(selected),2),
                'scene_cue_events_total_80':cue_events,
                'scene_cue_events_represented':covered_events,
                'scene_cue_event_coverage_percent':round(100*covered_events/max(1,cue_events),2),
                'top3_contains_literal_cue_percent':round(100*len(matched)/len(selected),2),
                'top3_no_literal_cue_count':len(selected)-len(matched),
                'top3_distinct_scenes_used':len({j for row in ranks for j in row}),
                'largest_top1_scene':first.most_common(1)[0][0],
                'largest_top1_scene_percent':round(100*first.most_common(1)[0][1]/len(selected),2),
                'mean_best_semantic_score':None})
    scenes = []
    for j,s in enumerate(SCENES):
        first = sum(row[0] == j for row in all_rankings['80'])
        top3 = sum(j in row for row in all_rankings['80'])
        d = int(support[j]); h = sum(bool(features[i]['scene_cues'][j]) for i in holdout)
        scenes.append({'id':s[0], 'label':s[1], 'description':s[2], 'query':s[3], 'cues':configured_cues(s),
            'in_original_31':j<31,'in_selected_60':j in sets['60'],
            'literal_discovery_songs':d,'literal_holdout_songs':h,'literal_all_songs':d+h,
            'top1_80_songs':first,'top3_80_songs':top3,
            'example_titles':[data[i]['title'] for i in discovery if features[i]['scene_cues'][j]][:4]})
    tag_counts = Counter(t for r in result for t in r['source_topics'])
    subject_counts = Counter(t for r in result for t in r['themes'])
    ref_cases = [i for i in holdout if source_reference[i]]
    ref_overlap = [i for i in ref_cases if set(source_reference[i]) & set(subject_predictions[i])]
    stats = {'date':'2026-10-21','songs':len(data),'discovery_songs':len(discovery),'holdout_songs':len(holdout),
        'source_counts':dict(Counter(r['provider'] for r in result)), 'language_counts':dict(Counter(r['language'] for r in result)),
        'artist_counts':dict(Counter(r.get('artist') for r in result if r.get('artist'))),
        'songs_with_source_tags':sum(bool(r['source_topics']) for r in result),
        'source_topic_counts':dict(tag_counts.most_common()),'automatic_subject_counts':dict(subject_counts.most_common()),
        'flagged_for_review':sum(bool(r['review_reasons']) for r in result),
        'source_tag_reference_check':{'holdout_tagged_songs':len(ref_cases),'top3_any_subject_overlap':len(ref_overlap),
            'percent':round(100*len(ref_overlap)/max(1,len(ref_cases)),2),
            'warning':'Broad overlap with incomplete publisher/library tags; not classification accuracy.'},
        'comparisons':comparisons,'runtime_seconds':round(time.perf_counter()-start,2),
        'model':'sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2',
        'model_revision':'e8f8c211226b894fcb81acc59f3b34ba3efd5f42',
        'model_weight_bytes':118453870, 'embedding_dimension':384,
        'method':'85% mean lyric chunks + 15% title; cosine similarity plus explicit cue bonuses; top3 diversity selection',
        'limitations':['Convenience sample from three catalogues, not a popularity-ranked or random sample.',
                       'Mandarin/Cantonese and some Hokkien; Chinese text is normalized but dialect understanding is not validated.',
                       'Automated annotations; no claim of 1000 human-reviewed summaries.',
                       'Fixed seed split; 60-scene selection uses discovery literal support only.',
                       'Literal-cue coverage and higher similarity do not establish image suitability or thematic accuracy.',
                       'Source tags are incomplete subject labels, not image relevance ground truth.',
                       'Same-title songs are conservatively merged, potentially excluding different compositions.',
                       'No actual image search results or licensing were evaluated.']}
    (OUT/'songs.json').write_text(json.dumps(result, ensure_ascii=False, indent=2),encoding='utf-8')
    (OUT/'master-scenes.json').write_text(json.dumps(scenes,ensure_ascii=False,indent=2),encoding='utf-8')
    (OUT/'subject-themes.json').write_text(json.dumps([{'id':s[0],'label':s[1],'description':s[2],'cues':s[3].split()} for s in SUBJECTS],ensure_ascii=False,indent=2),encoding='utf-8')
    (OUT/'analysis.json').write_text(json.dumps(stats,ensure_ascii=False,indent=2),encoding='utf-8')
    vocabulary = [{'keyword':TRAD.convert(w),'songs':n} for w,n in counts.most_common() if n >= 3]
    (OUT/'observed-keywords.json').write_text(json.dumps(vocabulary,ensure_ascii=False,indent=2),encoding='utf-8')
    dump_csv('observed-keywords.csv',vocabulary,['keyword','songs'])
    dump_csv('songs.csv',result,['id','title','provider','artist','album','language','theme_summary','keywords','source_topics','visual_31','visual_60','visual_80','literal_visual_cues','modifiers','query','source','google_images_url','analysis_basis','review_status','review_reasons','split','chinese_character_count'])
    dump_csv('master-scenes.csv',scenes,['id','label','query','cues','in_original_31','in_selected_60','literal_discovery_songs','literal_holdout_songs','literal_all_songs','top1_80_songs','top3_80_songs','example_titles'])
    dump_csv('comparison.csv',comparisons,['split','scene_count','songs','literal_catalogue_coverage_percent','scene_cue_event_coverage_percent','top3_contains_literal_cue_percent','top3_no_literal_cue_count','top3_distinct_scenes_used','largest_top1_scene','largest_top1_scene_percent'])
    print(json.dumps({k:stats[k] for k in ['songs','source_counts','language_counts','runtime_seconds','source_tag_reference_check','comparisons']},ensure_ascii=False,indent=2),flush=True)


if __name__ == '__main__':
    main()
