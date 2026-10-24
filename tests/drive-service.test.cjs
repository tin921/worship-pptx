// Run with node tests/drive-service.test.cjs. No Google requests are made.
const fs=require('node:fs');
const vm=require('node:vm');
const assert=require('node:assert/strict');
const crypto=require('node:crypto');
const source=fs.readFileSync('integrations/google-drive/Code.gs','utf8');
function service(existing=[]) {
 const key='test-upload-key-0123456789-0123456789';
 const props={FOLDER_ID:'folder123',UPLOAD_KEY:key};
 const calls=[]; let committed=false, bytes=Buffer.alloc(0), expected, lostReply=false,failRetention=false,metadata={};
 const response=(code,value,headers={})=>({getResponseCode:()=>code,getContentText:()=>JSON.stringify(value),getAllHeaders:()=>headers});
 const context={console,Date,JSON,Number,Object,String,Error,
  ContentService:{MimeType:{JSON:'json'},createTextOutput:text=>({setMimeType:()=>JSON.parse(text)})},
  PropertiesService:{getScriptProperties:()=>({getProperty:n=>props[n],setProperty:(n,v)=>{props[n]=v;},deleteProperty:n=>{delete props[n];},getProperties:()=>({...props})})},
  LockService:{getScriptLock:()=>({tryLock:()=>true,hasLock:()=>true,releaseLock:()=>{}})},
  ScriptApp:{getOAuthToken:()=> 'test-oauth-secret'},
  Utilities:{base64Decode:text=>Array.from(Buffer.from(text,'base64'))},
  UrlFetchApp:{fetch:(url,options)=>{
   calls.push({url,options});
   if(url.includes('/files/folder123?')) return response(200,{id:'folder123',mimeType:'application/vnd.google-apps.folder',capabilities:{canAddChildren:true}});
   if(url.includes('/files?q=')) return response(200,{files:existing});
   if(url.includes('uploadType=resumable')) return response(200,{}, {Location:'https://www.googleapis.com/upload/drive/session/test'});
   if(url.includes('/files/file123?') && options.method==='patch') {
    if(failRetention) {failRetention=false;return response(503,{error:'Temporary metadata error'});}
    metadata=JSON.parse(options.payload);return response(200,{id:'file123',...metadata});
   }
   if(url.includes('/session/')) {
    if(options.headers['Content-Range'].startsWith('bytes */')) return committed ? response(200,expected) : response(308,{}, bytes.length ? {Range:'bytes=0-'+(bytes.length-1)} : {});
    const chunk=Buffer.from(options.payload); bytes=Buffer.concat([bytes,chunk]);
    if(bytes.length===Number(options.headers['Content-Range'].split('/')[1])) {
     committed=true; expected={id:'file123',size:bytes.length,md5Checksum:crypto.createHash('md5').update(bytes).digest('hex')};
     if(lostReply) {lostReply=false;throw new Error('Simulated lost network reply');}
     return response(200,expected);
    }
    return response(308,{}, {Range:'bytes=0-'+(bytes.length-1)});
   }
   throw new Error('Unexpected request '+url);
  }}
 };
 vm.createContext(context); vm.runInContext(source,context);
 return {key,calls,props,context,get metadata(){return metadata;},get bytes(){return bytes;},loseReply:()=>{lostReply=true;},failRetention:()=>{failRetention=true;},post:body=>context.doPost({postData:{contents:JSON.stringify({key,sessionId:'a'.repeat(32),...body})}})};
}
const data=Buffer.from('PK\x03\x04test-powerpoint-package');
const start={action:'start',name:'PC-1025c-call+praise.pptx',size:data.length,md5:crypto.createHash('md5').update(data).digest('hex'),replace:true};
let count=0;
function test(name,body){body();console.log('PASS '+name);count++;}
test('Create, verify and repeat a completed request without another Drive write',()=>{
 const s=service(); assert.equal(s.post(start).ok,true);
 const body={action:'chunk',offset:0,data:data.toString('base64')};
 const result=s.post(body);assert.equal(result.complete,true);assert.equal(result.fileId,'file123');assert.equal(result.folderUrl,'https://drive.google.com/drive/folders/folder123');
 assert.deepEqual(s.bytes,data);const requests=s.calls.length;assert.equal(s.post(body).complete,true);assert.equal(s.calls.length,requests);
 assert.equal(s.post(start).complete,true);assert.equal(s.calls.length,requests);
});
test('Replacement updates the existing ID; keeping both creates a new file',()=>{
 for(const replace of [true,false]){
  const s=service([{id:'oldfile',mimeType:'application/vnd.openxmlformats-officedocument.presentationml.presentation'}]);assert.equal(s.post({...start,replace}).ok,true);
  const call=s.calls.find(c=>c.url.includes('uploadType=resumable'));assert.equal(call.options.method,replace?'patch':'post');assert.equal(call.url.includes('/oldfile?'),replace);
 }
});
test('Reject unauthorized writes, unsafe names, ambiguous replacements and wrong file types',()=>{
 const s=service();assert.equal(s.post({...start,key:'wrong'}).ok,false);assert.equal(s.calls.length,0);
 assert.equal(s.post({...start,name:'../unsafe.pptx'}).ok,false);assert.equal(s.calls.length,0);
 for(const language of ['C','M']) {assert.equal(s.post({...start,name:'PC-1025'+language+'-praise.pptx'}).ok,false);assert.equal(s.calls.length,0);}
 const ambiguous=service([{id:'one'},{id:'two'}]);assert.equal(ambiguous.post(start).ok,false);assert(!ambiguous.calls.some(c=>c.url.includes('/upload/')));
 const folder=service([{id:'nestedfolder',mimeType:'application/vnd.google-apps.folder'}]);assert.equal(folder.post(start).ok,false);
});
test('A lost final reply can be retried without another file or content write',()=>{
 const s=service();assert.equal(s.post(start).ok,true);s.loseReply();
 const body={action:'chunk',offset:0,data:data.toString('base64')};assert.equal(s.post(body).ok,false);assert.equal(s.post(body).complete,true);assert.deepEqual(s.bytes,data);
 assert.equal(s.calls.filter(c=>c.url.includes('uploadType=resumable')).length,1);
});
test('Filename leases prevent concurrent replacement and cancellation releases the lease',()=>{
 const s=service();assert.equal(s.post(start).ok,true);
 assert.equal(s.post({...start,sessionId:'b'.repeat(32)}).ok,false);
 assert.equal(s.post({action:'cancel'}).ok,true);
 assert.equal(s.post({...start,sessionId:'b'.repeat(32)}).ok,true);
});
test('Checksum mismatch fails verification and does not claim success',()=>{
 const s=service();assert.equal(s.post({...start,md5:'0'.repeat(32)}).ok,true);
 const reply=s.post({action:'chunk',offset:0,data:data.toString('base64')});assert.equal(reply.ok,false);assert.match(reply.error,/checksum/);assert(reply.stack);assert(!JSON.stringify(reply).includes(s.key));
});
test('Expired sessions and non-PowerPoint content are rejected',()=>{
 const s=service();s.post(start);const key='upload_'+'a'.repeat(32);const state=JSON.parse(s.props[key]);state.expires=0;s.props[key]=JSON.stringify(state);
 assert.equal(s.post({action:'chunk',offset:0,data:data.toString('base64')}).ok,false);
 const bad=service();bad.post(start);assert.equal(bad.post({action:'chunk',offset:0,data:Buffer.alloc(data.length).toString('base64')}).ok,false);
});
test('Completed uploads receive a ten-day file expiry and a retry preserves it',()=>{
 const s=service();s.post(start);const body={action:'chunk',offset:0,data:data.toString('base64')};
 const before=Date.now();const result=s.post(body);const expires=Date.parse(result.expiresAt);
 assert(expires>=before+10*86400000 && expires<=Date.now()+10*86400000);
 assert.equal(s.metadata.appProperties.worshipExpiresAt,result.expiresAt);assert.equal(s.metadata.appProperties.worshipFolder,'folder123');
 assert.equal(s.post(body).expiresAt,result.expiresAt);
 const state=JSON.parse(s.props['upload_'+'a'.repeat(32)]);assert(state.expires<expires);assert.equal(state.retentionSet,true);
});
test('Retention metadata failure retries without re-uploading or falsely claiming success',()=>{
 const s=service();s.post(start);s.failRetention();const body={action:'chunk',offset:0,data:data.toString('base64')};
 assert.equal(s.post(body).ok,false);assert.deepEqual(s.bytes,data);
 assert.equal(JSON.parse(s.props['upload_'+'a'.repeat(32)]).complete,true);
 assert.equal(s.post(body).ok,true);assert.deepEqual(s.bytes,data);
 assert.equal(s.calls.filter(c=>c.url.includes('/session/') && !c.options.headers['Content-Range'].startsWith('bytes */')).length,1);
});
const pptx='application/vnd.openxmlformats-officedocument.presentationml.presentation';
function expiredFile(id,overrides={}) {
 return {id,name:id+'.pptx',mimeType:pptx,parents:['folder123'],trashed:false,capabilities:{canTrash:true},appProperties:{worshipPptx:'worship-pptx-v1',worshipFolder:'folder123',worshipExpiresAt:new Date(Date.now()-1000).toISOString()},...overrides};
}
function cleanupService(pages,current={}) {
 const s=service();s.context.console={log:()=>{},error:()=>{}};
 const response=(value,code=200)=>({getResponseCode:()=>code,getContentText:()=>JSON.stringify(value),getAllHeaders:()=>({})});
 s.context.UrlFetchApp.fetch=(url,options)=> {
  s.calls.push({url,options});const u=new URL(url);
  if(u.searchParams.has('q')) {
   const q=u.searchParams.get('q');assert(q.includes("'folder123' in parents") && q.includes('appProperties has') && q.includes('trashed = false') && q.includes(pptx));
   const page=Number(u.searchParams.get('pageToken')||0);return response({files:pages[page],nextPageToken:page+1<pages.length ? String(page+1) : undefined});
  }
  const id=u.pathname.split('/').pop();
  if(options.method==='patch') {assert.deepEqual(JSON.parse(options.payload),{trashed:true});return response({id,trashed:true});}
  return response(current[id] || pages.flat().find(f=>f.id===id));
 };
 return s;
}
test('Daily cleanup only trashes expired tagged PowerPoints in the configured folder, including later pages',()=>{
 const s=cleanupService([[expiredFile('expired'),expiredFile('unmarked',{appProperties:{}}),expiredFile('other-folder',{parents:['elsewhere']}),expiredFile('folder',{mimeType:'application/vnd.google-apps.folder'}),expiredFile('trashed',{trashed:true})],
 [expiredFile('future',{appProperties:{worshipPptx:'worship-pptx-v1',worshipFolder:'folder123',worshipExpiresAt:new Date(Date.now()+86400000).toISOString()}}),expiredFile('invalid-date',{appProperties:{worshipPptx:'worship-pptx-v1',worshipFolder:'folder123',worshipExpiresAt:'broken'}}),expiredFile('expired2')]]);
 assert.equal(s.context.cleanupExpiredUploads().moved,2);
 assert.equal(s.calls.filter(c=>c.options.method==='patch').length,2);assert(!s.calls.some(c=>c.options.method==='delete'));
});
test('Cleanup rechecks moved/replaced files and skips in-progress replacements',()=>{
 const s=cleanupService([[expiredFile('moved'),expiredFile('replaced'),expiredFile('active')]],{
  moved:expiredFile('moved',{parents:['elsewhere']}),replaced:expiredFile('replaced',{appProperties:{worshipPptx:'worship-pptx-v1',worshipFolder:'folder123',worshipExpiresAt:new Date(Date.now()+10*86400000).toISOString()}})
 });
 s.props.upload_active=JSON.stringify({name:'active.pptx',complete:false,expires:Date.now()+600000});
 assert.equal(s.context.cleanupExpiredUploads().moved,0);assert(!s.calls.some(c=>c.options.method==='patch'));
});
test('Cleanup preview is read-only and permission failures are reported',()=>{
 const preview=cleanupService([[expiredFile('test')]]);assert.equal(preview.context.previewExpiredUploads().moved,0);assert(!preview.calls.some(c=>c.options.method==='patch'));
 const denied=cleanupService([[expiredFile('denied',{capabilities:{canTrash:false}})]]);assert.throws(()=>denied.context.cleanupExpiredUploads(),/could not be cleaned up/);
 assert(!denied.calls.some(c=>c.options.method==='patch'));
});
console.log('RESULT '+count+' passed');
