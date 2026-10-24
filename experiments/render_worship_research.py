"""Build a self-contained, offline, searchable research catalogue (no lyrics)."""
from pathlib import Path
from collections import Counter
import hashlib
import json

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT/'docs/research/chinese-worship-corpus'

TEMPLATE = r'''<!doctype html>
<html lang="zh-HK"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>中文敬拜詩歌 · 主題研究</title>
<style>
:root{font-family:"Microsoft JhengHei","PingFang HK",system-ui,sans-serif;color:#203c35;background:#f5f7f4;font-size:16px}*{box-sizing:border-box}body{margin:0}header{background:#e5eee6;padding:36px 5vw 28px;border-bottom:1px solid #c9d9cb}h1{font-size:32px;margin:8px 0 14px;letter-spacing:.03em}h2{font-size:22px;margin:28px 0 14px}p{line-height:1.7;max-width:1050px}a{color:#216749}main{padding:20px 5vw 60px}.eyebrow{font-size:13px;letter-spacing:.12em;color:#52715e}.stats{display:flex;gap:16px;flex-wrap:wrap;margin:22px 0}.stat{background:white;border:1px solid #d4e0d5;border-radius:12px;padding:16px 24px;min-width:170px}.stat b{display:block;font-size:30px;font-weight:600}.stat span,small{font-size:13px;color:#64796b}.tabs{display:flex;gap:8px;flex-wrap:wrap;margin-bottom:22px}button,.download{border:1px solid #bdcfc1;background:white;border-radius:8px;padding:10px 17px;font:inherit;color:#284a3b;cursor:pointer;text-decoration:none;display:inline-block}button.active{background:#345d47;color:white;border-color:#345d47}button:disabled{opacity:.5;cursor:default}.filters{display:flex;gap:12px;flex-wrap:wrap;align-items:center;margin:12px 0 18px}input,select{background:white;border:1px solid #bccdc0;border-radius:8px;padding:11px;font:inherit}input{flex:1;min-width:240px}label{font-size:14px}.tablewrap{overflow:auto;background:white;border:1px solid #d3dfd4;border-radius:10px}table{border-collapse:collapse;width:100%;font-size:14px;min-width:920px}th{text-align:left;background:#edf3ed;padding:13px;white-space:nowrap;position:sticky;top:0}td{border-top:1px solid #e5ebe4;padding:13px;vertical-align:top;line-height:1.65}td:first-child{min-width:150px}td:nth-child(3){min-width:150px}.tags{display:flex;gap:5px;flex-wrap:wrap}.tag{background:#eef3ec;border-radius:5px;padding:2px 6px;white-space:nowrap;font-size:12px}.source{font-size:12px;color:#607568}.pager{display:flex;align-items:center;gap:15px;justify-content:flex-end;margin:16px 0}.note{background:#fff8e8;border-left:4px solid #c4a559;padding:12px 18px;font-size:14px;line-height:1.7}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:12px}.card{background:white;border:1px solid #d5e0d5;border-radius:10px;padding:18px}.card h3{margin:0 0 8px;font-size:17px}.bar{height:7px;background:#e8eee6;border-radius:8px;margin:12px 0}.bar span{height:100%;display:block;background:#739980;border-radius:8px}.muted{color:#6a7c6d}.hide{display:none}.flag{color:#94601d;font-size:12px}code{font-size:13px;background:#eaf0e8;padding:2px 5px}.links{display:flex;gap:9px;flex-wrap:wrap}@media(max-width:700px){header,main{padding-left:18px;padding-right:18px}h1{font-size:26px}.stat{min-width:130px;padding:12px}.stat b{font-size:25px}}
</style>
<header><div class="eyebrow">WORSHIP PPTX · RESEARCH · 2026-10-21</div><h1>中文敬拜詩歌 · 主題與背景意象</h1><p id="intro"></p><div class="stats" id="stats"></div><div class="links"><a class="download" href="songs.csv" download>下載所有歌曲 CSV</a><a class="download" href="master-scenes.csv" download>下載 80 個畫面主題</a><a class="download" href="report.md">研究報告</a></div></header>
<main><div class="note">這是以歌詞全文及來源標籤作分析的<strong>自動初步標註</strong>，並非逐首由人手核實。歌曲內容主題與圖片意象分開記錄；來源原有標籤亦保留供比較。此報告不包含歌詞全文。背景搜尋詞可修改，搜尋結果尚未經檢視。</div>
<nav class="tabs" style="margin-top:24px"><button data-tab="catalog" class="active">歌曲目錄</button><button data-tab="themes">80 個圖片意象</button><button data-tab="comparison">31 / 60 / 80 比較</button><button data-tab="method">資料與方法</button></nav>
<section id="catalog"><div class="filters"><input id="search" aria-label="搜尋歌名、主題、關鍵字" placeholder="搜尋歌名、主題、關鍵字…"><select id="provider" aria-label="資料來源"><option value="">所有來源</option></select><select id="language" aria-label="語言"><option value="">所有語言</option></select><label><input id="flagged" type="checkbox" style="min-width:auto"> 只看需覆核</label></div><p class="muted" id="count"></p><div class="tablewrap"><table><thead><tr><th>歌名 / 來源</th><th>內容主題</th><th>歌詞關鍵字</th><th>圖片意象 · 修訂規則</th><th>來源原有標籤</th></tr></thead><tbody id="songs"></tbody></table></div><div class="pager"><button id="prev">上一頁</button><span id="page"></span><button id="next">下一頁</button></div></section>
<section id="themes" class="hide"><h2>圖片意象候選集</h2><p>保留原有 31 組，再按發現組的意象詞出現次數選出 29 組，組成 60 組核心；其餘 20 組作擴充。出現次數只代表字詞匹配，並不保證圖片適合。</p><div class="filters"><select id="tier"><option value="">全部 80 組</option><option value="core">60 組核心</option><option value="extra">20 組擴充</option><option value="original">原有 31 組</option></select></div><div class="grid" id="sceneCards"></div></section>
<section id="comparison" class="hide"><h2>保留組比較 · 凍結的基準方法</h2><p id="splitInfo"></p><div class="note">「畫面詞涵蓋」以這份 80 組候選詞表作分母，因此 80 組的 100% 是定義所致，<strong>不是準確率</strong>。需要人工評審真實搜尋圖片，才能判斷實際成效。</div><div class="tablewrap" style="margin-top:18px"><table><thead><tr><th>意象數量</th><th>有候選意象詞的歌曲</th><th>畫面詞涵蓋</th><th>前三個建議含匹配詞</th><th>實際用到的意象</th></tr></thead><tbody id="comparisonRows"></tbody></table></div><h2>使用建議</h2><p>採用分層結構：內容主題、圖片意象、氣氛修飾詞分開。60 組可作核心起點，保留較少見的擴充意象；不要把 60 或 80 當作已證明的最佳數目。提供三個候選和可編輯搜尋詞。</p></section>
<section id="method" class="hide"><h2>資料來源</h2><div id="sourceInfo"></div><h2>分析流程</h2><ol><li>取得公開中文歌詞，清除和弦、導覽、相關歌曲連結及重複行。</li><li>繁簡正規化；同名保守合併、相同歌詞及高度相似版本去重；排除組曲、英文及不足 30 個中文字的資料。</li><li>本機 118 MB MiniLM：歌詞向量佔 85%，歌名佔 15%；長歌詞分段處理。</li><li>從原文分詞抽取數個關鍵字，按語意、辨識度及多樣性排序。</li><li>內容主題與圖片意象各自比較向量；明確意象詞和歌名匹配加分。</li><li>按固定雜湊分為發現組與保留組；60 組的選擇只使用發現組。</li></ol><h2>限制</h2><ul id="limits"></ul><p>模型與主題表可獨立更新。增加意象只需增加文字描述，毋須重訓或下載較大模型。</p></section>
</main><script id="data" type="application/json">__DATA__</script><script>
const D=JSON.parse(document.getElementById('data').textContent),$=id=>document.getElementById(id),esc=x=>String(x??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
let page=0,filtered=D.songs;const perPage=40;
$('intro').textContent=`整理 ${D.stats.songs.toLocaleString()} 首去重後中文基督教詩歌，包含普通話、粵語及部分台語作品。每首均附內容主題、數個關鍵字、圖片意象建議及公開來源連結。`;
$('stats').innerHTML=[[D.stats.songs.toLocaleString(),'首去重歌曲'],[D.stats.language_counts.Cantonese||0,'首明確標示粵語'],[36,'組內容主題'],[80,'組圖片意象候選']].map(([n,l])=>`<div class="stat"><b>${n}</b><span>${l}</span></div>`).join('');
for(const [id,key]of [['provider','provider'],['language','language']])for(const s of [...new Set(D.songs.map(r=>r[key]))].sort())$(id).insertAdjacentHTML('beforeend',`<option value="${esc(s)}">${esc(s)}</option>`);
function tags(xs){return `<div class="tags">${xs.map(x=>`<span class="tag">${esc(x)}</span>`).join('')}</div>`}
function render(){const total=Math.max(1,Math.ceil(filtered.length/perPage));page=Math.min(page,total-1);$('count').textContent=`找到 ${filtered.length.toLocaleString()} 首歌曲`;$('songs').innerHTML=filtered.slice(page*perPage,(page+1)*perPage).map(r=>`<tr><td><a href="${esc(r.source)}" target="_blank" rel="noopener">${esc(r.title)}</a><br><span class="source">${esc(r.provider)} · ${esc(r.language)}</span>${r.review_reasons.length?`<div class="flag" title="${esc(r.review_reasons.join('；'))}">建議覆核</div>`:''}</td><td>${esc(r.theme_summary)}<br><small class="source">${esc((r.annotation_method||"自動分析").split("；")[0])}</small></td><td>${tags(r.keywords)}</td><td>${tags(r.recommended_visuals)}<small><a href="${esc(r.google_images_url)}" target="_blank" rel="noopener">${esc(r.query)} ↗</a></small></td><td>${tags(r.source_topics)}</td></tr>`).join('');$('page').textContent=`${page+1} / ${total}`;$('prev').disabled=page===0;$('next').disabled=page===total-1}
function filter(){const q=$('search').value.toLowerCase().trim();filtered=D.songs.filter(r=>(!$('provider').value||r.provider===$('provider').value)&&(!$('language').value||r.language===$('language').value)&&(!$('flagged').checked||r.review_reasons.length)&&(!q||[r.title,r.theme_summary,...r.keywords,...r.recommended_visuals,...r.source_topics].join(' ').toLowerCase().includes(q)));page=0;render()}
for(const id of ['search','provider','language','flagged'])$(id).addEventListener('input',filter);$('prev').onclick=()=>{page--;render()};$('next').onclick=()=>{page++;render()};
document.querySelectorAll('[data-tab]').forEach(b=>b.onclick=()=>{document.querySelectorAll('[data-tab]').forEach(x=>x.classList.toggle('active',x===b));for(const id of ['catalog','themes','comparison','method'])$(id).classList.toggle('hide',id!==b.dataset.tab)});
function scenes(){const tier=$('tier').value;const rows=D.scenes.filter(s=>!tier||tier==='core'&&s.in_selected_60||tier==='extra'&&!s.in_selected_60||tier==='original'&&s.in_original_31);$('sceneCards').innerHTML=rows.sort((a,b)=>b.literal_all_songs-a.literal_all_songs).map(s=>`<div class="card"><h3>${esc(s.label)}</h3><small>${s.in_original_31?'原有 31':s.in_selected_60?'核心擴充':'額外候選'} · ${s.literal_all_songs} 首有匹配詞</small><div class="bar"><span style="width:${Math.max(2,100*s.literal_all_songs/D.stats.songs)}%"></span></div><p style="font-size:13px;margin:8px 0">${esc(s.example_titles.join('、'))}</p><code>${esc(s.query)}</code></div>`).join('')}$('tier').onchange=scenes;
$('splitInfo').textContent=`發現組 ${D.stats.discovery_songs} 首；保留組 ${D.stats.holdout_songs} 首。以下數字只用保留組計算。歌曲目錄已採用分析後修訂的規則，未作獨立驗證；此表仍保留修訂前的測量。`;$('comparisonRows').innerHTML=D.stats.comparisons.filter(r=>r.split==='holdout').map(r=>`<tr><td>${r.scene_count}</td><td>${r.literal_catalogue_coverage_percent}%</td><td>${r.scene_cue_event_coverage_percent}%</td><td>${r.top3_contains_literal_cue_percent}%</td><td>${r.top3_distinct_scenes_used}</td></tr>`).join('');
$('sourceInfo').innerHTML=Object.entries(D.stats.source_counts).map(([s,n])=>`<p><b>${esc(s)}</b>：${n} 首</p>`).join('');$('limits').innerHTML=D.stats.limitations.map(x=>`<li>${esc(x)}</li>`).join('');render();scenes();
</script></html>'''


