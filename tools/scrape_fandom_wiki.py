#!/usr/bin/env python3
"""
Scrape a Fandom wiki for quotes and import into MongoDB.

Currently supports Mass Effect wiki structure (Unique Dialogue pages + headquotes).
Designed to be generalized to other Fandom wikis over time.

Usage:
  python tools/scrape_fandom_wiki.py --wiki masseffect --dry-run
  python tools/scrape_fandom_wiki.py --wiki masseffect

Tracking: each wiki's import status is recorded in the fandomWikiSyncs collection.
"""

import argparse
import hashlib
import os
import re
import sys
import time
import unicodedata
import urllib.parse
import urllib.request
import json
from datetime import datetime, timezone
from pymongo import MongoClient, InsertOne
from pymongo.errors import BulkWriteError

MONGO_URI = os.environ["MONGO_URI"]
DB_NAME      = "quotations"
BATCH_SIZE   = 200
MIN_QUOTE_LEN = 20
MAX_QUOTE_LEN = 2000
IMPORT_DATE  = datetime(2026, 1, 1, tzinfo=timezone.utc)
REQUEST_DELAY = 1.0


# ---------------------------------------------------------------------------
# Utilities
# ---------------------------------------------------------------------------

def fetch_json(url, retries=3):
    for attempt in range(retries):
        try:
            req = urllib.request.Request(url, headers={"User-Agent": "QuotationsBot/1.0"})
            with urllib.request.urlopen(req, timeout=30) as r:
                return json.loads(r.read().decode("utf-8"))
        except Exception as e:
            if attempt == retries - 1:
                raise
            time.sleep(2 ** attempt)


def api_url(wiki_host, **params):
    base = "https://{}/api.php".format(wiki_host)
    params.setdefault("format", "json")
    qs = "&".join("{}={}".format(k, urllib.parse.quote(str(v))) for k, v in params.items())
    return "{}?{}".format(base, qs)


def compute_text_hash(text):
    normalized = unicodedata.normalize("NFKD", text)
    normalized = normalized.encode("ascii", "ignore").decode("ascii")
    normalized = normalized.lower()
    normalized = re.sub(r'\s+', ' ', normalized).strip()
    return hashlib.sha256(normalized.encode("utf-8")).hexdigest()


# ---------------------------------------------------------------------------
# Wikitext cleaning
# ---------------------------------------------------------------------------

_CLEAN_PATTERNS = [
    (re.compile(r'<tabber>.*?</tabber>', re.DOTALL | re.IGNORECASE), ''),
    (re.compile(r'<ref[^>]*>.*?</ref>', re.DOTALL | re.IGNORECASE), ''),
    (re.compile(r'<[^>]+>'), ''),
    (re.compile(r'\{\{[^}]*\}\}'), ''),
    (re.compile(r'\[\[(?:[^|\]]*\|)?([^\]]+)\]\]'), r'\1'),
    (re.compile(r"'''(.+?)'''"), r'\1'),
    (re.compile(r"''(.+?)''"), r'\1'),
    (re.compile(r'\[https?://\S+ ([^\]]+)\]'), r'\1'),
    (re.compile(r'\[https?://\S+\]'), ''),
    (re.compile(r'&amp;'), '&'),
    (re.compile(r'&lt;'), '<'),
    (re.compile(r'&gt;'), '>'),
    (re.compile(r'&nbsp;'), ' '),
    (re.compile(r'&quot;'), '"'),
    (re.compile(r'&#\d+;'), ''),
    (re.compile(r'\s+'), ' '),
]


def clean_wikitext(text):
    for pattern, replacement in _CLEAN_PATTERNS:
        text = pattern.sub(replacement, text)
    return text.strip()


def strip_outer_quotes(text):
    # Strip straight or curly double quotes
    if len(text) >= 2:
        first, last = text[0], text[-1]
        if (first == '"' and last == '"') or \
           (first == '“' and last == '”'):
            text = text[1:-1].strip()
    return text


# ---------------------------------------------------------------------------
# Mass Effect wiki parser
# ---------------------------------------------------------------------------

_GAME_SECTION = re.compile(r'^==\s*(Mass Effect[^=]*?)\s*==\s*$')
_HEADQUOTE    = re.compile(r'\{\{headquote\|([^}|]+)', re.IGNORECASE)
_BOLD_SPEAKER = re.compile(r"^'''([^']+)''':\s*(.+)$")
# Matches quoted strings (straight or curly double-quotes) of 20-500 chars
_INLINE_QUOTE = re.compile(
    u'[“”""]([^“”""]{20,500})[“”""]'
)

MASS_EFFECT_TITLES = {
    "mass effect: andromeda": "Mass Effect: Andromeda",
    "mass effect 3":          "Mass Effect 3",
    "mass effect 2":          "Mass Effect 2",
    "mass effect":            "Mass Effect",
}

_SKIP_LINE = re.compile(
    r'^(note|thumb|right|left|file:|category:|#redirect|==|\{\{|\[\[file)',
    re.IGNORECASE
)


