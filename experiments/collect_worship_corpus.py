"""Collect public Chinese worship lyrics for LOCAL analysis, never for redistribution.

Source HTML and lyrics stay in git-ignored .tools. Exported research contains only
metadata, our short analytical labels, and source links. No credentials required.
"""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor, as_completed
from collections import Counter, defaultdict
from urllib.parse import urljoin, unquote, urlparse
import hashlib
import json
import re
import threading
import time

import requests
from bs4 import BeautifulSoup
from opencc import OpenCC

ROOT = Path(__file__).resolve().parents[1]
CACHE = ROOT / '.tools/worship-research'
HTML = CACHE / 'pages'
HTML.mkdir(parents=True, exist_ok=True)
CC = OpenCC('t2s')
DATE = '2026-10-21'
LOCK = threading.Lock()
NEXT = defaultdict(float)
EVENTS = []


def normal(text):
    return CC.convert(text).replace('祢', '你').replace('祂', '他')


def chinese(text):
    return ''.join(re.findall(r'[\u3400-\u9fff]', normal(text)))


def get(url):
    path = HTML / (hashlib.sha256(url.encode()).hexdigest() + '.html')
    if path.exists():
        return BeautifulSoup(path.read_bytes(), 'html.parser')
    host = urlparse(url).netloc
    with LOCK:
        delay = max(0, NEXT[host] - time.monotonic())
        NEXT[host] = time.monotonic() + delay + .5
    if delay:
        time.sleep(delay)
    for attempt in range(3):
        try:
            r = requests.get(url, timeout=(15, 55), headers={
                'User-Agent': 'WorshipPptxResearch/0.1 (local noncommercial catalogue analysis)'})
            if r.status_code in (401, 403):
                raise RuntimeError(f'Access denied {r.status_code}; not retried')
            if r.status_code == 429:
                time.sleep(min(30, int(r.headers.get('Retry-After', '20'))))
                continue
            r.raise_for_status()
            path.write_bytes(r.content)
            with LOCK:
                EVENTS.append({'url': url, 'resolved_url': r.url, 'status': r.status_code,
                               'bytes': len(r.content), 'sha256': hashlib.sha256(r.content).hexdigest()})
            return BeautifulSoup(r.content, 'html.parser')
        except (requests.Timeout, requests.ConnectionError):
            if attempt == 2:
                raise
            time.sleep(2 * (attempt + 1))
    raise RuntimeError('Rate limited: ' + url)


def seed(name, url):
    old = CACHE / (name + '.html')
    if old.exists():
        return BeautifulSoup(old.read_bytes(), 'html.parser')
    return get(url)


def extract_lines(node):
    # Preserve line boundaries, strip chords without inserting spaces in Chinese.
    for e in node.select('script,style,h1,h2,h3,h4,h5,h6,.chord,.chord-label,.chord-inline,.KCN-chord,.KCNchordWrap,.KCNtranspose,.KCNkeyTitle'):
        e.decompose()
    for br in node.find_all('br'):
        br.replace_with('\n')
    for block in node.find_all(['p', 'h1', 'h2', 'h3', 'h4', 'section']):
        block.append('\n')
    lines = []
    for line in node.get_text('', strip=False).splitlines():
        line = re.sub(r'\s+', ' ', line).strip()
        if '→' in line or re.match(r'^(主歌|副歌|橋段|間奏|尾聲|結尾|尾句|前奏)[\s\d#（(]*$', line):
            continue
        # Chord placement can insert spaces inside words (e.g. 拯 救). Chinese
        # line boundaries remain separate; inline Chinese whitespace is removed.
        line = re.sub(r'(?<=[\u3400-\u9fff])\s+(?=[\u3400-\u9fff])', '', line)
        if len(chinese(line)) >= 2 and not re.match(r'^(主歌|副歌|橋段|間奏|尾聲|Verse|Chorus|Bridge|Pre.chorus)\s*\d*[:：#]*$', line, re.I):
            if not re.match(r'^(詞[曲：:]|曲[：:]|版權|主唱[：:]|編曲[：:]|發行[：:]|請支持|廣東話|普通話|國語|粵語|英文|Tempo)', line):
                lines.append(line)
    return list(dict.fromkeys(lines))


def row(title, lines, source, provider, **extra):
    return dict(title=title.strip(), lines=lines, lyrics='\n'.join(lines),
                source=source, provider=provider, retrieved=DATE, **extra)


