// The app's media library contains still images only. No video encoding is needed.
const fs=require('fs'),path=require('path');
const media=path.resolve(__dirname,'../assets/media');
const file=path.join(media,'catalog.json');
const catalog=JSON.parse(fs.readFileSync(file,'utf8'));
const allowed=['id','title','theme','style','lighting','core','still'];
const clean=catalog.map(item=>{
 const asset=Object.fromEntries(Object.entries(item).filter(([key])=>allowed.includes(key)));
 const still=path.resolve(media,asset.still);
 if(!still.startsWith(media+path.sep)||!fs.existsSync(still)) throw Error('Missing or invalid still: '+asset.id);
 asset.title=asset.title.replace('動態插畫','插畫');
 return asset;
});
fs.writeFileSync(file,JSON.stringify(clean,null,2));
console.log(`${clean.length} static backgrounds ready`);
