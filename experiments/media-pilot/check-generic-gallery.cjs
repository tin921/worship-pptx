'use strict';
const {chromium}=require('playwright'),path=require('path'),fs=require('fs'),{pathToFileURL}=require('url');
(async()=>{
 const browser=await chromium.launch({headless:true,channel:'msedge'});
 try{
  const page=await browser.newPage({viewport:{width:1440,height:1080}});
  page.setDefaultTimeout(12000);
  const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto(pathToFileURL(path.resolve(__dirname,'../../docs/media-pilot/generic/index.html')).href);
  if(await page.locator('body').getAttribute('data-lighting')!=='daylight')throw Error('Daylight must open first');
  for(const [mode,w,h,color] of [['daylight',1280,720,'rgb(34, 55, 75)'],['night',1920,1080,'rgb(255, 255, 255)']]){
   await page.locator('#'+mode).click();
   if(await page.locator('.card').count()!==6)throw Error('Missing cards');
   if(await page.locator('#'+mode).getAttribute('aria-pressed')!=='true')throw Error('Lighting selection failed');
   if(await page.locator('.lyrics').first().evaluate(x=>getComputedStyle(x).color)!==color)throw Error('Incorrect lyric color');
   if(!(await page.locator('#status').innerText()).includes(h+'p'))throw Error('Incorrect resolution label');
   for(let i=0;i<6;i++){
    const card=page.locator('.card').nth(i);await card.scrollIntoViewIfNeeded();
    const v=card.locator('video');
    await v.evaluate(v=>new Promise((resolve,reject)=>{
     if(v.readyState>=2)return resolve();
     v.addEventListener('loadeddata',resolve,{once:true});
     v.addEventListener('error',()=>reject(Error('Decode failure')),{once:true});
    }));
    const meta=await v.evaluate(v=>({w:v.videoWidth,h:v.videoHeight,t:v.duration,muted:v.muted,loop:v.loop}));
    if(meta.w!==w||meta.h!==h||meta.t!==5||!meta.muted||!meta.loop)throw Error('Invalid clip: '+mode);
    await v.evaluate(v=>{v.currentTime=4.9;return v.play();});
    await page.waitForFunction(i=>{const v=document.querySelectorAll('video')[i];return v.currentTime<1&&!v.paused;},i);
    const links=await card.locator('a[download]').evaluateAll(xs=>xs.map(x=>({href:x.getAttribute('href'),name:x.download})));
    if(links.length!==2||links.some(x=>!x.name.includes('-'+mode)))throw Error('Wrong download name');
    const suffix=mode==='daylight'?'-daylight':'';
    if(!links[0].href.endsWith('/graphic'+suffix+'.png')||!links[1].href.endsWith('/loop'+suffix+'.mp4'))throw Error('Wrong download file');
   }
   await page.waitForFunction(w=>[...document.querySelectorAll('.frame img')].every(x=>x.complete&&x.naturalWidth===w),w);
  }
  await page.locator('#pause').click();
  await page.locator('#daylight').click();
  if(!await page.locator('video').evaluateAll(vs=>vs.every(v=>v.paused)))throw Error('Pause not retained on lighting change');
  await page.locator('#mode').click();
  if(!await page.locator('video').evaluateAll(vs=>vs.every(v=>v.hidden)))throw Error('Still mode failed');
  await page.waitForFunction(()=>[...document.querySelectorAll('.frame img')].every(x=>!x.hidden&&x.complete&&x.naturalWidth===1280));
  for(const [label,count] of [['安靜',2],['溫暖',1],['喜樂',4],['活力',1]]){
   await page.getByRole('button',{name:label,exact:true}).click();
   if(await page.locator('.card').count()!==count)throw Error('Filter failed: '+label);
  }
  await page.locator('#text-toggle').check();await page.locator('#guide-toggle').check();
  await page.locator('#sample-text').fill('文字預覽\n第二行');
  await page.locator('#night').focus();await page.keyboard.press('Enter');
  if(await page.locator('.card').count()!==1||await page.locator('body').getAttribute('data-lighting')!=='night')throw Error('Filter/keyboard mode change failed');
  if(!await page.locator('video').evaluateAll(vs=>vs.every(v=>v.hidden&&v.paused)))throw Error('Still mode not retained');
  if(!await page.locator('.lyrics').evaluateAll(xs=>xs.every(x=>x.textContent==='文字預覽\n第二行'&&getComputedStyle(x).display==='flex')))throw Error('Sample text not retained');
  if(!await page.locator('.guide').evaluateAll(xs=>xs.every(x=>getComputedStyle(x).display==='block')))throw Error('Guide not retained');
  await page.getByRole('button',{name:'全部',exact:true}).click();
  await page.setViewportSize({width:390,height:844});
  for(const mode of ['night','daylight']){
   await page.locator('#'+mode).click();
   if(!await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth))throw Error('Mobile overflow');
  }
  await page.emulateMedia({reducedMotion:'reduce'});await page.reload();
  for(const mode of ['daylight','night']){
   await page.locator('#'+mode).click();
   if(!await page.locator('video').evaluateAll(vs=>vs.every(v=>v.hidden&&v.paused)))throw Error('Reduced motion failed');
  }
  if(errors.length)throw Error(errors.join('\n'));
  const result={passed:true,checks:['All 12 videos decode and loop in Edge','All 12 PNG images load at their expected resolution','Daylight/night selection changes assets, lyric color, resolution label and download files','Mood filters, pause, still mode, sample text and guide survive lighting changes','Lighting buttons work with keyboard','390px layout has no horizontal overflow in either lighting mode','Reduced-motion preference displays stills in both modes'],screenshots_taken:0};
  fs.writeFileSync(path.resolve(__dirname,'../../docs/media-pilot/generic/gallery-validation.json'),JSON.stringify(result,null,2));console.log(JSON.stringify(result));
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1});
