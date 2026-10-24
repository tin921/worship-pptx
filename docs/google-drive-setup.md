# Google Drive upload — one-time administrator setup

Users **do not sign in to Google**. The app sends PowerPoint files to a small Apps Script web service, which writes to one fixed folder as the church account. A public folder link lets people view/download files; it is not an anonymous upload API. Only the administrator signs in during setup.

The repository includes an example configuration. Each PC needs its own local connection settings before uploading. Use a dedicated church account and a dedicated folder.

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

## Ten-day retention and local copies

The desktop app saves a complete local copy before uploading. Choose its default folder in **Settings → PowerPoint save folder**, or edit it in the upload window; the change is remembered. The default is `Documents/WorshipPptx`. **Make PowerPoint** also starts its save dialog there. When replacement is unchecked, local duplicates receive a numbered suffix. A network failure or cloud cleanup never deletes the local copy.

Version 2 of `Code.gs` assigns every successfully uploaded/replaced Drive file a **10-day** deadline in private file metadata. Replacement restarts the period; retrying the same completed upload does not. The upload log and success message include the deadline. New uploads and replacements are covered; previously untagged files are left alone.

After updating `Code.gs`, use **Deploy → Manage deployments → Edit → New version → Deploy** to retain the existing web-app URL. In **Triggers → Add Trigger**, choose **cleanupExpiredUploads**, **Head**, **Time-driven**, **Day timer**, and a daily time window. Create only one such trigger. It runs under the owner's account without requiring a desktop PC to be on. This does not require adding OAuth scopes to the existing manifest.

Run **previewExpiredUploads** from the editor to see eligible files without moving anything. The daily **cleanupExpiredUploads** job only moves expired, app-tagged `.pptx` files still in the configured folder to **Drive Trash**. It rechecks each file and skips active replacement uploads. Errors appear in the execution log and the trigger's configured failure notifications. An expired file is removed on the next daily run, rather than at the exact deadline minute. Google normally permanently deletes items after another 30 days in Trash; downloaded/local copies are unaffected. See [Drive Trash behavior](https://developers.google.com/workspace/drive/api/guides/delete) and [time-driven triggers](https://developers.google.com/apps-script/guides/triggers/installable).

The temporary `upload_…` Script Properties still expire after 24 hours for completed uploads. They are unrelated to the ten-day Drive file deadline.

## Google verification and authorization expiry

The **Google hasn't verified this app** warning is separate from OAuth publishing status. It does not impose a one-month lifetime on an Apps Script deployment. Google permits limited personal-use apps to remain unverified, and Workspace-internal apps have a separate exception; see [verification exceptions](https://support.google.com/cloud/answer/13464323?hl=en).

If the linked Google Cloud OAuth project is **External / Testing**, test-user authorizations expire after **seven days**. For ongoing use, check **Google Auth Platform → Audience → Publishing status** in that project's console and use **In production** when appropriate. Publishing and verification are separate; the warning can remain for an unverified production app. The deployment screenshot alone does not reveal this status. See [Google's publishing-status rules](https://support.google.com/cloud/answer/15549945?hl=en).

With this service deployed as **Execute as me / Anyone**, the owner authorizes Google Drive access. Desktop users do not grant Google OAuth access. Re-run `checkSetup` as the owner if authorization is revoked or needs renewal.

The current deployment uses an Apps Script **Default** Cloud project. Google manages that project, and the usual OAuth Audience page is not accessible. The unverified warning does not establish Testing status or a seven-day expiry; a project switch is not needed for this uploader. See [default Cloud projects](https://developers.google.com/apps-script/guides/cloud-platform-projects).

## Reference links

- [Apps Script web apps and execution identity](https://developers.google.com/apps-script/guides/web)
- [Enable advanced Google services](https://developers.google.com/apps-script/guides/services/advanced)
- [Content Service responses and redirects](https://developers.google.com/apps-script/guides/content)
- [Drive resumable uploads](https://developers.google.com/workspace/drive/api/guides/manage-uploads)

Local tests cover the upload protocol, local-copy ordering, failure recovery, and scoped retention cleanup with fake HTTP/Drive responses. The live administrator deployment was updated to version 2 on 2026-10-23: an original one-slide test deck uploaded successfully, replacement preserved its file ID and renewed the ten-day deadline, and the service verified size and MD5 for both transfers. An intentionally expired synthetic test file was then moved to recoverable Trash by the cleanup function, with its local copy retained. One daily trigger runs between midnight and 1am in the project's America/New_York time zone. The private connection settings remain outside the repository. A new installation or deployment still needs its own live check.
