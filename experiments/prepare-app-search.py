"""Build-time scene vectors and parity fixtures. Never reads users' lyrics."""
from pathlib import Path
import json
import numpy as np
from opencc import OpenCC
from image_keywords import Encoder

ROOT = Path(__file__).resolve().parents[1]
cc = OpenCC('t2s')
mapping = {chr(i): cc.convert(chr(i)) for lo, hi in [(0x3400,0xA000),(0xF900,0xFB00)] for i in range(lo,hi) if cc.convert(chr(i)) != chr(i)}
mapping.update({'祢':'你','祂':'他'})
(ROOT/'assets/models/normalization.json').write_text(json.dumps(mapping,ensure_ascii=False),encoding='utf-8')
def normalize(s): return ''.join(mapping.get(c,c) for c in s)
scenes=json.loads((ROOT/'docs/research/chinese-worship-corpus/master-scenes.json').read_text(encoding='utf-8'))
encoder=Encoder()
vectors=encoder.encode([normalize(s['description']) for s in scenes])
out=[dict(id=s['id'],title=s['label'],query=s['query'],core=s['in_selected_60'],cues=[normalize(c) for c in s['cues']],vector=np.round(v,8).tolist()) for s,v in zip(scenes,vectors)]
(ROOT/'assets/media/scenes.json').write_text(json.dumps(out,ensure_ascii=False,separators=(',',':')),encoding='utf-8')
texts=['山間小路走過高山低谷','山间小路走过高山低谷','恩典與慈愛 祢的手引領我','calm lake sunrise','雨後有彩虹 🌈','讚美！\n希望與喜樂','unknown\u0378letter']
fixtures=[dict(text=t,ids=encoder.tokenizer.encode(t).ids,vector=np.round(encoder.encode([t])[0],8).tolist()) for t in texts]
(ROOT/'tests/embedding-fixtures.json').write_text(json.dumps(fixtures,ensure_ascii=False),encoding='utf-8')
print(f'Prepared {len(out)} scene vectors and {len(fixtures)} tokenizer/model fixtures.')
