"""Record the agent's 24 diagnostic lyric/annotation reviews, not an accuracy test.

Only short analytical notes are exported. These cases were deliberately chosen,
not sampled to estimate population accuracy. One content summary is corrected.
"""
import json
from analyze_worship_corpus import OUT, dump_csv

NOTES = {
    ('讚美之泉','讚美之泉'): '清泉適合；來源的活水標籤屬意象，不能取代敬拜與感恩的內容主題。',
    ('讚美之泉','有一位神'): '創造、救贖與掌權均有內容依據；寶座和十架有明確文字支持。',
    ('讚美之泉','讓愛走動'): '淚滴雖有字面依據，歌曲重點卻是愛帶來希望；背景應優先考慮擁抱、暖光或種子。',
    ('讚美之泉','恩典之路'): '恩典、保護與引導相符；山路比無字面依據的冠冕更貼切。',
    ('讚美之泉','全然向祢'): '讚美、聖靈、委身相符；音樂、清泉、春雨等均有文字支持。',
    ('讚美之泉','展開清晨的翅膀'): '晨光與翅膀有支持；飛鷹是視覺比喻，並非歌詞明言的鳥種。',
    ('讚美之泉','我要全心讚美'): '喜樂讚美及聖殿意象有支持；沒有理由自動加入婚宴。',
    ('讚美之泉','能不能'): '同在、愛與敬拜是合適主題；聖殿背景有明確支持。',
    ('小羊詩歌','盟約'): '婚約、神愛與救恩互相交織；可以多標籤，單一類別會失真。',
    ('小羊詩歌','一粒麥子'): '舍己委身是核心；麥粒雖低頻，仍應在歌名匹配時啟用。',
    ('小羊詩歌','願祢的國降臨'): '神國、禱告和順服明確；沒有具體景物，使用者應能選擇象徵背景。',
    ('小羊詩歌','祢名'): '原頁演唱次序中的橋段不可當作橋樑；已清除段落標頭及演唱指示。',
    ('小羊詩歌','為我造清潔的心'): '悔改潔淨、聖靈與救恩相符；比雪更白應加入雪景的同義詞，現有詞表不完整。',
    ('小羊詩歌','祢歡喜的祭(台)'): '感恩、禱告、舉手敬拜相符；台語用字的召回率未經系統驗證。',
    ('小羊詩歌','感恩有祢'): '感恩與同行相符；一般暖光屬氣氛聯想，非明確景物。',
    ('小羊詩歌','片刻的安寧'): '自動主題過度偏向引導和更新；依全文修正為平安安息與生命反思。',
    ('Simply Worship','不破的約誓'): '十架、犧牲、救贖是重點；寶座雖有文字支持，不一定應排第一。',
    ('Simply Worship','我算甚麼'): '來源的創造、恩典、生命比基準模型的永恆再來更貼近內容。',
    ('Simply Worship','當你走到無力'): '疲乏中得到扶助與信靠；雙手和展翅均有文字依據。',
    ('Simply Worship','永不止息'): '神愛與同在相符；山嶺、高峰、深洋等同義詞仍需擴充，無須新增同義主題。',
    ('Simply Worship','我只想去讚美你'): '敬拜、愛慕和舉手意象直接，屬較容易處理的例子。',
    ('Simply Worship','無價至寶'): '恩典、愛與十架相符；不可只因歌名含至寶便強推珠寶圖片。',
    ('Simply Worship','我在這'): '感恩恩典相符；沒有明確景物，背景應標示為一般氣氛聯想。',
    ('Simply Worship','我全心相信'): '信靠、呼求及恢復生命相符；伸手的意象有文字依據。',
}


def main():
    rows = json.loads((OUT/'songs.json').read_text(encoding='utf-8'))
    checks = []
    for r in rows:
        key = r['provider'],r['title']
        if key not in NOTES:
            continue
        r['diagnostic_review_note'] = NOTES[key]
        if r['title']=='片刻的安寧' and r['provider']=='小羊詩歌':
            r['pre_review_theme_summary']=r['theme_summary']
            r['theme_summary']='平安安息；生命反思'
            r['themes']=['平安安息','生命反思']
            r['theme_ids']=['peace']
            r['annotation_method']='診斷覆核時依歌詞全文人工修正內容主題；圖片仍為自動建議'
        checks.append({'id':r['id'],'title':r['title'],'provider':r['provider'],'note':NOTES[key],
                       'source':r['source'],'selection':'deliberate diagnostic example; not random'})
    assert len(checks)==24,len(checks)
    (OUT/'spot-checks.json').write_text(json.dumps(checks,ensure_ascii=False,indent=2),encoding='utf-8')
    (OUT/'songs.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
    dump_csv('songs.csv',rows,['id','title','provider','artist','album','language','theme_summary','keywords','recommended_visuals','source_topics','annotation_method','diagnostic_review_note','visual_31','visual_60','visual_80','query','source','google_images_url','review_status','review_reasons','split','chinese_character_count'])
    print('Recorded 24 diagnostic spot checks, including one manual subject correction.')


if __name__=='__main__':main()
