"""Record fresh capture provenance and check for reused older tutorial frames."""
import datetime, hashlib, json, pathlib

ROOT=pathlib.Path(__file__).resolve().parent
REPO=ROOT.parents[2]
def digest(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

older={}
for p in (REPO/'docs').rglob('*'):
    if p.is_file() and ROOT not in p.parents and 'tutorial' in str(p).lower() and p.suffix.lower() in {'.png','.jpg','.jpeg','.webp'}:
        older.setdefault(digest(p),[]).append(str(p.relative_to(REPO)))

records=[]
reused=[]
for folder in ('native','rendered'):
    for p in sorted((ROOT/folder).glob('*.png')):
        sha=digest(p)
        records.append({'path':p.relative_to(ROOT).as_posix(),'sha256':sha,
          'captured_local':datetime.datetime.fromtimestamp(p.stat().st_mtime).astimezone().isoformat()})
        if sha in older:
            reused.append({'new':p.relative_to(ROOT).as_posix(),'older':older[sha]})

report={'app_version':'0.93.33','capture_sources':['current app rendered with Avalonia/Skia','current Windows app and browser captures'],
        'previous_tutorial_image_hashes_checked':len(older),'reused_previous_tutorial_images':reused,'captures':records}
(ROOT/'capture-manifest.json').write_text(json.dumps(report,indent=2),'utf-8')
assert not reused,reused
print(f'{len(records)} fresh capture files; no matches with {len(older)} older tutorial image hashes.')
