// Deploy as the church account, access: Anyone. Users do not sign in.
// Set FOLDER_ID and UPLOAD_KEY in Script Properties, never in this source file.
const PPTX = 'application/vnd.openxmlformats-officedocument.presentationml.presentation';
const CHUNK = 2 * 1024 * 1024;
const MAX_SIZE = 100 * 1024 * 1024;
const API = 'https://www.googleapis.com/drive/v3/files';

function doGet() {
  return json_({ok:true, service:'Worship PPTX upload', version:1});
}

function doPost(e) {
  const logs = [];
  const log = text => logs.push('[' + new Date().toISOString() + '] ' + text);
  const props = PropertiesService.getScriptProperties();
  let lock;
  try {
    const raw = e && e.postData && e.postData.contents;
    if (!raw || raw.length > 3 * 1024 * 1024) throw new Error('Invalid request size.');
    const request = JSON.parse(raw);
    const key = props.getProperty('UPLOAD_KEY');
    const folder = props.getProperty('FOLDER_ID');
    if (!key || key.length < 32 || !/^[A-Za-z0-9_-]+$/.test(folder || '')) throw new Error('Administrator setup is incomplete.');
    if (typeof request.key !== 'string' || !same_(request.key,key)) throw new Error('Upload key is invalid. Ask the administrator to update local configuration.');
    if (!/^[a-f0-9]{32}$/.test(request.sessionId || '')) throw new Error('Invalid upload session.');
    lock = LockService.getScriptLock();
    if (!lock.tryLock(25000)) throw new Error('Another upload is busy. Please retry.');
    prune_(props);
    let state = JSON.parse(props.getProperty('upload_' + request.sessionId) || 'null');
    if (request.action === 'cancel') {
      if (state && !state.complete) props.deleteProperty('upload_' + request.sessionId);
      log('Pending upload released. A completed file, if any, is retained.');
      return json_({ok:true,logs});
    }
    if (request.action === 'start') {
      if (!/^[A-Za-z0-9]{1,64}-[0-9]{4}[MC]-(call\+)?praise\.pptx$/.test(request.name || '')) throw new Error('Invalid PowerPoint filename.');
      if (!Number.isSafeInteger(request.size) || request.size < 1 || request.size > MAX_SIZE || !/^[a-f0-9]{32}$/.test(request.md5 || '') || typeof request.replace !== 'boolean') throw new Error('Invalid upload size, checksum or replacement option.');
      if (state) {
        if (state.name !== request.name || state.size !== request.size || state.md5 !== request.md5 || state.replace !== request.replace) throw new Error('Session does not match this file.');
        return json_(reply_(state,folder,logs));
      }
      const all = props.getProperties();
      for (const name of Object.keys(all).filter(x => x.startsWith('upload_'))) {
        const active = JSON.parse(all[name]);
        if (!active.complete && active.name === request.name) throw new Error('This filename is already being uploaded. Cancel the other upload or wait 15 minutes before retrying.');
      }
      if (Object.keys(all).filter(x => x.startsWith('upload_')).length >= 80) throw new Error('Upload service is busy. Please retry later.');
      const info = driveJson_(API + '/' + encodeURIComponent(folder) + '?fields=id,mimeType,capabilities(canAddChildren)&supportsAllDrives=true');
      if (info.mimeType !== 'application/vnd.google-apps.folder' || !info.capabilities || !info.capabilities.canAddChildren) throw new Error('The service owner cannot add files to this folder.');
      log('Destination folder is writable.');
      const q = "'" + folder + "' in parents and name = '" + request.name + "' and trashed = false";
      const found = driveJson_(API + '?q=' + encodeURIComponent(q) + '&fields=files(id,mimeType),nextPageToken&pageSize=100&supportsAllDrives=true&includeItemsFromAllDrives=true');
      const files = found.files || [];
      if (request.replace && (files.length > 1 || found.nextPageToken)) throw new Error('Several files have this name. Ask the administrator to remove duplicates before replacement.');
      if (request.replace && files.length && files[0].mimeType !== PPTX) throw new Error('The existing item is not a PowerPoint file. It will not be replaced.');
      const existing = request.replace && files.length ? files[0].id : null;
      log(existing ? 'Replacing the existing file; its Drive link is preserved.' : 'Creating a new PowerPoint file.');
      const endpoint = 'https://www.googleapis.com/upload/drive/v3/files' + (existing ? '/' + encodeURIComponent(existing) : '') + '?uploadType=resumable&supportsAllDrives=true&fields=id,size,md5Checksum';
      const meta = {name:request.name,mimeType:PPTX};
      if (!existing) meta.parents = [folder];
      const response = fetch_(endpoint,{method:existing ? 'patch' : 'post',contentType:'application/json',headers:{'X-Upload-Content-Type':PPTX,'X-Upload-Content-Length':String(request.size)},payload:JSON.stringify(meta)});
      requireOk_(response);
      const url = header_(response,'Location');
      if (!/^https:\/\/www\.googleapis\.com\/upload\/drive\//.test(url || '')) throw new Error('Drive did not issue a valid resumable upload session.');
      state = {name:request.name,size:request.size,md5:request.md5,replace:request.replace,url,offset:0,complete:false,expires:Date.now()+15*60*1000};
      props.setProperty('upload_' + request.sessionId,JSON.stringify(state));
      return json_(reply_(state,folder,logs));
    }
    if (request.action !== 'chunk' || !state) throw new Error('Upload session expired. Start the upload again.');
    if (state.complete) return json_(reply_(state,folder,logs));
    if (!Number.isSafeInteger(request.offset) || request.offset < 0 || request.offset % CHUNK !== 0 || typeof request.data !== 'string' || !/^[A-Za-z0-9+/]*={0,2}$/.test(request.data)) throw new Error('Invalid upload chunk.');
    // Probe Drive first: a previous request may have succeeded despite a lost reply.
    let response = fetch_(state.url,{method:'put',headers:{'Content-Range':'bytes */' + state.size},payload:[]});
    update_(state,response,log);
    if (!state.complete && state.offset === request.offset) {
      const bytes = Utilities.base64Decode(request.data);
      const required = Math.min(CHUNK,state.size-state.offset);
      if (bytes.length !== required) throw new Error('Chunk size does not match the file.');
      if (state.offset === 0 && (bytes[0] !== 80 || bytes[1] !== 75)) throw new Error('Only PowerPoint ZIP packages are accepted.');
      response = fetch_(state.url,{method:'put',contentType:PPTX,headers:{'Content-Range':'bytes ' + state.offset + '-' + (state.offset+bytes.length-1) + '/' + state.size},payload:bytes});
      update_(state,response,log);
    } else if (!state.complete && state.offset < request.offset) throw new Error('An earlier chunk is missing. Restart the upload.');
    state.expires = Date.now() + (state.complete ? 24*60*60*1000 : 15*60*1000);
    props.setProperty('upload_' + request.sessionId,JSON.stringify(state));
    return json_(reply_(state,folder,logs));
  } catch(error) {
    const redact = value => String(value || '').split(props.getProperty('UPLOAD_KEY') || '\u0000').join('[redacted]').replace(/https:\/\/www\.googleapis\.com\/upload\/[^\s"']+/g,'[Drive upload session]');
    log('Upload failed.');
    return json_({ok:false,logs,error:redact(error.message),stack:redact(error.stack)});
  } finally { if (lock && lock.hasLock()) lock.releaseLock(); }
}

function update_(state,response,log) {
  const code=response.getResponseCode();
  if (code === 308) {
    const range=header_(response,'Range');
    state.offset=range ? Number(range.match(/bytes=0-(\d+)/)[1])+1 : 0;
    if (!Number.isSafeInteger(state.offset) || state.offset>state.size) throw new Error('Invalid Drive upload position.');
    return;
  }
  requireOk_(response);
  const result=JSON.parse(response.getContentText());
  if (!result.id || Number(result.size)!==state.size || result.md5Checksum!==state.md5) throw new Error('Drive file size or checksum verification failed. Check the folder before retrying.');
  state.fileId=result.id; state.complete=true; state.offset=state.size; delete state.url;
  log('Drive confirmed file size and MD5 checksum. Upload complete.');
}
function reply_(state,folder,logs) { return {ok:true,complete:state.complete,offset:state.offset,fileId:state.fileId || '',folderUrl:'https://drive.google.com/drive/folders/'+folder,logs}; }
function fetch_(url,options) {
  options=options || {}; options.muteHttpExceptions=true; options.followRedirects=false;
  options.headers=Object.assign({},options.headers || {},{Authorization:'Bearer '+ScriptApp.getOAuthToken()});
  return UrlFetchApp.fetch(url,options);
}
function requireOk_(response) { const code=response.getResponseCode(); if(code<200 || code>=300) throw new Error('Drive HTTP '+code+': '+response.getContentText().substring(0,2000)); }
function driveJson_(url) {const response=fetch_(url);requireOk_(response);return JSON.parse(response.getContentText());}
function header_(response,name) {const headers=response.getAllHeaders();const key=Object.keys(headers).find(k=>k.toLowerCase()===name.toLowerCase());return key ? String(headers[key]) : '';}
function same_(a,b) {let diff=a.length^b.length;for(let i=0;i<b.length;i++) diff|=(a.charCodeAt(i)||0)^b.charCodeAt(i);return diff===0;}
function prune_(props) {const all=props.getProperties();for(const name of Object.keys(all).filter(x=>x.startsWith('upload_'))) {if(JSON.parse(all[name]).expires<Date.now()) props.deleteProperty(name);}}
function json_(value) {return ContentService.createTextOutput(JSON.stringify(value)).setMimeType(ContentService.MimeType.JSON);}

// Run once from the Apps Script editor to authorize the owner's account and check the folder.
function checkSetup() {
  const props=PropertiesService.getScriptProperties();
  const id=props.getProperty('FOLDER_ID');
  if(!/^[A-Za-z0-9_-]+$/.test(id || '') || (props.getProperty('UPLOAD_KEY') || '').length<32) throw new Error('Set FOLDER_ID and a random UPLOAD_KEY (32+ characters) first.');
  const folder=driveJson_(API+'/'+id+'?fields=name,mimeType,capabilities(canAddChildren)&supportsAllDrives=true');
  if(folder.mimeType!=='application/vnd.google-apps.folder' || !folder.capabilities.canAddChildren) throw new Error('Folder is not writable.');
  console.log('Ready: '+folder.name+'. This check did not upload a file.');
}
