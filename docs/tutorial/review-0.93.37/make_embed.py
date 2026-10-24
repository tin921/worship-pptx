"""Create a smaller H.264 copy for GitHub's inline attachment player."""
import hashlib,json,subprocess
from pathlib import Path

ROOT=Path(__file__).resolve().parent
source=ROOT/'output/worship-pptx-0.93.37-english-review.mp4'
target=ROOT/'output/worship-pptx-0.93.37-english-inline.mp4'
subprocess.run(['ffmpeg','-y','-v','error','-i',str(source),'-c:v','libx264',
                '-preset','slow','-crf','26','-pix_fmt','yuv420p','-c:a','aac',
                '-b:a','80k','-movflags','+faststart','-metadata:s:a:0','language=eng',str(target)],check=True)
assert target.stat().st_size<10_000_000,'GitHub inline copy exceeds the free-account attachment limit'
probe=json.loads(subprocess.check_output(['ffprobe','-v','error','-show_format','-show_streams','-of','json',str(target)]))
assert float(probe['format']['duration'])<300
subprocess.run(['ffmpeg','-v','error','-i',str(target),'-f','null','-'],check=True)
(ROOT/'inline-video.json').write_text(json.dumps({'file':target.relative_to(ROOT).as_posix(),
 'bytes':target.stat().st_size,'sha256':hashlib.sha256(target.read_bytes()).hexdigest(),
 'duration_seconds':float(probe['format']['duration']),'decoded_without_errors':True,
 'unchanged_edit_and_playback_speed':True},indent=2),'utf-8')
print(target.stat().st_size)