def sop():
    # Album pages provide individual track blocks and citation locations.
    index = seed('sop', 'https://sop.org/music/')
    links = sorted(set(a['href'] for a in index.select('a[href]')
                       if re.search(r'/music/(pw\d+[apj]?|cpw\d+|dev\d+|gl\d+|tpw\d+)/?$', a['href'])))
    # The searchable public catalogue includes older albums not on the first index page.
    table = seed('sop_songs', 'https://sop.org/songs/')
    records = []
    album_links = {}
    for a in index.select('a[href]'):
        if '/music/' in a['href'] and a.get_text(strip=True):
            album_links[normal(a.get_text(strip=True))] = a['href']
    for tr in table.select('table tbody tr'):
        cells = tr.find_all('td', recursive=False)
        if len(cells) != 7:
            continue
        title, composer, writer, album, series = [c.get_text(' ', strip=True) for c in cells[:5]]
        source = album_links.get(normal(album), 'https://sop.org/songs/')
        if source.endswith('/songs/'):
            m = re.search(r'敬拜讚美\s*\((\d+)\)', series)
            if m and int(m[1]) <= 10:
                source = f'https://sop.org/music/pw{int(m[1]):02}/'
        records.append(row(title, extract_lines(cells[6]), source, '讚美之泉',
                           album=album, series=series, composer=composer, writer=writer,
                           source_tags=[], language='Chinese (publisher catalogue)',
                           source_kind='official publisher', catalogue_url='https://sop.org/songs/'))
    print(f'SOP catalogue: {len(records)} records', flush=True)
    return records


def discover_simply():
    first = seed('simply_lyrics', 'https://simplyworship.hk/lyrics/')
    pages = sorted(set(urljoin('https://simplyworship.hk', a['href']) for a in first.select('a[href]')
                       if re.search(r'/lyrics/page/\d+/', a['href'])))
    soups = [first]
    with ThreadPoolExecutor(max_workers=2) as ex:
        for soup in ex.map(get, pages):
            soups.append(soup)
    songs = {}
    for soup in soups:
        for article in soup.select('article'):
            a = article.select_one('h1 a, h2 a')
            if not a or not re.search(r'/lyrics/[^/]+/', a['href']):
                continue
            tags = list(dict.fromkeys(t.get_text(' ', strip=True) for t in article.select('a[href*="/tag/"]')))
            songs[a['href']] = {'title': a.get_text(' ', strip=True), 'source': a['href'], 'source_tags': tags}
    print(f'Simply Worship discovery: {len(songs)} songs from {len(soups)} pages', flush=True)
    return list(songs.values())


def simply(item):
    s = get(item['source'])
    # Do not use a comma-union selector: document order would choose the outer
    # wrapper, which also contains unrelated-song recommendations and adverts.
    lyrics = s.select_one('.lyric-text .KCN-content')
    if lyrics is None:
        lyrics = s.select_one('.lyric-text')
    if lyrics is None:
        raise ValueError('No public lyrics block')
    for e in lyrics.select('.related-list,.ads,.translated'):
        e.decompose()
    raw = lyrics.get_text(' ', strip=True)
    language = 'Cantonese' if '廣東話' in raw or '粵語' in raw else ('Mandarin' if '普通話' in raw or '國語' in raw else 'Chinese (unspecified)')
    # Plugin chord markup is inline; remove before assembling contiguous p text.
    for e in lyrics.select('span.chord, span.chord-container, .KCN-chord, .chord'):
        e.decompose()
    lines = extract_lines(lyrics)
    artist = []
    for a in s.select('a[href*="/artist/"]'):
        name = a.get_text(' ', strip=True)
        if name and name not in artist:
            artist.append(name)
    return row(item['title'], lines, item['source'], 'Simply Worship', source_tags=item['source_tags'],
               language=language, source_kind='Hong Kong worship reference library', artist=' / '.join(artist), album='')


def discover_lamb():
    s = seed('lamb_catalog_real', 'https://w247.net/catalog/')
    urls = set(urljoin('https://w247.net', a['href']) for a in s.select('a[href]')
               if re.match(r'/songs/[^/]+/$', a['href']) and not re.match(r'/songs/\d+/', a['href']))
    # Include recent songs outside the published album catalogue.
    s = seed('lamb_real', 'https://w247.net/songs/')
    for a in s.select('a[href]'):
        if re.match(r'/songs/[^/]+/$', a['href']) and not re.match(r'/songs/\d+/', a['href']):
            urls.add(urljoin('https://w247.net', a['href']))
    print(f'Lamb discovery: {len(urls)} songs', flush=True)
    return sorted(urls)


def lamb(url):
    s = get(url)
    title = s.select_one('[data-pagefind-meta="title"]')
    body = s.select_one('.markdown-content')
    if not title or not body:
        raise ValueError('No song title/lyrics block')
    tags = list(dict.fromkeys(a.get_text(' ', strip=True) for a in s.select('#post-container a[href*="/songs/tag/"]')))
    lang = 'Cantonese' if '粵語' in tags else 'Taiwanese Hokkien' if '台語' in tags else 'Chinese (unspecified)'
    albums = [a.get_text(' ', strip=True) for a in s.select('#post-container a[href*="/product/"]') if a.get_text(strip=True)]
    return row(title.get_text(' ', strip=True), extract_lines(body), url, '小羊詩歌',
               source_tags=tags, language=lang, source_kind='official publisher', album=' / '.join(albums))


