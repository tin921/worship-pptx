"""Inspect deliverable media, decode every frame, and measure loop seams."""
from pathlib import Path
import json, subprocess, hashlib
import numpy as np

ROOT = Path(__file__).resolve().parents[2] / 'docs' / 'media-pilot'
catalog = json.loads((ROOT / 'catalog.json').read_text(encoding='utf-8'))
render_checks = json.loads((ROOT / 'render-validation.json').read_text(encoding='utf-8'))
rows = []
for song in catalog['songs']:
    for kind, relative in song['files'].items():
        p = ROOT / relative
        assert p.is_file(), relative
        info = json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-show_streams', '-show_format', '-of', 'json', str(p)]))
        v = next(s for s in info['streams'] if s['codec_type'] == 'video')
        row = dict(file=relative, bytes=p.stat().st_size, sha256=hashlib.sha256(p.read_bytes()).hexdigest(), width=v['width'], height=v['height'], codec=v['codec_name'])
        assert abs(v['width']/v['height']-16/9) < .002
        if p.suffix == '.mp4':
            assert (v['width'],v['height']) == (1920,1080)
            assert v['codec_name'] == 'h264' and v['pix_fmt'] == 'yuv420p'
            assert v['avg_frame_rate'] == '30/1' and int(v['nb_frames']) == 150
            assert abs(float(info['format']['duration']) - 5) < .001
            assert len(info['streams']) == 1, 'Unexpected audio stream'
            raw = subprocess.check_output(['ffmpeg','-v','error','-i',str(p),'-vf','scale=320:180','-f','rawvideo','-pix_fmt','rgb24','pipe:1'])
            frames = np.frombuffer(raw,dtype=np.uint8).reshape(-1,180,320,3).astype(np.float32)
            assert len(frames)==150
            adjacent = np.abs(frames[1:]-frames[:-1]).mean(axis=(1,2,3))
            seam = float(np.abs(frames[-1]-frames[0]).mean())
            p95 = float(np.percentile(adjacent,95))
            # Source periodicity is checked exactly below. Lossy H.264 can encode
            # identical content differently in I/P/B frames, especially when motion
            # eases to zero. Use a one-level (of 255) mean quantization floor for
            # this diagnostic, rather than dividing by almost-zero motion.
            assert seam <= max(1.5*p95,1.0), (relative,seam,p95)
            assert float(np.max(np.abs(frames[75]-frames[0]))) > 0, 'No animation detected'
            row.update(seconds=float(info['format']['duration']),fps=30,frames=150,pixel_format=v['pix_fmt'],audio=False,decoded_seam_mean_absolute_difference=round(seam,5),seam_limit_mean_pixel_levels=round(max(1.5*p95,1.0),5),adjacent_frame_difference_p95=round(p95,5),upper_half_max_mean_brightness_step=round(float(np.max(np.abs(np.diff(frames[:,:90].mean(axis=(1,2,3)))))),5))
        rows.append(row)
for r in render_checks:
    assert r['rendered_frames']==150
    assert r['graphic_phase0_sha256']==r['graphic_phase5_sha256']
    assert r['photo_phase0_sha256']==r['photo_phase5_sha256']
assert len(rows)==20
report=dict(passed=True,assets=len(rows),videos=10,stills=10,total_bytes=sum(r['bytes'] for r in rows),checks=['Every asset present','16:9 aspect ratio','All 150 frames decoded in every video','5 seconds, 30 fps, 1920x1080 H.264/yuv420p, no audio','Exact source rendering at t=0 equals t=5','Decoded seam within 1.5x adjacent-frame p95 or one pixel level out of 255, whichever is larger'],limitations=['Numerical seam checks do not guarantee gapless playback in every player','PowerPoint playback not tested on this machine','Photo loops animate a generated still; no natural scene motion','Song selection is curated and is not a verified current popularity ranking'],files=rows)
(ROOT/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(dict(passed=True,assets=len(rows),megabytes=round(report['total_bytes']/1e6,2),max_seam_difference=round(max(r.get('decoded_seam_mean_absolute_difference',0) for r in rows),4))))
