"""Publish the verified 0.93.38 assets using the configured Git credential helper.

Run only when publication to tin921/worship-pptx is intended. Credentials stay
in memory. Existing assets are checked rather than overwritten.
"""
import hashlib
import json
from pathlib import Path
import subprocess
import requests

ROOT = Path(__file__).resolve().parents[3]
RECORDS = Path(__file__).resolve().parent
DIST = Path('E:/WorshipPptx-work/worship-release-0.93.38/dist')
API = 'https://api.github.com/repos/tin921/worship-pptx'
TAG = 'v0.93.38'


def sha256(path):
    with path.open('rb') as source:
        return hashlib.file_digest(source, 'sha256').hexdigest()


def main():
    validation = json.loads((RECORDS / 'package-validation.json').read_text(encoding='utf-8-sig'))
    assert validation['msi_payload_matches'] and validation['portable_payload_matches']
    head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
    remote = subprocess.check_output(['git', 'ls-remote', 'origin', 'refs/heads/main'], cwd=ROOT, text=True).split()[0]
    assert head == remote, 'Push the reviewed source and documentation before publishing.'
    source_record = json.loads((RECORDS / 'build-source.json').read_text(encoding='utf-8'))
    release_commit = source_record['commit']
    subprocess.run(['git', 'merge-base', '--is-ancestor', release_commit, head], cwd=ROOT, check=True)
    assert subprocess.check_output(['git', 'status', '--porcelain'], cwd=ROOT, text=True).strip() == '', 'Commit the release records before publication.'
    credential = subprocess.run(['git', 'credential', 'fill'], cwd=ROOT,
        input='protocol=https\nhost=github.com\n\n', text=True, capture_output=True, check=True)
    values = dict(line.split('=', 1) for line in credential.stdout.splitlines() if '=' in line)
    session = requests.Session()
    session.headers.update({'Authorization': 'Bearer ' + values['password'],
        'Accept': 'application/vnd.github+json', 'X-GitHub-Api-Version': '2022-11-28'})
    response = session.get(API + '/releases/tags/' + TAG, timeout=30)
    if response.status_code == 404:
        response = session.post(API + '/releases', json={'tag_name': TAG,
            'target_commitish': release_commit, 'name': 'Worship PPTX v0.93.38', 'draft': True,
            'body': (ROOT / 'docs/release-v0.93.38.md').read_text(encoding='utf-8')}, timeout=30)
    response.raise_for_status()
    release = response.json()
    assert release['target_commitish'] == release_commit, 'Existing release points to another build.'
    assets = {a['name']: a for a in release['assets']}
    paths = [DIST / 'WorshipPptx-0.93.38-Setup.msi', DIST / 'WorshipPptx-0.93.38-Portable.zip',
        DIST / 'SHA256SUMS.txt']
    upload = release['upload_url'].split('{', 1)[0]
    assert upload.startswith('https://uploads.github.com/repos/tin921/worship-pptx/')
    records = []
    for path in paths:
        digest = sha256(path)
        asset = assets.get(path.name)
        if asset is None:
            print('Uploading ' + path.name, flush=True)
            with path.open('rb') as source:
                result = session.post(upload, params={'name': path.name}, data=source,
                    headers={'Content-Type': 'application/octet-stream'}, timeout=(30, 600))
            result.raise_for_status()
            asset = result.json()
        assert asset['size'] == path.stat().st_size
        assert asset.get('digest') == 'sha256:' + digest
        records.append({'name': path.name, 'bytes': asset['size'], 'sha256': digest,
            'url': asset['browser_download_url']})
    if release['draft']:
        result = session.patch(API + '/releases/' + str(release['id']),
            json={'draft': False, 'make_latest': 'true'}, timeout=30)
        result.raise_for_status()
        # Publishing replaces draft-only untagged download URLs with tag URLs.
        release = result.json()
        final_assets = {a['name']: a for a in release['assets']}
        for asset in records:
            asset['url'] = final_assets[asset['name']]['browser_download_url']
    for asset in records:
        # Verify anonymous availability without sending the GitHub credential.
        result = requests.head(asset['url'], allow_redirects=True, timeout=60)
        result.raise_for_status()
        asset['http_status'] = result.status_code
        assert int(result.headers['Content-Length']) == asset['bytes']
    latest = requests.get(API + '/releases/latest', timeout=30)
    latest.raise_for_status()
    assert latest.json()['tag_name'] == TAG
    (RECORDS / 'publication.json').write_text(json.dumps({'tag': TAG,
        'release_commit': release['target_commitish'], 'assets': records, 'anonymous_downloads_verified': True,
        'latest': True, 'release_url': release['html_url']}, indent=2) + '\n', encoding='utf-8')
    print('Published and verified ' + release['html_url'], flush=True)


if __name__ == '__main__':
    main()
