"""Preserve aspect ratio when framing real native captures for the edit."""
from pathlib import Path
import json
from PIL import Image, ImageOps, ImageEnhance

ROOT=Path(__file__).parent
for source,target in [('fullscreen-double','22-full-double'),('browser-top','59-browser-top'),('browser-scrolled','60-browser-scrolled'),('drive-folder','63-drive-folder'),('feedback-doc','65-feedback-doc')]:
    im=Image.open(ROOT/'native'/f'{source}.png').convert('RGB')
    original=im.size
    canvas=Image.new('RGB',(1600,900),'#e7ebe6')
    im=ImageOps.contain(im,(1600,900),Image.Resampling.LANCZOS)
    canvas.paste(im,((1600-im.width)//2,(900-im.height)//2))
    canvas.save(ROOT/'rendered'/f'{target}.png')
    if source=='drive-folder':
        x,y,r,b=json.loads((ROOT/'native/drive-folder-region.json').read_text('utf-8'))['file_bounds']
        sx,sy=im.width/original[0],im.height/original[1]
        dx,dy=(1600-im.width)//2,(900-im.height)//2
        geometry=[{'name':target,'width':1600,'height':900,'controls':[{'name':'UploadedFile','x':dx+x*sx,'y':dy+y*sy,'w':(r-x)*sx,'h':(b-y)*sy}]}]
        (ROOT/'browser-geometry.json').write_text(json.dumps(geometry,indent=2),'utf-8')

im=Image.open(ROOT/'native/save-dialog.png').convert('RGB')
im=ImageOps.contain(im,(1240,760),Image.Resampling.LANCZOS)
canvas=ImageEnhance.Brightness(Image.open(ROOT/'rendered/53-export.png').convert('RGB')).enhance(.75)
canvas.paste(im,((1600-im.width)//2,(900-im.height)//2))
canvas.save(ROOT/'rendered/61-save-dialog.png')

# Native export confirmation is retained in full; no fake success text is added.
im=Image.open(ROOT/'native/export-complete.png').convert('RGB')
im=ImageOps.contain(im,(1600,900),Image.Resampling.LANCZOS)
canvas=Image.new('RGB',(1600,900),'#e7ebe6')
canvas.paste(im,((1600-im.width)//2,(900-im.height)//2))
canvas.save(ROOT/'rendered/62-saved.png')
