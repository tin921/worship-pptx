"""Post-hoc editorial pass; NEVER used to claim held-out evaluation accuracy.

Keep the frozen 31/60/80 comparison unchanged. Improve the deliverable's labels
using source-supplied subjects and explicit evidence, and disclose this change.
"""
from collections import Counter
from pathlib import Path
import json
import logging
import re

import jieba
import jieba.posseg
import numpy as np

from image_keywords import Encoder, unit, normalize, diverse_top, search_link
from worship_taxonomy import SCENES, SUBJECTS
from analyze_worship_corpus import (ROOT, PRIVATE, OUT, TRAD, STOP, TAG_SUBJECTS,
                                   cues, chunks, read_cache, save_cache, dump_csv)

SUBJECT_BY_ID = {s[0]:s for s in SUBJECTS}
UNINFORMATIVE_TAGS = set('心 眼睛 上帝 耶稣 英语 粤语 台语 国语 韩语 蒙语 译自外语 献诗 回应 破冰 宣告 见证 福音音乐剧 经文诗歌 天路共勉'.split())
GENERIC_SCENES = {'gentle_light','calm_lake','mountains','sunrise','sky','meadow','forest_path'}
EXTRA_WORDS = '恩典 慈爱 怜悯 救恩 救赎 盟约 盼望 信心 信实 应许 委身 舍己 奉献 感恩 敬拜 赞美 祷告 圣灵 圣洁 复兴 国度 公义 平安 安息 医治 使命 福音 服侍 光明 黑暗 生命 灵魂 荣耀 尊贵 能力 祝福 眼泪 哀伤 勇气 坚忍 赦罪 十架 麦子 活水 羊群 创造 罪恶 悔改 亲密 拥抱 合一 同在 真理 承诺 约誓 约定 奇迹 释放 犠牲 牺牲 宇宙 浩瀚 晨曦 爱情 婚姻 风暴 田地 旷野 欢喜 高兴 安宁 宁静 休憩 建立 拆下 充满 主脚前'.split()


