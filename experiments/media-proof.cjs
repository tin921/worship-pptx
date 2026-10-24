// Contact sheets for reviewing generated assets; never captures the application.
const fs=require('fs'),path=require('path'),sharp=require('sharp');
const root=path.resolve(__dirname,'..');
const jobs=JSON.parse(fs.readFileSync(path.join(root,'assets/media/generation-prompts.json'),'utf8'));
(async()=>{
 for(let start=0;start<jobs.length;start+=36){
  const batch=jobs.slice(start,start+36);if(batch.some(j=>!fs.existsSync(path.join(root,j.destination))))continue;
  const tiles=[];
  for(let i=0;i<batch.length;i++){
   const j=batch[i],x=(i%6)*240,y=Math.floor(i/6)*158;
   tiles.push({input:await sharp(path.join(root,j.destination)).resize(240,135).toBuffer(),left:x,top:y});
   const label=Buffer.from(`<svg width="240" height="23"><rect width="240" height="23" fill="#fafaf7"/><text x="7" y="16" fill="#233a30" font-family="Arial" font-size="11">${j.id}</text></svg>`);
   tiles.push({input:label,left:x,top:y+135});
  }
  const dest=path.join(root,`artifacts/media-proof-${String(start/36+1).padStart(2,'0')}.jpg`);
  await sharp({create:{width:1440,height:Math.ceil(batch.length/6)*158,channels:3,background:'#fafaf7'}}).composite(tiles).jpeg({quality:92}).toFile(dest);
  console.log(dest);
 }
})().catch(e=>{console.error(e);process.exitCode=1});