def detect_game_title(section_header):
    lower = section_header.lower().strip()
    for key, title in MASS_EFFECT_TITLES.items():
        if key in lower:
            return title
    return "Mass Effect"


def parse_unique_dialogue_page(wikitext, character_name):
    """Extract quotes from a Unique Dialogue page.

    The ME wiki uses narrative prose: 'EDI says, "Quote text here."'
    We extract quoted strings and attribute them to the character.
    """
    quotes = []
    seen = set()
    current_game = "Mass Effect"

    # Strip heavy markup first
    wikitext = re.sub(r'\{\|.*?\|\}', '', wikitext, flags=re.DOTALL)
    wikitext = re.sub(r'<ref[^>]*>.*?</ref>', '', wikitext, flags=re.DOTALL | re.IGNORECASE)
    wikitext = re.sub(r'\[\[File:[^\]]+\]\]', '', wikitext, flags=re.IGNORECASE)
    wikitext = re.sub(r'<tabber>.*?</tabber>', '', wikitext, flags=re.DOTALL | re.IGNORECASE)

    for raw_line in wikitext.splitlines():
        line = raw_line.strip()
        if not line:
            continue

        m = _GAME_SECTION.match(line)
        if m:
            current_game = detect_game_title(m.group(1))
            continue

        if _SKIP_LINE.match(line):
            continue

        # Pattern 1: '''Speaker''': text
        m = _BOLD_SPEAKER.match(line)
        if m:
            speaker = clean_wikitext(m.group(1))
            char_lower = character_name.lower()
            if speaker.lower() in char_lower or char_lower in speaker.lower():
                text = clean_wikitext(m.group(2))
                text = strip_outer_quotes(text)
                if MIN_QUOTE_LEN <= len(text) <= MAX_QUOTE_LEN and text not in seen:
                    seen.add(text)
                    quotes.append({"text": text, "game": current_game})
            continue

        # Pattern 2: inline quoted strings within narrative prose
        cleaned = clean_wikitext(line)
        for m in _INLINE_QUOTE.finditer(cleaned):
            text = m.group(1).strip()
            text = strip_outer_quotes(text)
            if re.match(r'^(if |when |after |before |upon |she |he |they |this |the )', text, re.IGNORECASE):
                continue
            if text not in seen and MIN_QUOTE_LEN <= len(text) <= MAX_QUOTE_LEN:
                seen.add(text)
                quotes.append({"text": text, "game": current_game})

    return quotes


def parse_headquote(wikitext):
    m = _HEADQUOTE.search(wikitext)
    if not m:
        return None
    text = clean_wikitext(m.group(1))
    text = strip_outer_quotes(text)
    if len(text) >= MIN_QUOTE_LEN:
        return text
    return None


# ---------------------------------------------------------------------------
# Wiki discovery
# ---------------------------------------------------------------------------

def discover_unique_dialogue_pages(wiki_host):
    pages = []
    sroffset = 0
    while True:
        url = api_url(
            wiki_host,
            action="query", list="search",
            srsearch="Unique+Dialogue", srnamespace=0,
            srlimit=50, sroffset=sroffset,
        )
        data = fetch_json(url)
        results = data.get("query", {}).get("search", [])
        if not results:
            break
        for r in results:
            title = r["title"]
            if "unique dialogue" in title.lower() or "unique dialog" in title.lower():
                char = title.split("/")[0].strip()
                pages.append({"title": title, "character": char})
        if "continue" not in data:
            break
        sroffset = data["continue"].get("sroffset", sroffset + 50)
        time.sleep(REQUEST_DELAY)
    return pages


def discover_character_pages(wiki_host):
    pages = []
    cmcontinue = None
    while True:
        params = dict(
            action="query", list="categorymembers",
            cmtitle="Category:Characters", cmlimit=50, cmtype="page",
        )
        if cmcontinue:
            params["cmcontinue"] = cmcontinue
        url = api_url(wiki_host, **params)
        data = fetch_json(url)
        for m in data.get("query", {}).get("categorymembers", []):
            title = m["title"]
            if "/" not in title:
                pages.append({"title": title, "character": title})
        if "continue" not in data:
            break
        cmcontinue = data["continue"].get("cmcontinue")
        time.sleep(REQUEST_DELAY)
    return pages


def fetch_wikitext(wiki_host, title):
    url = api_url(
        wiki_host,
        action="query",
        titles=urllib.parse.quote(title),
        prop="revisions",
        rvprop="content",
        rvslots="main",
    )
    data = fetch_json(url)
    for page in data.get("query", {}).get("pages", {}).values():
        if "missing" in page:
            return None
        revs = page.get("revisions", [{}])
        slots = revs[0].get("slots", {})
        return slots.get("main", {}).get("*", "") or revs[0].get("*", "")
    return None


# ---------------------------------------------------------------------------
# Build quote documents
# ---------------------------------------------------------------------------

