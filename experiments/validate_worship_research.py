"""Validate the corpus deliverables and offline catalogue logic without screenshots."""
from pathlib import Path
import csv
import json
import subprocess
import sys

from bs4 import BeautifulSoup

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'docs/research/chinese-worship-corpus'
PRIVATE=ROOT/'.tools/worship-research'


def main():
    songs=json.loads((OUT/'songs.json').read_text(encoding='utf-8'))
    scenes=json.loads((OUT/'master-scenes.json').read_text(encoding='utf-8'))
    stats=json.loads((OUT/'analysis.json').read_text(encoding='utf-8'))
    checks=json.loads((OUT/'spot-checks.json').read_text(encoding='utf-8'))
    corpus=json.loads((PRIVATE/'corpus-private.json').read_text(encoding='utf-8'))
    assert len(songs)==len(corpus)==stats['songs'] and len(songs)>=1000
    assert len({r['id'] for r in songs})==len(songs)
    assert len({r['lyrics_sha256'] for r in songs})==len(songs)
    assert len(scenes)==80 and sum(s['in_selected_60'] for s in scenes)==60
    assert sum(s['in_original_31'] for s in scenes)==31
    assert len(checks)==24 and all(r['id'] in {s['id'] for s in songs} for r in checks)
    for r in songs:
        assert r['theme_summary'] and r['keywords'] and r['annotation_method']
        assert r['source'].startswith('https://')
        assert len(r['recommended_scenes'])==3
        assert len(r['keywords'])>=2
        assert not {'lyrics','lines','full_lyrics'} & set(r)
    assert not any('→' in line or 'Related Lyrics' in line or 'KCNchord' in line for r in corpus for line in r['lines'])
    assert not any(r['title'] in ['Worthy','Embers','Build my life','Open the eyes of my Heart'] for r in corpus)
    with (OUT/'songs.csv').open(encoding='utf-8-sig',newline='') as f:
        rows=list(csv.DictReader(f))
    assert len(rows)==len(songs) and rows[0]['title']==songs[0]['title']
    html=BeautifulSoup((OUT/'index.html').read_text(encoding='utf-8'),'html.parser')
    assert not html.select('script[src]')
    payload=json.loads(html.select_one('script#data').string)
    assert len(payload['songs'])==len(songs)
    assert payload['songs']==songs
    code=html.find_all('script')[-1].string
    script=PRIVATE/'catalogue-runtime.js';script.write_text(code,encoding='utf-8')
    data=PRIVATE/'catalogue-payload.json';data.write_text(json.dumps(payload,ensure_ascii=False),encoding='utf-8')
    subprocess.run(['node','--check',str(script)],check=True)
    harness=PRIVATE/'validate-catalogue.cjs'
    harness.write_text(r'''
const fs=require('fs'),vm=require('vm'),assert=require('assert');
const data=JSON.parse(fs.readFileSync(process.argv[2],'utf8'));
const code=fs.readFileSync(process.argv[3],'utf8');
const nodes={};function el(id){return nodes[id]||(nodes[id]={id,value:'',checked:false,disabled:false,innerHTML:'',textContent:'',dataset:{},classList:{toggle(){}},addEventListener(){},insertAdjacentHTML(pos,html){this.innerHTML+=html}})}
el('data').textContent=JSON.stringify(data);
const buttons=['catalog','themes','comparison','method'].map(x=>({...el('tab-'+x),dataset:{tab:x}}));
const context=vm.createContext({document:{getElementById:el,querySelectorAll:()=>buttons},console});
vm.runInContext(code,context);
assert.strictEqual(vm.runInContext('filtered.length',context),data.songs.length);
assert.strictEqual((el('songs').innerHTML.match(/<tr>/g)||[]).length,40);
el('language').value='Cantonese';vm.runInContext('filter()',context);
assert.strictEqual(vm.runInContext('filtered.length',context),data.stats.language_counts.Cantonese);
el('language').value='';el('search').value='一粒麥子';vm.runInContext('filter()',context);
assert(vm.runInContext('filtered.some(r=>r.title==="一粒麥子")',context));
el('search').value='no-such-song-000';vm.runInContext('filter()',context);
assert.strictEqual(vm.runInContext('filtered.length',context),0);assert(el('next').disabled);
el('search').value='';el('provider').value='讚美之泉';vm.runInContext('filter()',context);
assert.strictEqual(vm.runInContext('filtered.length',context),534);
el('next').onclick();assert.strictEqual(vm.runInContext('page',context),1);
el('tier').value='extra';vm.runInContext('scenes()',context);
assert.strictEqual((el('sceneCards').innerHTML.match(/class="card"/g)||[]).length,20);
console.log('PASS offline catalogue: initial page, Cantonese/source filters, search, zero results, pagination, 20-scene expansion');
''',encoding='utf-8')
    subprocess.run(['node',str(harness),str(data),str(script)],check=True)
    print(f'PASS deliverables: {len(songs)} unique records, complete summaries/keywords/sources, 80/60/31 memberships, CSV/JSON/HTML parity, 24 diagnostic checks, no lyric fields.')


if __name__=='__main__':main()
