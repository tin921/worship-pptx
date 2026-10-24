"""Validate the static image library without application screenshots."""
from pathlib import Path
from PIL import Image
import hashlib
import json
ROOT=Path(__file__).resolve().parents[1]
MEDIA=ROOT/'assets/media'
catalog=json.loads((MEDIA/'catalog.json').read_text(encoding='utf-8'))
assert len(catalog)==270
assert len({a['id'] for a in catalog})==270
assert not list(MEDIA.rglob('*.mp4')) and not list(MEDIA.rglob('*.webp'))
records=[]
for asset in catalog:
    assert not {'motion','preview','duration'} & asset.keys(),asset['id']
    file=(MEDIA/asset['still']).resolve()
    assert file.is_relative_to(MEDIA.resolve())
    with Image.open(file) as image:
        assert image.size==(1280,720),(asset['id'],image.size)
        assert getattr(image,'n_frames',1)==1,asset['id']
    records.append(dict(id=asset['id'],sha256=hashlib.sha256(file.read_bytes()).hexdigest()))
themed=[r for r in records if not r['id'].startswith(('generic-','legacy-'))]
assert len(themed)==240 and len({r['sha256'] for r in themed})==240
(ROOT/'artifacts').mkdir(exist_ok=True)
(ROOT/'artifacts/media-validation.json').write_text(json.dumps(dict(complete=True,count=270,dimensions=[1280,720],animated_assets=0,images=records),indent=2),encoding='utf-8')
print('270 static 720p images validated; no animation assets remain in the app library.')
