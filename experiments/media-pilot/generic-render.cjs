'use strict';
// Generic mood backgrounds: editable Canvas artwork, 5-second periodic animation.
// Run with Node + @napi-rs/canvas and ffmpeg on PATH. No network or AI service.
const fs=require('fs'),path=require('path'),crypto=require('crypto');
const {spawn}=require('child_process'),{once}=require('events');
const {createCanvas,Path2D}=require('@napi-rs/canvas');
const library=process.argv.includes('--library');
const OUT=path.resolve(__dirname,library?'../../assets/media/generic':'../../docs/media-pilot/generic');
const daylight=process.argv.includes('--daylight');
// Preserve the original night assets; all new daylight assets use the 720p default.
const W=daylight||library?1280:1920,H=daylight||library?720:1080,FPS=30,DURATION=5,FRAMES=FPS*DURATION,TAU=Math.PI*2;
const ink=(night,day)=>daylight?day:night;
const daylightPalettes=['冰藍・雪白','晨霧・薄荷綠','奶油白・蜂蜜金','晴空藍・暖金','淡粉・薰衣草紫','海鹽綠・柔金'];
const canvas=createCanvas(W,H),c=canvas.getContext('2d');
const items=[
 {id:'01-soft-snow',title:'輕雪',english:'Soft snow',moods:['安靜','清新'],energy:1,palette:'霧藍・銀白',description:'細雪緩緩飄落，帶一點柔和的景深。',motion:'Small layered snow particles fall diagonally, fading gently near their wrap boundary.',seed:114},
 {id:'02-gentle-rain',title:'微雨',english:'Gentle rain',moods:['安靜','清新'],energy:2,palette:'深綠・青藍',description:'細雨與小漣漪，清新而不陰沉。',motion:'Fine rain falls across the lower area with slowly expanding, fading ripples.',seed:214},
 {id:'03-golden-lights',title:'暖光浮游',english:'Golden lights',moods:['溫暖','喜樂'],energy:2,palette:'莓紫・香檳金',description:'暖色光點輕輕升起，溫暖、有生命力。',motion:'Soft gold bokeh drifts upward with smooth opacity envelopes; no blinking.',seed:314},
 {id:'04-paper-flight',title:'自在飛翔',english:'Paper flight',moods:['盼望','喜樂'],energy:3,palette:'靛藍・珊瑚金',description:'幾隻紙鳥乘風滑翔，輕盈而有盼望。',motion:'Small paper birds glide slowly through shallow closed paths, with subtle banking.',seed:414},
 {id:'05-petal-breeze',title:'花瓣微風',english:'Petal breeze',moods:['喜樂','清新'],energy:3,palette:'梅紫・柔粉',description:'花瓣隨微風飄動，輕快但不喧鬧。',motion:'Petals float sideways with gentle vertical drift and slow tumbling.',seed:514},
 {id:'06-living-ribbons',title:'流光',english:'Living ribbons',moods:['活力','喜樂'],energy:4,palette:'深藍・青綠・柔金',description:'柔和光帶緩緩流動，適合較有活力的敬拜。',motion:'Layered luminous ribbons travel in smooth waves, without brightness pulses.',seed:614}
];
if(library)items.push(
 {id:'07-drifting-clouds',title:'雲端漫步',english:'Drifting clouds',moods:['安靜','盼望'],energy:1,description:'柔雲緩緩飄移，開闊而安穩。',seed:714},
 {id:'08-water-ripples',title:'水面漣漪',english:'Water ripples',moods:['安靜','清新'],energy:2,description:'一圈圈漣漪輕輕展開，平靜而有生氣。',seed:814},
 {id:'09-floating-lanterns',title:'暖燈飄揚',english:'Floating lanterns',moods:['溫暖','盼望'],energy:2,description:'小小暖燈緩緩升起，帶來溫暖與盼望。',seed:914},
 {id:'10-gentle-confetti',title:'喜樂彩紙',english:'Gentle confetti',moods:['喜樂','活力'],energy:4,description:'彩紙輕盈飄落，柔和地添上歡慶氣氛。',seed:1014}
);
function rng(seed){let v=seed;return ()=>{v=(Math.imul(v,1664525)+1013904223)>>>0;return v/4294967296;};}
const clouds=items.map(s=>{const r=rng(s.seed);return Array.from({length:100},()=>({x:r(),y:r(),a:r(),b:r(),d:r()}));});
function fillPath(d,fill,stroke=null,width=1){const p=new Path2D(d);if(fill){c.fillStyle=fill;c.fill(p);}if(stroke){c.strokeStyle=stroke;c.lineWidth=width;c.stroke(p);}}
function background(top,bottom){const g=c.createLinearGradient(0,0,0,900);g.addColorStop(0,top);g.addColorStop(1,bottom);c.fillStyle=g;c.fillRect(0,0,1600,900);}
function glow(x,y,rx,ry,color){c.save();c.translate(x,y);c.scale(1,ry/rx);const g=c.createRadialGradient(0,0,0,0,0,rx);g.addColorStop(0,color);g.addColorStop(1,daylight?color.replace(/,[\d.]+\)$/,',0)'):'transparent');c.fillStyle=g;c.fillRect(-rx,-rx,2*rx,2*rx);c.restore();}
const fract=x=>x-Math.floor(x),smooth=x=>{x=Math.max(0,Math.min(1,x));return x*x*(3-2*x);};
const fade=u=>smooth(u/.16)*smooth((1-u)/.16);
function dot(x,y,r,color){c.beginPath();c.arc(x,y,r,0,TAU);c.fillStyle=color;c.fill();}
function snow(u){
 background(ink('#102637','#edf6f9'),ink('#3e626c','#bad6e2'));glow(1170,720,460,280,ink('rgba(171,211,218,.17)','rgba(255,255,255,.42)'));
 fillPath('M -40 820 C 360 770 500 835 820 805 S 1300 760 1640 804 L 1640 940 L -40 940 Z',ink('#42636c','#d5e8ee'));
 fillPath('M -40 864 C 400 811 610 878 940 840 S 1420 805 1640 850 L 1640 940 L -40 940 Z',ink('#64828a','#f5fbfd'));
 for(const [i,p] of clouds[0].entries()) {const q=fract(p.y+u),y=425+q*530,x=p.x*1700-50+20*Math.sin(TAU*u+p.a*TAU);const alpha=fade(q)*(.16+p.d*.43),r=1+p.b*2.5;c.globalAlpha=alpha;
  if(i%5===0)glow(x,y,r*5,r*5,ink('rgba(226,245,247,.25)','rgba(255,255,255,.7)'));dot(x,y,r,ink('#d2e7e9','#ffffff'));
 }c.globalAlpha=1;
}
function rain(u){
 background(ink('#132f34','#eff6ed'),ink('#41635d','#b8d8c9'));glow(360,650,510,310,ink('rgba(157,183,133,.16)','rgba(255,250,216,.36)'));
 fillPath('M 0 770 Q 700 738 1600 785 L 1600 900 L 0 900 Z',ink('#294e51','#9bbfbd'));
 for(const p of clouds[1].slice(0,library?96:68)){const q=fract(u+p.y),y=425+q*560,x=p.x*1660-30-q*35;const length=library?26+p.d*22:14+p.d*18;c.globalAlpha=fade(q)*(library?.45+p.b*.35:.13+p.b*.23);const g=c.createLinearGradient(x,y,x-3,y+length);g.addColorStop(0,daylight?'rgba(46,99,123,0)':'transparent');g.addColorStop(1,ink('#d5eeef',library?'#2e637b':'#4f8c96'));c.strokeStyle=g;c.lineWidth=library?1.8+p.a*.9:.7+p.a*.5;c.beginPath();c.moveTo(x,y);c.lineTo(x-3,y+length);c.stroke();}c.globalAlpha=1;
 for(const p of clouds[1].slice(70,88)){const q=fract(u+p.y),x=p.x*1650-25,y=782+p.b*120;c.globalAlpha=(library?.42:.20)*Math.sin(Math.PI*q)**2;c.strokeStyle=ink('#b3d3bf','#386c78');c.lineWidth=library?1.6:1.1;c.beginPath();c.ellipse(x,y,10+q*42,2+q*7,0,0,TAU);c.stroke();}c.globalAlpha=1;
}
function lights(u){
 background(ink('#2d2339','#fff7e5'),ink('#6b4644','#eccda8'));glow(1150,740,440,280,ink('rgba(235,163,92,.22)','rgba(255,252,231,.55)'));glow(330,820,370,220,ink('rgba(192,111,119,.20)','rgba(241,181,142,.28)'));
 for(const [i,p] of clouds[2].slice(0,60).entries()){const q=fract(u+p.y),y=960-q*480,x=p.x*1680-40+28*Math.sin(TAU*u+p.a*TAU),r=2+p.b*8;c.globalAlpha=fade(q)*(.18+p.d*.28);glow(x,y,r*4,r*4,ink('rgba(249,213,158,.30)','rgba(255,255,241,.65)'));dot(x,y,r,i%3?ink('#e5bc82','#b9833b'):ink('#d69d91','#ce9876'));if(i%4===0){c.strokeStyle=ink('rgba(250,225,170,.28)','rgba(151,108,53,.3)');c.lineWidth=.8;c.beginPath();c.arc(x,y,r*1.45,0,TAU);c.stroke();}}
 c.globalAlpha=1;
 for(let i=0;i<4;i++){c.beginPath();c.moveTo(-100,815+i*28);c.bezierCurveTo(300,680+i*28,1000,970-i*12,1700,715+i*30);c.strokeStyle=ink('rgba(235,199,156,.07)','rgba(167,123,73,.13)');c.lineWidth=1.5;c.stroke();}
}
function bird(x,y,size,bank){c.save();c.translate(x,y);c.rotate(bank);c.scale(size,size);fillPath('M -42 -14 L -5 1 L 2 -2 L 38 -22 L 12 7 L 3 8 L -2 20 L -7 10 Z',ink('#d4c7b0','#6e9aab'));fillPath('M -42 -14 L -5 1 L -7 10 Z',ink('#eee1c7','#f1c895'));fillPath('M -5 1 L 2 -2 L 12 7 L 3 8 L -2 20 Z',ink('#a39ba0','#426c84'));c.restore();}
function flight(u){
 const p=u*TAU;background(ink('#212a48','#edf4fc'),ink('#6a708b','#b9d8e7'));glow(1090,740,500,320,ink('rgba(224,165,131,.20)','rgba(255,232,191,.42)'));
 for(let i=0;i<9;i++){c.beginPath();c.moveTo(-120,780+i*15);c.bezierCurveTo(470,660+i*16+6*Math.sin(p),960,885+i*11,1740,650+i*20);c.strokeStyle=ink('rgba(213,205,207,.075)','rgba(255,255,255,.34)');c.lineWidth=2; c.stroke();}
 const positions=[[310,737,.62,0],[670,675,.91,.9],[1080,737,.72,2.1],[1330,611,.53,3.2],[870,842,.38,4.4]];
 for(const [x,y,z,phase] of positions){const q=p+phase;bird(x+25*Math.sin(q),y+7*Math.cos(q),z,.06*Math.cos(q));}
}
function petal(x,y,r,angle,squash,color){c.save();c.translate(x,y);c.rotate(angle);c.scale(r*squash,r);fillPath('M 0 -1 C .95 -.75 1.1 .15 0 1 C -.5 .4 -.95 -.4 0 -1 Z',color);fillPath('M 0 -.8 Q .2 -.1 0 .65',null,'rgba(255,227,225,.28)',.045);c.restore();}
function petals(u){
 background(ink('#352b42','#fff2ef'),ink('#805562','#eed1dc'));glow(470,800,530,340,ink('rgba(229,158,142,.18)','rgba(255,248,219,.4)'));glow(1390,760,410,300,ink('rgba(173,155,190,.15)','rgba(206,200,236,.28)'));
 const colors=daylight?['#bc788c','#b580ae','#d49885','#9d93bc']:['#d8a6a4','#bd8d9b','#dfb6a6','#9f91af'];
 for(const [i,p] of clouds[4].slice(0,39).entries()){const phase=TAU*u+p.a*TAU,x=p.x*1720-60+40*Math.sin(phase),y=555+p.y*340+18*Math.cos(phase);c.globalAlpha=.25+p.d*.48;petal(x,y,7+p.b*9,p.a*TAU+.35*Math.sin(phase),.8+.2*Math.cos(phase+p.d*TAU),colors[i%4]);}c.globalAlpha=1;
 for(const p of clouds[4].slice(40,60)){dot(p.x*1600,650+p.y*250,.7+p.b,'rgba(234,192,181,.12)');}
}
function ribbons(u){
 const p=u*TAU;background(ink('#172941','#eff8f4'),ink('#244d60','#c6e1dd'));glow(1150,790,500,330,ink('rgba(86,174,160,.20)','rgba(255,250,223,.3)'));glow(350,780,400,270,ink('rgba(167,158,186,.14)','rgba(225,216,240,.3)'));
 const bands=[[ink('#79aaa5','#82b7b0'),560,35,0],[ink('#8299b5','#91b0d2'),650,38,1.1],[ink('#c1ad88','#dcbf7a'),740,34,2.2],[ink('#63988d','#9cc8b7'),830,42,3.3]];
 for(const [color,base,amp,phase] of bands){
  const y=x=>base+amp*Math.sin(x/310+p+phase)+16*Math.sin(x/590-p+phase);
  c.beginPath();for(let x=-50;x<=1650;x+=8){if(x===-50)c.moveTo(x,y(x));else c.lineTo(x,y(x));}for(let x=1650;x>=-50;x-=8)c.lineTo(x,y(x)+42+14*Math.sin(x/420+p+phase));c.closePath();
  const g=c.createLinearGradient(0,base-40,0,base+85);g.addColorStop(0,color);g.addColorStop(1,ink('rgba(31,57,76,.05)','rgba(220,235,234,.05)'));c.globalAlpha=.68;c.fillStyle=g;c.fill();c.globalAlpha=1;
  for(let k=0;k<4;k++){c.beginPath();for(let x=-50;x<=1650;x+=8){const yy=y(x)+k*7;if(x===-50)c.moveTo(x,yy);else c.lineTo(x,yy);}c.strokeStyle=color;c.globalAlpha=.16-k*.028;c.lineWidth=1.1;c.stroke();}c.globalAlpha=1;
 }
}
function cloudDrift(u){
 background(ink('#192c4c','#eaf4fc'),ink('#617d96','#b7d8e7'));
 for(let k=0;k<5;k++){const x=100+k*350+45*Math.sin(TAU*u+k),y=655+(k%3)*85;glow(x,y,350,95,ink('rgba(195,211,231,.18)','rgba(255,255,255,.8)'));glow(x+80,y-20,210,90,ink('rgba(195,211,231,.13)','rgba(255,255,255,.55)'));}
}
function waterRipples(u){
 background(ink('#143441','#eaf5f2'),ink('#285d68','#a3cdc9'));
 for(const p of clouds[7].slice(0,16)){const q=fract(u+p.y);c.globalAlpha=.35*Math.sin(Math.PI*q)**2;c.strokeStyle=ink('#b3ddd7','#467f87');c.lineWidth=2.2;c.beginPath();c.ellipse(p.x*1600,555+p.a*340,18+q*155,4+q*28,0,0,TAU);c.stroke();}c.globalAlpha=1;
}
function lanterns(u){
 background(ink('#312438','#fff5e7'),ink('#704441','#ead0ad'));
 for(const p of clouds[8].slice(0,17)){const q=fract(u+p.y),x=p.x*1640-20+15*Math.sin(TAU*u+p.a*TAU),y=990-q*460,z=10+p.b*18;c.globalAlpha=fade(q)*(.4+p.d*.4);glow(x,y,z*3,z*3,ink('rgba(255,189,92,.3)','rgba(255,231,178,.6)'));c.fillStyle=ink('#ddab70','#bf9667');c.beginPath();c.roundRect(x-z/2,y-z*.6,z,z*1.1,z*.15);c.fill();c.fillStyle=ink('#f9d99c','#fff3c6');c.fillRect(x-z*.3,y-z*.47,z*.6,z*.75);}c.globalAlpha=1;
}
function confetti(u){
 background(ink('#28334c','#f3f4fa'),ink('#535c79','#d3dfea'));
 const colors=daylight?['#b38898','#65988e','#b59a5c','#7e94b8']:['#e0abc0','#94cbbc','#dbc087','#abbfe5'];
 for(const [i,p] of clouds[9].slice(0,65).entries()){const q=fract(u+p.y),x=p.x*1650-25+24*Math.sin(TAU*u+p.a*TAU),y=470+q*500;c.globalAlpha=fade(q)*(.45+p.d*.35);c.save();c.translate(x,y);c.rotate(p.a*TAU+.4*Math.sin(TAU*u));c.fillStyle=colors[i%4];c.fillRect(-3,-5,4+p.b*5,7+p.d*6);c.restore();}c.globalAlpha=1;
}
const painters=[snow,rain,lights,flight,petals,ribbons,cloudDrift,waterRipples,lanterns,confetti];
function draw(index,t){const u=((t%DURATION+DURATION)%DURATION)/DURATION;c.setTransform(1,0,0,1,0,0);c.clearRect(0,0,W,H);c.save();c.scale(W/1600,H/900);painters[index](u);c.restore();}
const hash=()=>crypto.createHash('sha256').update(c.getImageData(0,0,W,H).data).digest('hex');
async function encode(dest,index){const ff=spawn('ffmpeg',['-hide_banner','-loglevel','error','-y','-f','rawvideo','-pixel_format','rgba','-video_size',`${W}x${H}`,'-framerate',String(FPS),'-i','pipe:0','-an','-c:v','libx264','-preset','medium','-qp','16','-x264-params','ipratio=1:pbratio=1:scenecut=0','-g','150','-keyint_min','150','-pix_fmt','yuv420p','-profile:v','high','-level:v','4.0','-movflags','+faststart','-frames:v',String(FRAMES),dest],{windowsHide:true,stdio:['pipe','ignore','pipe']});let err='';ff.stderr.on('data',d=>err+=d);const done=once(ff,'close');for(let f=0;f<FRAMES;f++){draw(index,f/FPS);if(!ff.stdin.write(Buffer.from(c.getImageData(0,0,W,H).data.buffer)))await once(ff.stdin,'drain');}ff.stdin.end();const [code]=await done;if(code)throw Error(err);}
async function main(){
 fs.mkdirSync(OUT,{recursive:true});
 const catalogPath=path.join(OUT,'catalog.json');
 const catalog=fs.existsSync(catalogPath)?JSON.parse(fs.readFileSync(catalogPath,'utf8')):{schema_version:1,created:'2026-10-21',collection:'generic-moods',song_mapping:null,width:1920,height:1080,fps:FPS,duration_seconds:DURATION,frames:FRAMES,audio:false,provenance:'Original procedural Canvas graphics; no external images, music, model or image-search API.',safe_text_region:{x:.08,y:.06,width:.84,height:.40},moods:items};
 const checks=[],stills=process.argv.includes('--stills'),suffix=daylight?'-daylight':'';
 for(let i=0;i<items.length;i++){
  const s=items[i],dir=path.join(OUT,s.id);fs.mkdirSync(dir,{recursive:true});
  draw(i,0);fs.writeFileSync(path.join(dir,'graphic'+suffix+'.png'),canvas.toBuffer('image/png'));
  const a=hash();draw(i,DURATION);const b=hash();if(a!==b)throw Error('Source loop does not match: '+s.id);
  draw(i,2.5);const mid=hash();if(mid===a)throw Error('Missing motion: '+s.id);
  checks.push({id:s.id,phase0_sha256:a,phase5_sha256:b,phase2_5_sha256:mid});
  const entry=catalog.moods.find(x=>x.id===s.id),files={still:s.id+'/graphic'+suffix+'.png',loop:s.id+'/loop'+suffix+'.mp4'};
  if(daylight){entry.variants??={};entry.variants.daylight={palette:daylightPalettes[i]??'柔和日光',files};}else entry.files=files;
  if(!stills)await encode(path.join(dir,'loop'+suffix+'.mp4'),i);
  console.log(s.id+suffix+' complete');
 }
 catalog.schema_version=2;
 if(library){catalog.width=W;catalog.height=H;}
 catalog.lighting_modes??={night:{title:'夜色',width:W,height:H,lyric_color:'#ffffff'}};
 if(daylight)catalog.lighting_modes.daylight={title:'日光',width:W,height:H,lyric_color:'#22374b'};
 fs.writeFileSync(catalogPath,JSON.stringify(catalog,null,2));
 fs.writeFileSync(path.join(OUT,'render-validation'+suffix+'.json'),JSON.stringify({mode:daylight?'daylight':'night',width:W,height:H,source_loop_matches:true,rendered_videos:!stills,checks},null,2));
}
main().catch(e=>{console.error(e);process.exitCode=1});
