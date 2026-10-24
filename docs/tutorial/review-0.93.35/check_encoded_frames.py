"""Extract actual MP4 frames for visual QA after encoding."""
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parent
timeline = json.loads((ROOT / 'edit-timeline.json').read_text('utf-8'))
video = ROOT / 'output/worship-pptx-0.93.35-english-review.mp4'
shots = ['03-remove-selected', '03-remove-confirm', '03-removed-next',
         '04-pip-off', '22-full-double', '63-drive-folder',
         '64-feedback-link', '65-feedback-doc']
records = []
for shot in shots:
    event = next(item for item in timeline if item['shot'] == shot)
    second = event['t'] + .5
    target = ROOT / 'qa' / ('encoded-' + shot + '.jpg')
    subprocess.run(['ffmpeg', '-y', '-v', 'error', '-ss', str(second),
                    '-i', str(video), '-frames:v', '1', '-q:v', '2', str(target)], check=True)
    records.append({'shot': shot, 'seconds': second, 'path': target.relative_to(ROOT).as_posix()})
(ROOT / 'qa/encoded-frames.json').write_text(json.dumps(records, indent=2), 'utf-8')
print(f'Extracted {len(records)} encoded frames for visual review.')