def build_doc(text, character, game_title, tags):
    return {
        "text":   text,
        "author": {"id": "", "name": character},
        "source": {"id": "", "title": game_title, "type": "VideoGame"},
        "tags":   tags,
        "status": "Approved",
        "aiReview": {"status": "NotReviewed"},
        "submittedAt": IMPORT_DATE,
        "reviewedAt":  IMPORT_DATE,
        "createdAt":   IMPORT_DATE,
        "updatedAt":   IMPORT_DATE,
        "textHash":    compute_text_hash(text),
    }


# ---------------------------------------------------------------------------
# Wiki configs
# ---------------------------------------------------------------------------

WIKI_CONFIG = {
    "masseffect": {
        "host":     "masseffect.fandom.com",
        "slug":     "masseffect",
        "name":     "Mass Effect",
        "category": "VideoGame",
        "tags":     ["mass effect", "video game", "sci-fi"],
    },
}


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(description="Scrape a Fandom wiki for quotes")
    parser.add_argument("--wiki",    required=True, choices=list(WIKI_CONFIG.keys()))
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--limit",   type=int, default=0, help="Max pages (0=all)")
    parser.add_argument("--mongo-uri", default=MONGO_URI)
    args = parser.parse_args()

    cfg = WIKI_CONFIG[args.wiki]
    wiki_host = cfg["host"]

    client   = MongoClient(args.mongo_uri)
    col      = client[DB_NAME]["quotations"]
    sync_col = client[DB_NAME]["fandomWikiSyncs"]

    sync = sync_col.find_one({"wikiSlug": cfg["slug"]})
    if sync and sync.get("status") == "completed" and not args.dry_run:
        print("Wiki '{}' already completed. Reset status or use --dry-run.".format(cfg["slug"]))
        sys.exit(0)

    if not args.dry_run:
        sync_col.update_one(
            {"wikiSlug": cfg["slug"]},
            {"$set": {"status": "in_progress", "startedAt": datetime.now(timezone.utc)}},
            upsert=True,
        )

    print("Wiki:   {}".format(wiki_host))
    print("Mode:   {}".format("DRY RUN" if args.dry_run else "LIVE"))
    print()

    print("Discovering Unique Dialogue pages...")
    dialogue_pages = discover_unique_dialogue_pages(wiki_host)
    print("  Found {} pages".format(len(dialogue_pages)))

    print("Discovering character pages for headquotes...")
    char_pages = discover_character_pages(wiki_host)
    print("  Found {} character pages".format(len(char_pages)))
    print()

    all_pages = [("dialogue", p) for p in dialogue_pages] + [("character", p) for p in char_pages]
    if args.limit:
        all_pages = all_pages[:args.limit]

    pages_done = quotes_parsed = inserted = skipped = 0
    ops = []

    def flush():
        nonlocal inserted, skipped, ops
        if not ops:
            return
        if args.dry_run:
            inserted += len(ops)
            ops.clear()
            return
        try:
            r = col.insert_many([o._doc for o in ops], ordered=False)
            inserted += len(r.inserted_ids)
        except BulkWriteError as bwe:
            inserted += bwe.details.get("nInserted", 0)
            skipped  += sum(1 for e in bwe.details.get("writeErrors", []) if e["code"] == 11000)
        ops.clear()

    for page_type, page_info in all_pages:
        title     = page_info["title"]
        character = page_info["character"]
        time.sleep(REQUEST_DELAY)

        wikitext = fetch_wikitext(wiki_host, title)
        if not wikitext:
            continue

        page_quotes = []

        if page_type == "dialogue":
            for q in parse_unique_dialogue_page(wikitext, character):
                tags = cfg["tags"] + [character.lower().replace(" ", "-")]
                page_quotes.append(build_doc(q["text"], character, q["game"], tags))

        elif page_type == "character":
            hq = parse_headquote(wikitext)
            if hq:
                tags = cfg["tags"] + [character.lower().replace(" ", "-")]
                page_quotes.append(build_doc(hq, character, cfg["name"], tags))

        for doc in page_quotes:
            ops.append(InsertOne(doc))
            quotes_parsed += 1

        pages_done += 1
        if len(ops) >= BATCH_SIZE:
            flush()

        if pages_done % 20 == 0:
            print("  Pages: {:,}  Quotes: {:,}  Inserted: {:,}  Skipped: {:,}".format(
                pages_done, quotes_parsed, inserted, skipped))
            sys.stdout.flush()

    flush()

    print()
    print("Pages processed: {:,}".format(pages_done))
    print("Quotes parsed:   {:,}".format(quotes_parsed))
    if args.dry_run:
        print("Would insert:    {:,}".format(inserted))
    else:
        print("Inserted:        {:,}".format(inserted))
        print("Skipped (dupe):  {:,}".format(skipped))
        sync_col.update_one(
            {"wikiSlug": cfg["slug"]},
            {"$set": {
                "status":         "completed",
                "quotesImported": inserted,
                "lastSyncedAt":   datetime.now(timezone.utc),
            }}
        )
        print("Sync record updated for '{}'".format(cfg["slug"]))


if __name__ == "__main__":
    main()
