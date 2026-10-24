"""Full-window tutorial: normal-paced English, no captions, no zoom/crop."""
import bisect,json,math,pathlib,re,subprocess,sys
from functools import lru_cache
from PIL import Image,ImageDraw,ImageFont
sys.stdout.reconfigure(encoding='utf-8')
ROOT=pathlib.Path(__file__).parent;REPO=ROOT.parents[2]
OUT=ROOT/'output';OUT.mkdir(exist_ok=True)
FPS=24;SIZE=(1600,900);SCALE=1
SCENES=json.loads((ROOT/'voice.json').read_text('utf-8'))
upload=json.loads((ROOT/'upload-timing.json').read_text('utf-8'))
SCENES[-2]['seconds'] += upload[-1]['seconds'] + 2
pos=0
for scene in SCENES: scene['start']=pos;pos+=scene['seconds']
S={s['id']:s for s in SCENES}
TOTAL=math.ceil(sum(s['seconds'] for s in SCENES)*FPS)/FPS
G={s['name']:s for f in ('geometry.json','upload-geometry.json','browser-geometry.json') if (ROOT/f).exists() for s in json.loads((ROOT/f).read_text('utf-8'))}
MOUSE_LABELS={'Double-click','Mouse wheel','Drag'}
def rect(shot,name):
    c=next(c for c in G[shot]['controls'] if c['name']==name)
    if name=='CompositionBackground' and shot.startswith(('49-','50-','51-','52-')):
        # The centered preview Viewbox scales its 1280 x 720 logical child.
        width=SIZE[0]-2*c['x']
        return (c['x'],c['y'],c['x']+width,c['y']+width*9/16)
    return (c['x'],c['y'],c['x']+c['w'],c['y']+c['h'])
def norm(t):return re.sub('[^a-z0-9]','',t.lower())
def when(scene,phrase=None):
    s=S[scene]
    if phrase is None:return s['start']
    words=json.loads(pathlib.Path(s['boundaries']).read_text('utf-8'))
    tokens=[norm(w['text']) for w in words]
    target=[norm(w) for w in phrase.split()]
    for i in range(len(tokens)-len(target)+1):
        if tokens[i:i+len(target)]==target:
            return max(s['start'],s['start']+words[i]['offset']/1e7-.12)
    raise ValueError('No phrase timing: '+scene+' / '+phrase)

E=[]
def box(shot,name):return rect(shot,name)
def first(shot,prefix):return next(c['name'] for c in G[shot]['controls'] if c['name'].startswith(prefix))
def event(scene,shot,phrase=None,controls=(),key=None,offset=0,sequence=None,cursor=None):
    marks=[box(shot,c) for c in controls]
    E.append(dict(t=when(scene,phrase)+offset,shot=shot,marks=marks,key=key,sequence=sequence,cursor=cursor))
event('01-start','01-empty')
event('01-start','02-name','edit its name',('ServiceTitle',))
event('01-start','02-name','date',('ServiceDate',))
event('01-start','03-panel','two dividers',('EditorSplitter','ThumbnailSplitter'))
event('01-start','03-window-000','overall window size',sequence=['03-window-',12,.12])
event('02-invocation','04-invocation',controls=('SongTitle',))
event('02-invocation','04-invocation','paste',('Lyrics',))
event('02-invocation','04-invocation','performance order empty',('SongOrder',))
event('02b-pip','04-pip-on',controls=('PipToggle',))
event('02b-pip','04-pip-off','off for in-person',('PipToggle','SlideHost'))
event('02b-pip','04-pip-streaming','on to reserve',('PipToggle','SlideHost'))
event('03-song','04-invocation',controls=('AddTab',))
event('03-song','05-new-song','add a song',('SongTitle',))
event('03-song','06-song-lyrics','square brackets',('Lyrics',))
event('03-song','07-order','performance order',('SongOrder',))
event('03-song','08-credits','copyright',('CopyrightText','SongId'))
event('04-font','10-font-list',controls=('FontPicker',))
event('04-font','11-color','color',('TextColorPicker',))
event('04-font','12-align','alignment',('FormattingControls',))
event('04-font','13-auto','Auto fits',('AutoFontSizeButton','SlideHost'))
event('04-font','14-size','fixed size',('FontSizePicker','AutoFontSizeButton'))
event('04-font','15-title-size','song title',('FontSizePicker','SlideHost'))
event('04-font','16-lyric-size','lyrics',('FontSizePicker',))
event('05-match','16-lyric-size',controls=('EditBackground',))
event('05-match','18-matched','matches the song',('SearchText',))
event('05-match','18-matched','counter',('GalleryInfo',))
for phrase,style in [('photography','photo'),('illustration','illustration'),('abstract','abstract'),('three D','sculpted'),('minimalist','minimal')]:
    event('06-styles','19-'+style,phrase,('Style_'+style,))
