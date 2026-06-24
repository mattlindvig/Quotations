#!/usr/bin/env python3
"""
Import Wikiquote XML dump into MongoDB.

Downloads and streams the enwikiquote dump, parses wikitext using the same
logic as WikiquoteService.cs, and bulk-inserts new quotes (deduped via textHash).

Usage:
  python tools/import_wikiquote_dump.py                    # download + import
  python tools/import_wikiquote_dump.py --dry-run          # parse only, no writes
  python tools/import_wikiquote_dump.py --dump /tmp/wq.xml.bz2  # use local file
  python tools/import_wikiquote_dump.py --resume-after "Some Title"
"""

import argparse
import bz2
import hashlib
import os
import re
import sys
import unicodedata
import urllib.request
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from pymongo import MongoClient, InsertOne
from pymongo.errors import BulkWriteError

DUMP_URL    = "https://dumps.wikimedia.org/enwikiquote/latest/enwikiquote-latest-pages-articles.xml.bz2"
IMPORT_DATE = datetime(2026, 1, 1, tzinfo=timezone.utc)
MONGO_URI = os.environ.get("MONGO_URI", "mongodb://mongo:UbSviXPNWvFhMxBjqHlsYiTnOkuOPzdf@acela.proxy.rlwy.net:46933")
DB_NAME   = "quotations"
COLL_NAME = "quotations"
BATCH_SIZE     = 500
MIN_QUOTE_LEN  = 20
MW_NS = "http://www.mediawiki.org/xml/export-0.11/"

# ── Source type detection (mirrors DetectSourceType in C#) ─────────────────────

SOURCE_TYPE_MOVIE      = "Movie"
SOURCE_TYPE_TELEVISION = "Television"
SOURCE_TYPE_BOOK       = "Book"
SOURCE_TYPE_SPEECH     = "Speech"
SOURCE_TYPE_POEM       = "Poem"
SOURCE_TYPE_SONG       = "Song"
SOURCE_TYPE_PLAY       = "Play"
SOURCE_TYPE_VIDEOGAME  = "VideoGame"
SOURCE_TYPE_PROVERB    = "Proverb"
SOURCE_TYPE_SCRIPTURE  = "Scripture"
SOURCE_TYPE_OTHER      = "Other"

def detect_source_type(title: str, categories: list[str]) -> str:
    cats = [c.lower() for c in categories]
    t = title.lower()
    if any("film" in c or "movie" in c for c in cats): return SOURCE_TYPE_MOVIE
    if any("television" in c or "tv series" in c or "animated" in c for c in cats): return SOURCE_TYPE_TELEVISION
    if re.search(r'season\s+\d|episode|\(tv\)|\(tv series\)', t): return SOURCE_TYPE_TELEVISION
    if any("novel" in c or " book" in c for c in cats): return SOURCE_TYPE_BOOK
    if any("speech" in c or "address" in c for c in cats): return SOURCE_TYPE_SPEECH
    if any("poem" in c or "poetry" in c for c in cats): return SOURCE_TYPE_POEM
    if any("song" in c or "album" in c or "music" in c for c in cats): return SOURCE_TYPE_SONG
    if any("play" in c or "theatre" in c or "theater" in c for c in cats): return SOURCE_TYPE_PLAY
    if any("video game" in c or "video games" in c for c in cats): return SOURCE_TYPE_VIDEOGAME
    if any("proverb" in c for c in cats): return SOURCE_TYPE_PROVERB
    if any("scripture" in c or "religious text" in c or "bible" in c for c in cats): return SOURCE_TYPE_SCRIPTURE
    return SOURCE_TYPE_OTHER

def is_person_page(categories: list[str]) -> bool:
    lower = [c.lower() for c in categories]
    return any("people" in c or " born" in c or " died" in c for c in lower)

# ── Wikitext cleaning (mirrors CleanWikimarkup in C#) ─────────────────────────

