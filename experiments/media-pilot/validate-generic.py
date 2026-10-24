"""Independent decoding, loop and text-contrast checks for both lighting modes."""
from pathlib import Path
import hashlib, json, subprocess
import numpy as np

root=Path(__file__).resolve().parents[2]/'docs'/'media-pilot'/'generic'
catalog=json.loads((root/'catalog.json').read_text(encoding='utf-8'))
rows=[]

def luminance(rgb):
    linear=np.where(rgb<=.04045,rgb/12.92,((rgb+.055)/1.055)**2.4)
    return linear[...,0]*.2126+linear[...,1]*.7152+linear[...,2]*.0722

for mode,dimensions in [('night',(1920,1080)),('daylight',(1280,720))]:
    settings=catalog['lighting_modes'][mode]
    assert (settings['width'],settings['height'])==dimensions
    source=json.loads((root/('render-validation'+('-daylight' if mode=='daylight' else '')+'.json')).read_text(encoding='utf-8'))
    assert source['rendered_videos'] and len(source['checks'])==6
    for check in source['checks']:
        assert check['phase0_sha256']==check['phase5_sha256']
        assert check['phase0_sha256']!=check['phase2_5_sha256']
    color=settings['lyric_color'].lstrip('#')
    text_luminance=luminance(np.array([int(color[i:i+2],16) for i in (0,2,4)])/255)
    for mood in catalog['moods']:
        files=mood['files'] if mode=='night' else mood['variants'][mode]['files']
        for kind,relative in files.items():
            p=root/relative
            info=json.loads(subprocess.check_output(['ffprobe','-v','error','-show_streams','-show_format','-of','json',str(p)]))
            v=info['streams'][0]
            assert (v['width'],v['height'])==dimensions, relative
            row=dict(file=relative,mode=mode,bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest(),width=dimensions[0],height=dimensions[1])
            if kind=='loop':
                assert len(info['streams'])==1 and v['codec_name']=='h264' and v['pix_fmt']=='yuv420p'
                assert v['avg_frame_rate']=='30/1' and int(v['nb_frames'])==150 and abs(float(info['format']['duration'])-5)<.001
                raw=subprocess.check_output(['ffmpeg','-v','error','-i',str(p),'-vf','scale=320:180','-f','rawvideo','-pix_fmt','rgb24','pipe:1'])
                frames=np.frombuffer(raw,dtype=np.uint8).reshape(-1,180,320,3).astype(np.float32)
                assert len(frames)==150
                differences=np.abs(frames[1:]-frames[:-1]).mean(axis=(1,2,3))
                seam=float(np.abs(frames[-1]-frames[0]).mean())
                limit=max(1.5*float(np.percentile(differences,95)),1.0)
                assert seam<=limit, relative
                assert float(np.abs(frames[75]-frames[0]).mean())>.005, relative
                background_luminance=luminance(frames[:,11:83,26:294]/255)
                contrast=float(((np.maximum(background_luminance,text_luminance)+.05)/(np.minimum(background_luminance,text_luminance)+.05)).min())
                assert contrast>=4.5, (relative,contrast)
                row.update(duration_seconds=5,fps=30,frames=150,audio=False,decoded_seam_mean_pixel_levels=round(seam,5),seam_limit=round(limit,5),lyric_color=settings['lyric_color'],minimum_text_contrast_in_sampled_safe_area=round(contrast,2))
            rows.append(row)
assert len(rows)==24
report=dict(passed=True,stills=12,loops=12,total_bytes=sum(x['bytes'] for x in rows),checks=['Exact source rendering repeats at t=5 in both modes','All video frames decoded','Daylight 1280x720; original night 1920x1080','5 seconds, 30 fps H.264/yuv420p, silent','Decoded seam within 1.5x adjacent-frame p95 or one pixel level out of 255, whichever is larger','Mode-appropriate text contrast at least 4.5 in sampled lyric-safe region throughout each loop'],limitations=['PowerPoint and macOS playback have not been tested','Encoding checks do not guarantee gapless playback in every player','Contrast measured on downsampled frames, not a physical projector'],files=rows)
(root/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(dict(passed=True,assets=24,megabytes=round(report['total_bytes']/1e6,2),daylight_megabytes=round(sum(x['bytes'] for x in rows if x['mode']=='daylight')/1e6,2),minimum_daylight_contrast=min(x.get('minimum_text_contrast_in_sampled_safe_area',100) for x in rows if x['mode']=='daylight'))))