def deduplicate(records):
    accepted, excluded = [], []
    # Prefer first-party source over reference library. Stable order on reruns.
    records.sort(key=lambda r: (0 if r['source_kind'] == 'official publisher' else 1, r['provider'], r['source'], r['title']))
    by_title, by_lyrics = {}, {}
    for r in records:
        title = normal(r['title'])
        base = re.sub(r'[（(](?:粵語|粤语|国语|國語|普通話|女聲|男聲|現場|现场|Live|伴唱|演奏)[^）)]*[）)]', '', title, flags=re.I)
        key = re.sub(r'[^\u3400-\u9fffa-z0-9]', '', base.lower())
        text = chinese(r['lyrics'])
        if len(text) < 30:
            excluded.append({'title':r['title'], 'source':r['source'], 'reason':'fewer than 30 Chinese lyric characters'})
            continue
        if re.search(r'medley|組曲|组曲|串燒|串烧|＋|\+', r['title'], re.I):
            excluded.append({'title':r['title'], 'source':r['source'], 'reason':'medley; not counted as a distinct song'})
            continue
        digest = hashlib.sha256(text.encode()).hexdigest()
        existing = by_title.get(key) or by_lyrics.get(digest)
        if existing:
            existing.setdefault('alternate_sources', []).append({'title':r['title'], 'source':r['source'], 'provider':r['provider'], 'source_tags':r.get('source_tags', [])})
            excluded.append({'title':r['title'], 'source':r['source'], 'reason':'same normalized title or identical normalized lyrics', 'retained_id':existing['id']})
            continue
        r['id'] = 'song-' + hashlib.sha256((key + '|' + digest).encode()).hexdigest()[:12]
        r['lyrics_sha256'] = digest
        r['chinese_character_count'] = len(text)
        r['split'] = 'holdout' if int(hashlib.sha256(key.encode()).hexdigest()[:8], 16) % 5 == 0 else 'discovery'
        accepted.append(r)
        by_title[key] = r
        by_lyrics[digest] = r
    # Detect near-identical lyrics under different titles (arrangements/reissues).
    shingle_index = defaultdict(list)
    final, sets = [], []
    for r in accepted:
        text = chinese(r['lyrics'])
        grams = set(text[i:i+5] for i in range(max(0, len(text)-4)))
        candidates = Counter(j for g in grams for j in shingle_index[g])
        match = None
        for j, overlap in candidates.most_common(8):
            similarity = overlap / max(1, len(grams | sets[j]))
            if similarity >= .82:
                match = j
                break
        if match is not None:
            keep = final[match]
            keep.setdefault('alternate_sources', []).append({'title':r['title'], 'source':r['source'], 'provider':r['provider'], 'source_tags':r.get('source_tags', [])})
            excluded.append({'title':r['title'], 'source':r['source'], 'reason':'lyric 5-character shingle Jaccard >= 0.82', 'retained_id':keep['id']})
        else:
            j = len(final)
            final.append(r)
            sets.append(grams)
            for g in grams:
                shingle_index[g].append(j)
    return final, excluded


def main():
    records = sop()
    failures = []
    with ThreadPoolExecutor(max_workers=2) as ex:
        a = ex.submit(discover_simply)
        b = ex.submit(discover_lamb)
        simply_items, lamb_urls = a.result(), b.result()
    jobs = [(simply, x) for x in simply_items] + [(lamb, x) for x in lamb_urls]
    with ThreadPoolExecutor(max_workers=4) as ex:
        futures = {ex.submit(fn, value): value for fn, value in jobs}
        for n, f in enumerate(as_completed(futures), 1):
            try:
                records.append(f.result())
            except Exception as e:
                failures.append({'item':futures[f], 'error':str(e)})
            if n % 50 == 0:
                print(f'Fetched {n}/{len(jobs)} pages; valid records {len(records)}; failures {len(failures)}', flush=True)
    corpus, excluded = deduplicate(records)
    (CACHE/'corpus-private.json').write_text(json.dumps(corpus, ensure_ascii=False, indent=2), encoding='utf-8')
    prior = CACHE/'collection-log.json'
    previous_requests = json.loads(prior.read_text(encoding='utf-8')).get('requests', []) if prior.exists() else []
    (CACHE/'collection-log.json').write_text(json.dumps({'date':DATE, 'raw_records':len(records), 'unique_songs':len(corpus),
        'parser_version':'2026-10-21-inner-lyrics-no-chords-no-headings-v3',
        'providers':dict(Counter(r['provider'] for r in corpus)), 'languages':dict(Counter(r['language'] for r in corpus)),
        'failures':failures, 'excluded':excluded, 'requests':previous_requests + EVENTS}, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'raw':len(records),'unique':len(corpus),'providers':dict(Counter(r['provider'] for r in corpus)),
                     'languages':dict(Counter(r['language'] for r in corpus)),'excluded':len(excluded),'failures':len(failures)}, ensure_ascii=False), flush=True)


if __name__ == '__main__':
    main()