_WIKI_LINK_LABEL = re.compile(r'\[\[(?:[^\]|]+)\|([^\]]+)\]\]')
_WIKI_LINK       = re.compile(r'\[\[([^\]]+)\]\]')
_EXT_LINK_LABEL  = re.compile(r'\[https?://\S+\s+([^\]]+)\]')
_EXT_LINK        = re.compile(r'\[https?://\S+\]')
_TEMPLATE        = re.compile(r'\{\{[^}]*\}\}')
_HTML_REF        = re.compile(r'<ref[^>]*>.*?</ref>', re.DOTALL)
_HTML_TAG        = re.compile(r'<[^>]+>')
_BOLD            = re.compile(r"'''([^']+?)'''")
_ITALIC          = re.compile(r"''([^']+?)''")
_FOOTNOTE        = re.compile(r'\[\d+\]')
_MULTI_SPACE     = re.compile(r'\s{2,}')
_CATEGORY        = re.compile(r'\[\[Category:([^\]|]+)', re.IGNORECASE)
_REDIRECT        = re.compile(r'^#redirect', re.IGNORECASE)

def clean_markup(text: str) -> str:
    text = _WIKI_LINK_LABEL.sub(r'\1', text)
    text = _WIKI_LINK.sub(r'\1', text)
    text = _EXT_LINK_LABEL.sub(r'\1', text)
    text = _EXT_LINK.sub('', text)
    text = _TEMPLATE.sub('', text)
    text = _HTML_REF.sub('', text)
    text = _HTML_TAG.sub('', text)
    text = _BOLD.sub(r'\1', text)
    text = _ITALIC.sub(r'\1', text)
    text = _FOOTNOTE.sub('', text)
    text = _MULTI_SPACE.sub(' ', text.strip())
    return text.strip('“”"\'  ')

def extract_categories(wikitext: str) -> list[str]:
    return [m.group(1).strip() for m in _CATEGORY.finditer(wikitext)]

def strip_section(line: str, depth: int) -> str:
    return line[depth:-depth].strip()

def is_meta(section: str) -> bool:
    lower = section.lower()
    return lower in {
        "external links", "see also", "references", "notes",
        "misattributed", "disputed", "about", "cast", "crew",
        "voice cast", "main cast", "recurring cast", "guest cast",
        "quotes about", "film", "television", "music",
    }

def clean_title(title: str) -> str:
    return re.sub(r'\s*\(.*?\)', '', title).strip()

def split_character_prefix(text: str):
    m = re.match(r'^\[?([A-Z][^:\]]{1,50})\]?:\s*(.+)$', text, re.DOTALL)
    if m:
        return m.group(1).strip(), m.group(2).strip()
    return '', text

def extract_source_from_attribution(attr: str) -> str:
    text = re.sub(r'^[—–\-]+\s*', '', attr).strip()
    m = re.match(r'^([^,\(]+)', text)
    return m.group(1).strip() if m else text

def extract_author_from_attribution(attr: str) -> str:
    text = re.sub(r'^[—–\-~]+\s*', '', attr).strip()
    text = re.sub(r'\[\d+\]', '', text).strip()
    m = re.match(r'^([^,\[]+)', text)
    return m.group(1).strip() if m else text

def extract_year(attr: str):
    m = re.search(r'\b(1[5-9]\d{2}|20[0-2]\d)\b', attr)
    return int(m.group()) if m else None

def build_tags(page_title: str, section: str, source_type: str) -> list[str]:
    tags = [source_type.lower()]
    slug = re.sub(r'[^a-z0-9]+', '-', page_title.lower()).strip('-')
    if slug:
        tags.append(slug)
    if len(section) > 1:
        sec_slug = re.sub(r'[^a-z0-9]+', '-', section.lower()).strip('-')
        if 2 < len(sec_slug) < 40:
            tags.append(sec_slug)
    return list(dict.fromkeys(tags))  # dedupe, preserve order

# ── Wikitext parser (mirrors ParseWikitext in C#) ─────────────────────────────