event('06-styles','21-gallery','Toggle',('StyleFilters',))
event('06-styles','20-scroll-000','scroll',('GalleryScroll','GalleryInfo'),sequence=['20-scroll-',6,.6])
event('07-fullscreen','21-gallery',controls=(first('21-gallery','Card_'),),key='Double-click')
event('07-fullscreen','22-full-double','full image')
event('07-fullscreen','23-back','return',key='Double-click')
event('07-fullscreen','23-back','small expand box',controls=(first('23-back','Expand_'),))
event('08-google','25-google-link',controls=('GoogleImageLink',),cursor='click')
event('08-google','59-browser-top','in your browser')
event('08-google','60-browser-scrolled','Check an')
event('09-import','26-import',controls=('BrowseButton',))
event('09-import','26-import','paste one',('PasteButton',))
event('09-import','26-import','direct image URL',('UrlText',))
event('09-import','27-apply','Select your background',('ImagePreview',))
event('09-import','27-apply','Apply',('ApplyButton',))
event('10-transform','28-transform',controls=('BackgroundBox','SlideHost'))
event('10-transform','29-zoom-000','mouse wheel',('SlideHost',),key='Mouse wheel',sequence=['29-zoom-',30,.1])
event('10-transform','30-pan-000','drag',('SlideHost',),key='Drag',sequence=['30-pan-',30,.1],cursor='pan')
event('10-transform','31-plus','plus',('ZoomIn',))
event('10-transform','32-minus','minus',('ZoomOut',))
event('11-flip-opacity','33-flip',controls=('FlipBackground',))
event('11-flip-opacity','34-opacity-000','opacity',('BackgroundOpacity',),sequence=['34-opacity-',30,.1])
event('11-flip-opacity','35-reset','Reset',('ResetBackground',))
event('12-reading','36-second-song',controls=('SongTabs','Lyrics','SongOrder'))
event('12-reading','37-third-song','third',('SongTabs','Lyrics'))
event('12-reading','38-reading','Scripture reading',('SongTabs','Lyrics'))
event('12-reading','38-reading','performance order empty',('SongOrder',))
event('12-reading','39-reorder','Drag tabs',('SongTabs',))
event('13-shortcuts','39-field-focus',controls=('Lyrics',))
event('13-shortcuts','40-escape','Escape',('Lyrics',),key='Esc')
event('13-shortcuts','41-right','Left and right',('SongTabs',),key='→')
event('13-shortcuts','43-down','up and down',('Thumbnails','SlideHost'),key='↓')
event('13-shortcuts','46-pagedown','Page Down',('HistoryList',),key='Page Down')
event('13-shortcuts','47-pageup','Page Up',('HistoryList',),key='Page Up')
event('14-preview','48-before-preview',controls=('PreviewButton',),key='F5')
event('14-preview','49-preview','review the entire service')
event('14-preview','50-preview-next','arrow keys',('CompositionBackground',),key='↓')
event('14-preview','53-export','Escape',key='Esc')
event('15-export','53-export',controls=('ExportButton',),cursor='click')
event('15-export','61-save-dialog','save an editable PowerPoint')
event('15-export','62-saved','ready for projection')
event('16-upload','54-upload',controls=('UploadFolder',))
event('16-upload','55-upload-path','filename',('UploadName','UploadFolder'))
event('16-upload','56-upload-ready','path here',('StartUpload',),cursor='click')
start=when('16-upload')+S['16-upload']['duration']+.8
for record in upload:E.append(dict(t=start+record['seconds'],shot=record['shot'],marks=[],key=None,sequence=None,cursor=None))
event('17-complete','58-upload-complete',controls=('Outcome',))
event('17-complete','58-upload-complete','folder link',('FolderLink',),cursor='click')
event('17-complete','63-drive-folder','uploaded file',('UploadedFile',))
event('17-complete','63-drive-folder','ready to share')
E.sort(key=lambda e:e['t']);TIMES=[e['t'] for e in E]
@lru_cache(65)
def base(shot):
    return Image.open(ROOT/'rendered'/(shot+'.png')).convert('RGB').resize(SIZE,Image.Resampling.LANCZOS)
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',27)

