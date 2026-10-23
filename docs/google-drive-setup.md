# Google Drive upload — one-time administrator setup

Users **do not sign in to Google**. The app sends PowerPoint files to a small Apps Script web service, which writes to one fixed folder as the church account. A public folder link lets people view/download files; it is not an anonymous upload API. Only the administrator signs in during setup.

The app currently has placeholders. Nothing is uploaded until the service is deployed and the local configuration is filled in. Use a dedicated church account and a dedicated folder.

## 1. Prepare the folder

1. Sign in to the church Google account and create a folder, for example **Sunday Worship PowerPoints**.
2. Choose **Share → General access → Anyone with the link → Viewer**, if everyone holding the link should be able to check/download files.
3. Copy its folder link. In `https://drive.google.com/drive/folders/ABC123`, the folder ID is `ABC123`. Keep that ID for the next step.

## 2. Create the upload service

1. Open [Google Apps Script](https://script.google.com/) with the same account and create a new project, named **Worship PPTX Upload**.
2. Replace `Code.gs` with [Code.gs](../integrations/google-drive/Code.gs) from this repository.
3. In **Project Settings**, enable showing `appsscript.json`. Replace its contents with [appsscript.json](../integrations/google-drive/appsscript.json).
4. In the editor, **Services → + → Drive API → v3 → Add**. With the default Apps Script cloud project, adding the service enables its API. If using a standard Google Cloud project, also enable **Google Drive API** there.
5. In **Project Settings → Script properties**, add:

   | Property | Value |
   | --- | --- |
   | `FOLDER_ID` | The ID from step 1 |
   | `UPLOAD_KEY` | A randomly generated value of at least 32 characters; use a password manager |

6. Select and run `checkSetup` in the editor. Authorize the church account when Google asks. The execution log should say **Ready**. This check does not create or replace a file.
7. Choose **Deploy → New deployment → Web app**. Set **Execute as: Me** and **Who has access: Anyone**, then deploy. Do not choose “Anyone with Google account.” Copy the deployment URL ending in `/exec`.

If your Workspace administrator disables public web apps, they must allow this deployment or provide another server. Do not make users sign in as a workaround.

## 3. Connect the app

Copy [google-drive.example.json](../integrations/google-drive/google-drive.example.json) to a file named **google-drive.json** inside the app's data folder:

- Windows: `%LOCALAPPDATA%\WorshipPptx\google-drive.json`
- macOS: `~/Library/Application Support/WorshipPptx/google-drive.json`
- If `WORSHIP_PPTX_DATA` is set, use that folder instead.

Fill in `serviceUrl`, `uploadKey` and `folderUrl`. The upload key must match the Script Property. Keep this configuration out of Git. Distribute it privately to the worship PCs; anyone holding it can upload/replace PowerPoints in this folder. Users will not see a sign-in prompt. The Google account's password and OAuth credentials never go into the desktop app.

For this initial setup, send me the **folder URL and deployed `/exec` URL**. Put the upload key in the local configuration yourself; do not paste it into chat. Once ready, I can connect and test a real upload with you.

## 4. Verify before Sunday

1. Create a short test program. Click **Upload Google Drive**, immediately beside **Make PowerPoint**.
2. Check the suggested name, for example `CHURCHPC-1025c-call+praise.pptx`. The date is the next Sunday (today if already Sunday). Simplified text suggests lowercase m; Traditional suggests lowercase c. The filename remains editable because script cannot reliably identify spoken language. **Make PowerPoint** suggests the same name without the PC prefix, for example `1025c-call+praise.pptx`.
3. Upload, then follow the folder link. Open/download the PowerPoint and check it.
4. Upload a changed version with replacement checked. It should update the existing file and preserve its Drive link. Unchecked keeps both copies. Multiple same-name files block replacement until the administrator resolves the ambiguity.
5. Try cancellation and an invalid upload key. The log should explain the result. **Copy all logs** includes timestamps and exception details, with the upload key redacted.

The service accepts `.pptx` files up to 100 MB, in 2 MB chunks, and verifies the completed file's size and MD5 checksum. This is a transfer-integrity check, not a malware scan. Network retries use a session ID and byte offset to avoid repeating completed chunks. A canceled final request may already have completed on Drive; check the folder before retrying.

Uploads use the church account's storage and [Apps Script quotas](https://developers.google.com/apps-script/guides/services/quotas). Deploy a new web app version after changing the server code. Rotate `UPLOAD_KEY` in Script Properties and on each PC if it is shared accidentally.

## Reference

- [Apps Script web apps and execution identity](https://developers.google.com/apps-script/guides/web)
- [Enable advanced Google services](https://developers.google.com/apps-script/guides/services/advanced)
- [Content Service responses and redirects](https://developers.google.com/apps-script/guides/content)
- [Drive resumable uploads](https://developers.google.com/workspace/drive/api/guides/manage-uploads)

Local tests cover the upload protocol with fake HTTP/Drive responses. They do not confirm a live Google deployment; that verification waits for the administrator's setup.
