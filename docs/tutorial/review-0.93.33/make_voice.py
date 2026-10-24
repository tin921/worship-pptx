import asyncio,json,pathlib,subprocess,sys
import edge_tts
sys.stdout.reconfigure(encoding='utf-8')
ROOT=pathlib.Path(__file__).parent
async def main():
    scenes=json.loads((ROOT/'plan.json').read_text('utf-8'))
    cached={s['id']:s['text'] for s in json.loads((ROOT/'voice.json').read_text('utf-8'))} if (ROOT/'voice.json').exists() else {}
    out=ROOT/'audio';out.mkdir(exist_ok=True)
    timeline=0
    for s in scenes:
        path=out/(s['id']+'.mp3');meta=path.with_suffix('.json')
        if not (path.exists() and meta.exists() and cached.get(s['id'])==s['text']):
            for attempt in range(3):
                try:
                    boundaries=[]
                    with path.open('wb') as f:
                        async for c in edge_tts.Communicate(s['text'],'en-US-AriaNeural',rate='-5%',boundary='WordBoundary').stream():
                            if c['type']=='audio':f.write(c['data'])
                            elif c['type']=='WordBoundary':boundaries.append(c)
                    meta.write_text(json.dumps(boundaries,indent=2),'utf-8');break
                except Exception:
                    if attempt==2:raise
                    await asyncio.sleep(2)
        s['audio']=str(path);s['boundaries']=str(meta)
        s['duration']=float(subprocess.check_output(['ffprobe','-v','error','-show_entries','format=duration','-of','default=nw=1:nk=1',str(path)],text=True))
        s['start']=timeline;s['seconds']=s['duration']+s['pause'];timeline+=s['seconds']
        print(s['id'],round(s['duration'],2),'seconds',flush=True)
    (ROOT/'voice.json').write_text(json.dumps(scenes,indent=2),'utf-8')
    print('TOTAL',round(timeline,2),'WORDS',sum(len(s['text'].split()) for s in scenes),flush=True)
asyncio.run(main())