def main():
    rows = json.loads((OUT/'songs.json').read_text(encoding='utf-8'))
    if rows and 'annotation_method' in rows[0]:
        # Re-run against the preserved frozen input, never recursively refine.
        rows = json.loads((PRIVATE/'frozen-annotations.json').read_text(encoding='utf-8'))
    else:
        (PRIVATE/'frozen-annotations.json').write_text(json.dumps(rows,ensure_ascii=False),encoding='utf-8')
    corpus = {r['id']:r for r in json.loads((PRIVATE/'corpus-private.json').read_text(encoding='utf-8'))}
    stats = json.loads((OUT/'analysis.json').read_text(encoding='utf-8'))
    jieba.setLogLevel(logging.ERROR)
    known = set(normalize(w) for row in SCENES+SUBJECTS for w in row[-1].split() if len(w)>=2)
    known.update(normalize(w) for w in EXTRA_WORDS)
    forbidden = (STOP - {'充满'}) | set('结尾 副歌 主歌 桥段 间奏 尾声 齐唱 领唱 重复 上如 之泉 展开 化为 献出 走到 就让 冉冉升起 将要 应得 用尽 不息 上到 来到 在于 成为 我心 主脚'.split())
    for w in known:
        jieba.add_word(w,10000,'n')
    encoder = Encoder(); read_cache(encoder)
    sv = encoder.encode([normalize(s[2]) for s in SCENES])
    tv = encoder.encode([normalize(s[2]) for s in SUBJECTS])
    modified = []
    for i,r in enumerate(rows):
        song = corpus[r['id']]
        lines = [l for l in song['lines'] if normalize(l) not in forbidden]
        text = normalize(song['title']+' '+ ' '.join(lines))
        title = normalize(song['title'])
        cv = encoder.encode(chunks(encoder,lines))
        doc = unit(.85*unit(cv.mean(axis=0))+.15*encoder.encode([title])[0])
        hits = [cues(text,s) for s in SCENES]
        semantic = .7*(sv@doc)+.3*(sv@cv.T).max(axis=1)
        title_hit = [any(normalize(c) in title for c in s[-1].split()) for s in SCENES]
        # An explicit rare motif can enter from the 20 extensions; unsupported
        # crowns, tombs, weddings, kneeling figures, etc. cannot crowd out it.
        supported = [j for j in range(80) if hits[j] or title_hit[j]]
        eligible = supported + [j for j,s in enumerate(SCENES) if s[0] in GENERIC_SCENES and j not in supported]
        score = semantic + np.array([.20*min(3,len(h)) + .35*title_hit[j] + (.12 if j in supported else 0) for j,h in enumerate(hits)])
        selected = diverse_top(score[eligible], sv[eligible],count=3,weight=.9)
        selected = [eligible[j] for j in selected]
        recommendations = [{'id':SCENES[j][0], 'label':SCENES[j][1], 'query':SCENES[j][3],
            'evidence':'歌名或歌詞含意象詞' if j in supported else '一般氣氛聯想',
            'matched_cues':[TRAD.convert(c) for c in hits[j]], 'google_images_url':search_link(SCENES[j][3])} for j in selected]

        # Prefer published song-subject tags, retaining their wording; do not
        # silently reinterpret a broad tag such as "life" as "renewal".
        source_subjects = []
        for tag in r['source_topics']:
            clean = re.sub(r'[A-Za-z].*','',normalize(tag)).strip()
            if clean and clean not in UNINFORMATIVE_TAGS and clean in TAG_SUBJECTS:
                source_subjects.append(TRAD.convert(clean))
        source_subjects = list(dict.fromkeys(source_subjects))
        thits = [cues(text,s) for s in SUBJECTS]
        for j,s in enumerate(SUBJECTS):
            for alias in {'joy':['欢喜','高兴'], 'peace':['安宁','宁静','休憩']}.get(s[0],[]):
                if alias in text and alias not in thits[j]:thits[j].append(alias)
        tscore = tv@doc + np.array([.15*min(3,len(h)) + .20*bool(cues(title,SUBJECTS[j])) for j,h in enumerate(thits)])
        supported_subjects = [j for j,h in enumerate(thits) if h]
        if source_subjects:
            summary_topics = source_subjects[:3]
            theme_ids = list(dict.fromkeys(TAG_SUBJECTS.get(normalize(t)) for t in summary_topics))
            theme_ids = [t for t in theme_ids if t]
            method = '來源主題標籤摘要；關鍵字與圖片為本機自動分析'
        else:
            candidates = supported_subjects or list(range(len(SUBJECTS)))
            order = sorted(candidates,key=lambda j:-tscore[j])
            summary_topics = [SUBJECTS[j][1] for j in order[:2]]
            theme_ids = [SUBJECTS[j][0] for j in order[:2]]
            method = '歌詞意象詞與本機語意模型推斷；待人工覆核'
        # Keywords must occur in input. Prefer recognizable concepts and nouns
        # over highly ranked but malformed fragments / generic verb sequences.
        observed = [w for w in sorted(known) if w in text and w not in forbidden]
        nouns = []
        for w,pos in jieba.posseg.cut(text):
            if pos in ('n','nz') and 2<=len(w)<=5 and re.fullmatch(r'[\u3400-\u9fff]+',w) and w not in forbidden and not w.startswith(('之','其','所')):
                nouns.append(w)
        terms = list(dict.fromkeys(observed+nouns))
        if not terms:
            terms = [title]
        kv = encoder.encode(terms)
        ks = kv@doc + np.array([(.14 if w in known else 0) + (.20 if w in title else 0) for w in terms])
        ki = diverse_top(ks,kv,count=4,weight=.9)
        keywords = list(dict.fromkeys(TRAD.convert(terms[j]) for j in ki))
        review = []
        if not source_subjects:
            review.append('內容主題沒有來源標籤佐證')
        if not supported:
            review.append('沒有明確畫面意象詞；背景為氣氛聯想')
        if r['chinese_character_count']<60:
            review.append('短歌詞')
        if len(keywords)<3:
            review.append('可辨識關鍵字較少')
        r.update({'baseline_theme_summary':r['theme_summary'], 'baseline_theme_ids':r['theme_ids'], 'baseline_keywords':r['keywords'],
            'baseline_query_60':r['query'], 'theme_summary':'；'.join(summary_topics), 'themes':summary_topics,
            'theme_ids':theme_ids,
            'annotation_method':method, 'keywords':keywords, 'recommended_scenes':recommendations,
            'recommended_visuals':[x['label'] for x in recommendations],
            'query':recommendations[0]['query'], 'google_images_url':recommendations[0]['google_images_url'],
            'review_reasons':review, 'review_status':'待人工覆核' if review else '自動初步標註'})
        modified.append(r)
        if (i+1)%300==0:print(f'Refined {i+1}/{len(rows)}',flush=True)
    save_cache(encoder)
    stats['editorial_pass'] = {'status':'post-hoc, not held-out validated',
        'source_tag_based_summaries':sum(r['annotation_method'].startswith('來源') for r in modified),
        'model_and_cue_based_summaries':sum(not r['annotation_method'].startswith('來源') for r in modified),
        'songs_with_no_explicit_scene':sum('沒有明確畫面意象詞；背景為氣氛聯想' in r['review_reasons'] for r in modified),
        'flagged_for_review':sum(bool(r['review_reasons']) for r in modified),
        'rule':'Prioritize published subjects; restrict specific scenes to explicit cues or title matches; permit generic nature/lighting fallback; suppress fragment keywords.',
        'warning':'31/60/80 metrics describe the frozen baseline, not these revised catalogue suggestions.'}
    (OUT/'analysis.json').write_text(json.dumps(stats,ensure_ascii=False,indent=2),encoding='utf-8')
    (OUT/'songs.json').write_text(json.dumps(modified,ensure_ascii=False,indent=2),encoding='utf-8')
    dump_csv('songs.csv',modified,['id','title','provider','artist','album','language','theme_summary','keywords','recommended_visuals','source_topics','annotation_method','visual_31','visual_60','visual_80','query','source','google_images_url','review_status','review_reasons','split','chinese_character_count'])
    print(json.dumps(stats['editorial_pass'],ensure_ascii=False,indent=2))


if __name__=='__main__':main()