def parse_wikitext(wikitext: str, page_title: str, is_person: bool, source_type: str) -> list[dict]:
    results = []
    lines = wikitext.split('\n')
    author_name  = clean_title(page_title) if is_person else ''
    source_title = '' if is_person else clean_title(page_title)

    current_section    = ''
    current_subsection = ''
    pending_quote      = None
    pending_character  = None

    def flush(pending_q, pending_char, subsection):
        if pending_q is None:
            return
        meaningful = subsection if len(subsection) > 1 else ''
        src = '' if is_person else (meaningful or source_title)
        results.append({
            'text':        pending_q,
            'author':      pending_char or author_name,
            'source':      src,
            'source_type': source_type,
            'source_year': None,
            'tags':        build_tags(page_title, meaningful, source_type),
        })

    for raw_line in lines:
        line = raw_line.rstrip()

        # H3 ===subsection===
        if line.startswith('===') and line.endswith('===') and not line.startswith('===='):
            flush(pending_quote, pending_character, current_subsection)
            pending_quote = pending_character = None
            current_subsection = strip_section(line, 3)
            continue

        # H2 ==section==
        if line.startswith('==') and line.endswith('==') and not line.startswith('==='):
            flush(pending_quote, pending_character, current_subsection)
            pending_quote = pending_character = None
            current_section    = strip_section(line, 2)
            current_subsection = ''
            continue

        if is_meta(current_section):
            continue

        # Attribution line **
        if line.startswith('**') and not line.startswith('***') and pending_quote is not None:
            attr = clean_markup(line[2:].strip())
            year = extract_year(attr)

            if is_person:
                q_author = pending_character or author_name
                q_source = extract_source_from_attribution(attr)
            elif pending_character:
                q_author = pending_character
                q_source = source_title
            else:
                q_author = extract_author_from_attribution(attr)
                q_source = source_title

            results.append({
                'text':        pending_quote,
                'author':      q_author,
                'source':      q_source,
                'source_type': source_type,
                'source_year': year,
                'tags':        build_tags(page_title, current_subsection, source_type),
            })
            pending_quote = pending_character = None
            continue

        # Quote line *
        if line.startswith('*') and not line.startswith('**'):
            flush(pending_quote, pending_character, current_subsection)
            pending_quote = pending_character = None

            raw = clean_markup(line[1:].strip())
            if not raw:
                continue

            if not is_person:
                char, quote = split_character_prefix(raw)
                pending_character = char or None
                pending_quote     = quote or raw
            else:
                pending_quote = raw
            continue

        # Non-list line flushes pending quote without attribution
        if not line.startswith('*') and line.strip():
            flush(pending_quote, pending_character, current_subsection)
            pending_quote = pending_character = None

    flush(pending_quote, pending_character, current_subsection)
    return results

# ── Text normalisation & hashing (mirrors normalize_text.py) ──────────────────

def compute_text_hash(text: str) -> str:
    t = unicodedata.normalize('NFKD', text).lower().encode('ascii', 'ignore').decode('ascii')
    t = re.sub(r'[^a-z0-9]', ' ', t)
    return hashlib.sha256(' '.join(t.split()).encode('utf-8')).hexdigest()

# ── XML dump streaming ─────────────────────────────────────────────────────────

def iter_pages(stream):
    """Yield (title, wikitext) for every main-namespace non-redirect article."""
    ns_prefix = f'{{{MW_NS}}}'
    in_page  = False
    title    = None
    ns       = None
    text_buf = []
    in_text  = False

    for event, elem in ET.iterparse(stream, events=('start', 'end')):
        tag = elem.tag.replace(ns_prefix, '')

        if event == 'start':
            if tag == 'page':
                in_page  = True
                title    = None
                ns       = None
                text_buf = []
            elif tag == 'text' and in_page:
                in_text  = True
                text_buf = []
        else:  # end
            if not in_page:
                continue
            if tag == 'title':
                title = elem.text or ''
            elif tag == 'ns':
                ns = elem.text
            elif tag == 'text':
                in_text = False
                if elem.text:
                    text_buf.append(elem.text)
            elif tag == 'page':
                in_page = False
                if ns == '0' and title and text_buf:
                    wikitext = ''.join(text_buf)
                    if not _REDIRECT.match(wikitext):
                        yield title, wikitext
                elem.clear()

# ── Main ───────────────────────────────────────────────────────────────────────