def arrow_polygon(start,tip):
    dx,dy=tip[0]-start[0],tip[1]-start[1];length=math.hypot(dx,dy)
    ux,uy=dx/length,dy/length;px,py=-uy,ux
    neck=(tip[0]-ux*43,tip[1]-uy*43)
    def offset(p,w):return (p[0]+px*w,p[1]+py*w)
    return [offset(start,8),offset(neck,8),offset(neck,27),tip,offset(neck,-27),offset(neck,-8),offset(start,-8)]

def arrow_regions(marks):
    """Large arrows sit outside each target and avoid other marked controls."""
    placed=[];polygons=[]
    def overlap(a,b):return max(0,min(a[2],b[2])-max(a[0],b[0]))*max(0,min(a[3],b[3])-max(a[1],b[1]))
    for x,y,r,b in marks:
        cx,cy=(x+r)/2,(y+b)/2
        candidates=[('right',(r+182,cy+42),(r+9,cy)),('left',(x-182,cy-42),(x-9,cy)),
                    ('below',(cx-42,b+182),(cx,b+9)),('above',(cx+42,y-182),(cx,y-9))]
        preference=['right','below','above','left'] if x<220 else (['left','below','above','right'] if r>1450 and r-x<220 else (['below','above','left','right'] if b-y<100 else ['left','right','above','below']))
        options=[]
        for side,start,tip in candidates:
            poly=arrow_polygon(start,tip)
            bounds=(min(p[0] for p in poly)-4,min(p[1] for p in poly)-4,max(p[0] for p in poly)+4,max(p[1] for p in poly)+4)
            if bounds[0]<8 or bounds[1]<8 or bounds[2]>1592 or bounds[3]>892:continue
            penalty=sum(overlap(bounds,other) for other in marks+placed)*8+preference.index(side)*120
            options.append((penalty,poly,bounds))
        if options:
            _,poly,bounds=min(options,key=lambda option:option[0]);polygons.append(poly);placed.append(bounds)
    return polygons
def frame(t):
    e=E[max(0,bisect.bisect_right(TIMES,t)-1)];age=t-e['t'];shot=e['shot']
    if e['sequence']:
        prefix,count,step=e['sequence'];shot=prefix+f'{min(count-1,int(age/step)):03}'
    im=base(shot).copy();overlay=Image.new('RGBA',SIZE);dr=ImageDraw.Draw(overlay)
    for x,y,r,b in e['marks']:
        bounds=(max(2,x-4),max(2,y-4),min(SIZE[0]-3,r+4),min(SIZE[1]-3,b+4))
        dr.rounded_rectangle(bounds,radius=6,outline=(255,255,255,240),width=7)
        dr.rounded_rectangle(bounds,radius=6,outline=(224,91,48,255),width=3)
    for poly in arrow_regions(e['marks']):
        dr.polygon([(x+2,y+3) for x,y in poly],fill=(30,40,35,100))
        dr.polygon(poly,fill='#FF8A00',outline='white',width=3)
    if e['cursor']=='pan':
        x,y,r,b=box(e['shot'],'SlideHost');f=min(1,age/3);cx=x+(r-x)*.43+90*f;cy=y+(b-y)*.42+22*f
        dr.ellipse((cx-8,cy-8,cx+8,cy+8),fill='#f7c561',outline='#254c3a',width=2)
    if e['cursor']=='click' and e['marks']:
        x,y,r,b=e['marks'][-1];cx,cy=(x+r)/2,(y+b)/2
        dr.polygon([(cx,cy),(cx+5,cy+23),(cx+12,cy+15),(cx+23,cy+15)],fill='white',outline='#254c3a',width=2)
        if .65<age<1.3:
            radius=8+24*(age-.65)/.65
            dr.ellipse((cx-radius,cy-radius,cx+radius,cy+radius),outline='#FF8A00',width=4)
    # Keyboard shortcuts remain in the narration and edit metadata, without key badges.
    if e['key'] in MOUSE_LABELS and age<3:
        text=e['key'];bb=dr.textbbox((0,0),text,font=font);w=bb[2]+36
        dr.rounded_rectangle((24,810,24+w,866),radius=9,fill=(34,65,51,245),outline=(255,255,255,255),width=2)
        dr.text((42,818),text,font=font,fill='white')
    return Image.alpha_composite(im.convert('RGBA'),overlay).convert('RGB')