def main():
    payload = {key:json.loads((OUT/name).read_text(encoding='utf-8')) for key,name in
               [('songs','songs.json'),('scenes','master-scenes.json'),('stats','analysis.json')]}
    encoded = json.dumps(payload,ensure_ascii=False,separators=(',',':')).replace('</',r'<\/')
    (OUT/'index.html').write_text(TEMPLATE.replace('__DATA__', encoded),encoding='utf-8')
    private_log = json.loads((ROOT/'.tools/worship-research/collection-log.json').read_text(encoding='utf-8'))
    manifest = {k:private_log[k] for k in ['date','parser_version','raw_records','unique_songs','providers','languages','failures','excluded','requests']}
    manifest['seed_snapshots'] = []
    for name,url in [('sop_songs','https://sop.org/songs/'),('sop','https://sop.org/music/'),
                     ('simply_lyrics','https://simplyworship.hk/lyrics/'),('lamb_catalog_real','https://w247.net/catalog/'),
                     ('lamb_real','https://w247.net/songs/')]:
        raw=(ROOT/'.tools/worship-research'/(name+'.html')).read_bytes()
        manifest['seed_snapshots'].append({'url':url,'sha256':hashlib.sha256(raw).hexdigest(),'bytes':len(raw)})
    (OUT/'collection-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    print(f'Wrote offline catalogue with {len(payload["songs"])} songs: {OUT / "index.html"}')


if __name__=='__main__':
    main()