def download_dump(dest: str):
    print(f"Downloading dump to {dest} ...")
    def _progress(count, block, total):
        pct = min(count * block / total * 100, 100)
        print(f"\r  {pct:.1f}%", end='', flush=True)
    urllib.request.urlretrieve(DUMP_URL, dest, reporthook=_progress)
    print()

def main():
    parser = argparse.ArgumentParser(description="Import Wikiquote XML dump into MongoDB")
    parser.add_argument('--dump',         help='Path to local .xml or .xml.bz2 dump file')
    parser.add_argument('--dry-run',      action='store_true', help='Parse without writing to MongoDB')
    parser.add_argument('--resume-after', metavar='TITLE', help='Skip pages up to and including this title')
    parser.add_argument('--mongo-uri',    default=MONGO_URI)
    args = parser.parse_args()

    dump_path = args.dump
    if not dump_path:
        dump_path = '/tmp/enwikiquote-dump.xml.bz2'
        if not os.path.exists(dump_path):
            download_dump(dump_path)
        else:
            print(f"Using cached dump: {dump_path}")

    client = MongoClient(args.mongo_uri) if not args.dry_run else None
    col    = client[DB_NAME][COLL_NAME]  if client else None

    pages_processed = 0
    quotes_parsed   = 0
    inserted        = 0
    skipped         = 0
    ops             = []
    resuming        = bool(args.resume_after)

    print(f"Mode: {'DRY RUN' if args.dry_run else 'LIVE'}")
    if resuming:
        print(f"Resuming after: {args.resume_after!r}")
    print()

    opener = bz2.open if dump_path.endswith('.bz2') else open
    with opener(dump_path, 'rb') as f:
        for title, wikitext in iter_pages(f):
            # Resume support
            if resuming:
                if title == args.resume_after:
                    resuming = False
                continue

            categories  = extract_categories(wikitext)
            is_person   = is_person_page(categories)
            source_type = detect_source_type(title, categories)

            quotes = parse_wikitext(wikitext, title, is_person, source_type)
            pages_processed += 1

            for q in quotes:
                if len(q['text']) < MIN_QUOTE_LEN:
                    continue
                quotes_parsed += 1

                if args.dry_run:
                    inserted += 1
                    continue

                doc = {
                    'text':      q['text'],
                    'textHash':  compute_text_hash(q['text']),
                    'author':    {'name': q['author']},
                    'source':    {
                        'title': q['source'],
                        'type':  q['source_type'],
                        'year':  q['source_year'],
                    },
                    'tags':      q['tags'],
                    'status':    'Approved',
                    'aiReview':  {'status': 'NotReviewed'},
                    'submittedAt': IMPORT_DATE,
                    'reviewedAt':  IMPORT_DATE,
                    'createdAt':   IMPORT_DATE,
                    'updatedAt':   IMPORT_DATE,
                }
                ops.append(InsertOne(doc))

            if pages_processed % 500 == 0:
                print(f"  Pages: {pages_processed:,}  Quotes: {quotes_parsed:,}  Inserted: {inserted:,}  Skipped: {skipped:,}")
                sys.stdout.flush()

            # Flush batch
            if col is not None and len(ops) >= BATCH_SIZE:
                try:
                    r = col.insert_many([o._doc for o in ops], ordered=False)
                    inserted += len(r.inserted_ids)
                except BulkWriteError as bwe:
                    inserted += bwe.details.get('nInserted', 0)
                    skipped  += sum(1 for e in bwe.details.get('writeErrors', []) if e['code'] == 11000)
                ops.clear()

    # Final flush
    if col is not None and ops:
        try:
            r = col.insert_many([o._doc for o in ops], ordered=False)
            inserted += len(r.inserted_ids)
        except BulkWriteError as bwe:
            inserted += bwe.details.get('nInserted', 0)
            skipped  += sum(1 for e in bwe.details.get('writeErrors', []) if e['code'] == 11000)

    print()
    print(f"Pages processed: {pages_processed:,}")
    print(f"Quotes parsed:   {quotes_parsed:,}")
    if args.dry_run:
        print(f"Would insert:    {inserted:,}")
    else:
        print(f"Inserted:        {inserted:,}")
        print(f"Skipped (dup):   {skipped:,}")


if __name__ == '__main__':
    main()