def audio():
    files=[]
    for s in SCENES:
        target=ROOT/'audio'/(s['id']+'.wav');files.append(target)
        subprocess.run(['ffmpeg','-y','-v','error','-i',s['audio'],'-af',f'apad,atrim=duration={s["seconds"]}','-ar','48000','-ac','1',str(target)],check=True)
    concat=ROOT/'audio-concat.txt';concat.write_text('\n'.join("file '"+p.as_posix()+"'" for p in files),'utf-8')
    result=ROOT/'narration.wav'
    subprocess.run(['ffmpeg','-y','-v','error','-f','concat','-safe','0','-i',str(concat),'-af',f'loudnorm=I=-16:TP=-1.5:LRA=7,apad,atrim=duration={TOTAL}','-ar','48000','-ac','1',str(result)],check=True)
    return result
def stills():
    qa=ROOT/'qa';qa.mkdir(exist_ok=True)
    picks=[s['start']+s['duration']*.55 for s in SCENES]
    contact=Image.new('RGB',(1280,math.ceil(len(picks)/3)*260),'#e2e8e2');dr=ImageDraw.Draw(contact)
    f=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',17)
    for i,t in enumerate(picks):
        im=frame(t);im.save(qa/f'{i+1:02}.jpg',quality=95);im.thumbnail((420,236));x=i%3*426;y=i//3*260
        contact.paste(im,(x,y));dr.text((x+6,y+238),f'{i+1:02} | {t:.1f} seconds',font=f,fill='#173e35')
    contact.save(ROOT/'contact.jpg',quality=94)
    for name,scene,phrase in [('splitters','01-start','two dividers'),('empty-order','02-invocation','performance order empty'),('pip-off','02b-pip','off for in-person'),('keyboard-history','13-shortcuts','Page Down'),('keyboard-preview','14-preview','arrow keys'),('fullscreen-key','07-fullscreen','return'),('drive-folder','17-complete','uploaded file')]:
        frame(when(scene,phrase)+.3).save(qa/(name+'.jpg'),quality=94)
    frame(when('10-transform')+1).save(OUT/'poster.jpg',quality=94)
def main():
    (ROOT/'edit-timeline.json').write_text(json.dumps(E,ensure_ascii=False,indent=2),'utf-8')
    (ROOT/'overlay-settings.json').write_text(json.dumps({'keyboard_badges':False,'mouse_gesture_labels':sorted(MOUSE_LABELS),'highlight_arrows':True,'arrow_color':'#FF8A00'},indent=2),'utf-8')
    (ROOT/'narration.txt').write_text('\n\n'.join(s['text'] for s in SCENES),'utf-8')
    stills()
    if '--stills' in sys.argv: print('QA ready',TOTAL);return
    narration=audio();target=OUT/'worship-pptx-0.93.34-english-review.mp4'
    cmd=['ffmpeg','-y','-v','error','-f','rawvideo','-pix_fmt','rgb24','-s','1600x900','-r',str(FPS),'-i','pipe:0','-i',str(narration),'-c:v','libx264','-preset','fast','-crf','19','-pix_fmt','yuv420p','-c:a','aac','-b:a','160k','-t',str(TOTAL),'-movflags','+faststart','-metadata','title=Worship PPTX 0.93.34 — tutorial review','-metadata:s:a:0','language=eng',str(target)]
    with (ROOT/'encode.log').open('w') as log:
        proc=subprocess.Popen(cmd,stdin=subprocess.PIPE,stderr=log)
        for i in range(round(TOTAL*FPS)):
            proc.stdin.write(frame(i/FPS).tobytes())
            if i%(15*FPS)==0:print(f'Rendered {i/FPS:.0f}/{TOTAL:.1f} seconds',flush=True)
        proc.stdin.close()
        if proc.wait():raise RuntimeError('Video encoding failed')
    print('DONE',target,flush=True)
if __name__=='__main__':main()
