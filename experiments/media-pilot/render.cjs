/* Reusable, deterministic worship-background pilot. All motion is 5-second periodic.
 * Needs Node, @napi-rs/canvas and ffmpeg on PATH. No network or model calls.
 */
'use strict';
const fs = require('fs');
const path = require('path');
const { spawn } = require('child_process');
const { once } = require('events');
const { createCanvas, loadImage } = require('@napi-rs/canvas');
const crypto = require('crypto');
const OUT = path.resolve(__dirname, '../../docs/media-pilot');
const W = 1920, H = 1080, FPS = 30, SECONDS = 5, FRAMES = FPS * SECONDS;
const TAU = Math.PI * 2;
const specs = JSON.parse(fs.readFileSync(path.join(OUT, 'prompts.json'), 'utf8')).images;
const canvas = createCanvas(W, H), ctx = canvas.getContext('2d');
const themeNotes = [
  ['恩典與引領', '山間步道、層疊山谷、晨光', '低處霧帶輕移，山徑保持穩定。'],
  ['委身與新生命', '麥穗、種子、柔和金光', '麥穗輕微擺動，沒有落粒或快速生長。'],
  ['重新得力與自由', '飛鷹、山巒、開闊天空', '遠處飛鷹微微滑翔，低處霧帶緩動。'],
  ['掌權與安穩', '平靜海面、雨後微光', '海面曲線緩緩起伏，光度保持穩定。'],
  ['安息與親近', '湖面、漣漪、靜謐暮色', '細小漣漪輕動，沒有閃爍或跳動。']
];
function gradient(top, bottom) { const g=ctx.createLinearGradient(0,0,0,900);g.addColorStop(0,top);g.addColorStop(1,bottom);ctx.fillStyle=g;ctx.fillRect(0,0,1600,900); }
function glow(x,y,r,c) {const g=ctx.createRadialGradient(x,y,0,x,y,r);g.addColorStop(0,c);g.addColorStop(1,'transparent');ctx.fillStyle=g;ctx.fillRect(x-r,y-r,2*r,2*r);}
function shape(d,fill,stroke=null,width=1) {const {Path2D}=require('@napi-rs/canvas');const p=new Path2D(d);if(fill){ctx.fillStyle=fill;ctx.fill(p);}if(stroke){ctx.strokeStyle=stroke;ctx.lineWidth=width;ctx.stroke(p);}}
function hill(base, amp, phase, color, shift=0) {
  ctx.beginPath();ctx.moveTo(-40,960);
  for(let x=-40;x<=1640;x+=12) {const y=base+amp*(.6*Math.sin(x/310+phase)+.4*Math.sin(x/530+phase*2));ctx.lineTo(x+shift,y);}
  ctx.lineTo(1680,960);ctx.closePath();ctx.fillStyle=color;ctx.fill();
}
function fog(y, phase, color='rgba(199,222,214,0.065)') {
  for(let i=0;i<5;i++) {ctx.beginPath();ctx.moveTo(-100,y+i*14);ctx.bezierCurveTo(370,y-35+i*14+5*Math.sin(phase),920,y+25+i*14,1700,y-8+i*14+4*Math.cos(phase));ctx.strokeStyle=color;ctx.lineWidth=11-i;ctx.stroke();}
}
function dots(seed, color, lower=520) {
  let s=seed;function rand(){s=(s*1664525+1013904223)>>>0;return s/4294967296;}
  ctx.fillStyle=color;
  for(let i=0;i<155;i++){const x=rand()*1600,y=lower+rand()*(900-lower),r=.4+rand();ctx.beginPath();ctx.arc(x,y,r,0,TAU);ctx.fill();}
}
function grace(p) {
  gradient('#102a30','#54776a');glow(1060,650,470,'rgba(214,194,126,0.28)');
  ctx.fillStyle='#a7ad85';ctx.beginPath();ctx.arc(1110,657,64,0,TAU);ctx.fill();
  hill(637,40,1.1,'#7b9582');hill(704,60,3,'#567a70');hill(760,67,5,'#325b53');hill(845,67,2.1,'#173f3c');
  shape('M 690 925 C 790 830 1100 861 1030 790 C 1000 759 927 747 1029 706 C 1090 681 1096 677 1110 665 C 1084 699 1015 717 1002 737 C 966 780 1156 791 1108 829 C 1060 866 880 881 850 925 Z','#b5ad87');
  shape('M 770 926 C 835 858 1140 841 1061 795 C 1020 768 966 742 1036 711',null,'rgba(238,218,165,.32)',2);
  fog(717,p);dots(42,'rgba(211,216,157,.16)',760);
}
function stalk(x,y,h,angle,p,color) {
  ctx.save();ctx.translate(x,y);ctx.rotate(angle+.012*Math.sin(p+x/480));
  shape(`M 0 0 Q -15 ${-h*.6} 0 ${-h}`,null,color,2.1);
  for(let k=0;k<8;k++) {const yy=-h+k*11;ctx.save();ctx.translate(0,yy);
    for(const side of [-1,1]) {ctx.save();ctx.rotate(side*.7);ctx.beginPath();ctx.ellipse(side*9,-6,5,15,0,0,TAU);ctx.fillStyle=color;ctx.fill();ctx.restore();
      shape(`M ${side*12} -7 L ${side*27} -42`,null,color,.65);}
    ctx.restore();}
  shape(`M 0 ${-h*.35} Q 65 ${-h*.64} 55 ${-h*.74} Q 25 ${-h*.68} 0 ${-h*.35}` ,color);
  ctx.restore();
}
function wheat(p) {
  gradient('#292237','#705046');glow(1000,697,500,'rgba(213,155,87,.24)');
  hill(763,20,1,'#806047');hill(804,26,4,'#574331');
  for(let i=0;i<43;i++) {const x=i*43-80,h=95+46*(1+Math.sin(i*2.7));stalk(x,920,h,.06*Math.sin(i),p,'#8f7049');}
  for(let i=0;i<12;i++){const x=[-70,35,145,270,402,990,1120,1260,1390,1510,1620,1720][i];stalk(x,950,250+38*Math.sin(i*1.8),-.15+.3*Math.sin(i),p,'#c1a16b');}
  dots(94,'rgba(235,209,143,.17)',590);
}
function eagle(p) {
  gradient('#1d2442','#6f7899');glow(1140,650,410,'rgba(220,184,177,.25)');
  hill(690,40,1,'#78819b');hill(746,44,4,'#565f81');hill(804,65,2,'#343d65');hill(896,87,5,'#202b50');
  fog(746,p,'rgba(219,217,237,.085)');
  ctx.save();ctx.translate(1170+9*Math.sin(p),604+2.5*Math.cos(p));ctx.rotate(.018*Math.sin(p));
  shape('M -55 -10 Q -28 -3 -7 5 L -3 2 L 4 4 Q 23 -9 52 -16 Q 28 4 9 8 L 4 19 L -2 13 L -7 21 L -10 9 Q -35 4 -55 -10 Z','#e0d4bd');ctx.restore();
  for(let i=0;i<7;i++){ctx.beginPath();ctx.ellipse(1040,785,390+i*29,24+i*7,-.14,Math.PI*.98,Math.PI*1.8);ctx.strokeStyle='rgba(187,194,224,.065)';ctx.lineWidth=1;ctx.stroke();}
}
function ocean(p) {
  gradient('#0d2b35','#3d706d');glow(420,628,390,'rgba(215,180,105,.24)');
  ctx.fillStyle='#b8ad7e';ctx.beginPath();ctx.ellipse(420,650,63,63,0,0,TAU);ctx.fill();
  ctx.fillStyle='#27595b';ctx.fillRect(0,678,1600,222);
  for(let j=0;j<18;j++) {ctx.beginPath();for(let x=-30;x<=1630;x+=12){const y=681+j*15+(3+j*.15)*Math.sin(x/225+p+j*.47);if(x===-30)ctx.moveTo(x,y);else ctx.lineTo(x,y);}ctx.strokeStyle=j%3===0?'rgba(201,188,139,.34)':'rgba(140,189,176,.17)';ctx.lineWidth=1+j*.14;ctx.stroke();}
  shape('M 1220 710 C 1350 701 1400 660 1462 640 C 1530 615 1570 624 1615 613 L 1630 748 Z','#153d48');
  for(let i=0;i<18;i++){ctx.beginPath();ctx.ellipse(418+2*Math.sin(p+i),695+i*10,29+i*3.4,1.2,0,0,TAU);ctx.fillStyle=`rgba(214,185,124,${.15*(1-i/22)})`;ctx.fill();}
}
function still(p) {
  gradient('#1b3039','#617e7a');glow(1060,650,400,'rgba(191,202,163,.14)');
  hill(704,22,2,'#638079');hill(732,29,4,'#3f635e');
  ctx.fillStyle='#28494f';ctx.fillRect(0,756,1600,144);
  for(let i=0;i<10;i++) {ctx.beginPath();ctx.ellipse(1060,803,65+i*43+2*Math.sin(p+i*.4),4+i*6+Math.sin(p+i*.4),0,0,TAU);ctx.strokeStyle=`rgba(176,203,188,${.19-i*.011})`;ctx.lineWidth=1.3;ctx.stroke();}
  fog(750,p,'rgba(196,218,203,.07)');
  for(let i=0;i<8;i++) {ctx.save();ctx.translate(80+i*31,930);ctx.rotate(-.22+i*.027+.007*Math.sin(p+i));
  shape(`M 0 0 Q -8 -92 0 ${-160-i*8}`,null,'#142f35',2);shape(`M 0 ${-145-i*8} Q 45 ${-180-i*7} 47 ${-172-i*7} Q 25 ${-130-i*7} 0 ${-145-i*8}`,'#294b4c');ctx.restore();}
}
const painters=[grace,wheat,eagle,ocean,still];
function graphic(index,t) {ctx.setTransform(1,0,0,1,0,0);ctx.clearRect(0,0,W,H);ctx.save();ctx.scale(W/1600,H/900);painters[index](TAU*((t%SECONDS+SECONDS)%SECONDS)/SECONDS);ctx.restore();}
function photo(img,t) {
  const p=TAU*((t%SECONDS+SECONDS)%SECONDS)/SECONDS;
  // Cosine easing reaches zero camera velocity at the loop boundary.
  const ease=(1-Math.cos(p))/2;
  const z=1.025+.003*ease,scale=Math.max(W/img.width,H/img.height)*z;
  const dw=img.width*scale,dh=img.height*scale;
  ctx.setTransform(1,0,0,1,0,0);ctx.clearRect(0,0,W,H);
  ctx.drawImage(img,(W-dw)/2+4*(2*ease-1),(H-dh)/2+2*(2*ease-1),dw,dh);
}
const sha=b=>crypto.createHash('sha256').update(b).digest('hex');
async function video(dest,render) {
  // Fixed quantization avoids an opening I-frame quality jump in detailed photos.
  const proc=spawn('ffmpeg',['-hide_banner','-loglevel','error','-y','-f','rawvideo','-pixel_format','rgba','-video_size',`${W}x${H}`,'-framerate',String(FPS),'-i','pipe:0','-an','-c:v','libx264','-preset','medium','-qp','14','-x264-params','ipratio=1:pbratio=1:scenecut=0','-g','150','-keyint_min','150','-pix_fmt','yuv420p','-profile:v','high','-level:v','4.0','-movflags','+faststart','-frames:v',String(FRAMES),dest],{windowsHide:true,stdio:['pipe','ignore','pipe']});
  let err='';proc.stderr.on('data',d=>err+=d);const done=once(proc,'close');
  for(let f=0;f<FRAMES;f++){render(f/FPS);const raw=Buffer.from(ctx.getImageData(0,0,W,H).data.buffer);if(!proc.stdin.write(raw))await once(proc.stdin,'drain');}
  proc.stdin.end();const [code]=await done;if(code!==0)throw Error(err);
}
async function main(){
 const validation=[];const justStills=process.argv.includes('--stills');
 for(let i=0;i<specs.length;i++){
  const s=specs[i],dir=path.join(OUT,s.id);fs.mkdirSync(dir,{recursive:true});
  graphic(i,0);fs.writeFileSync(path.join(dir,'graphic.png'),canvas.toBuffer('image/png'));
  const start=sha(ctx.getImageData(0,0,W,H).data);graphic(i,5);const end=sha(ctx.getImageData(0,0,W,H).data);
  if(start!==end)throw Error('Graphic loop mismatch: '+s.id);
  const img=await loadImage(path.join(dir,'photo.png'));
  photo(img,0);const photoStart=sha(ctx.getImageData(0,0,W,H).data);photo(img,5);const photoEnd=sha(ctx.getImageData(0,0,W,H).data);
  if(photoStart!==photoEnd)throw Error('Photo loop mismatch: '+s.id);
  if(!justStills){if(!process.argv.includes('--photos')){await video(path.join(dir,'graphic-loop.mp4'),t=>graphic(i,t));console.log(s.id+' graphic complete');}await video(path.join(dir,'photo-loop.mp4'),t=>photo(img,t));console.log(s.id+' animated-photo complete');}
  validation.push({id:s.id,graphic_phase0_sha256:start,graphic_phase5_sha256:end,photo_phase0_sha256:photoStart,photo_phase5_sha256:photoEnd,period_seconds:5,rendered_frames:justStills?0:FRAMES});
 }
 fs.writeFileSync(path.join(OUT,'render-validation.json'),JSON.stringify(validation,null,2));
 fs.writeFileSync(path.join(OUT,'catalog.json'),JSON.stringify({created:'2026-10-21',width:W,height:H,fps:FPS,duration_seconds:SECONDS,selection:'Curated familiar-song pilot; not a verified current top-five ranking.',photo_loop_type:'Animated AI still with periodic camera drift; not generated natural scene motion.',songs:specs.map((s,i)=>({id:s.id,title:s.title,theme:themeNotes[i][0],motifs:themeNotes[i][1],graphic_motion:themeNotes[i][2],files:{graphic:s.id+'/graphic.png',graphic_loop:s.id+'/graphic-loop.mp4',photo:s.id+'/photo.png',photo_loop:s.id+'/photo-loop.mp4'}}))},null,2));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
