'use strict';
const fs=require('fs'),path=require('path'),crypto=require('crypto'),sharp=require('sharp');
(async()=>{
 const [source,destination,id]=process.argv.slice(2);
 if(!source||!destination||!id)throw Error('source, destination and asset id are required');
 const root=path.resolve(__dirname,'..'),dest=path.resolve(root,destination);
 if(!dest.startsWith(path.join(root,'assets','media')+path.sep))throw Error('Destination outside the media library');
 fs.mkdirSync(path.dirname(dest),{recursive:true});
 const temporary=dest+'.partial.jpg';
 await sharp(source).resize(1280,720,{fit:'cover'}).jpeg({quality:92,mozjpeg:true}).toFile(temporary);
 fs.renameSync(temporary,dest);
 const bytes=fs.readFileSync(dest),meta=await sharp(bytes).metadata();
 const record={id,source:'OpenAI built-in image generation',original_file:source,file:destination.replaceAll('\\','/'),width:meta.width,height:meta.height,sha256:crypto.createHash('sha256').update(bytes).digest('hex')};
 const folder=path.join(root,'assets','media','provenance');fs.mkdirSync(folder,{recursive:true});
 fs.writeFileSync(path.join(folder,id+'.json'),JSON.stringify(record,null,2));
 console.log(JSON.stringify({id,width:meta.width,height:meta.height,bytes:bytes.length}));
})().catch(e=>{console.error(e);process.exitCode=1;});
