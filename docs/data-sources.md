# Quote Data Sources

## Current Sources (Completed)

| Source | License | Quotes Added | Notes |
|---|---|---|---|
| Wikiquote XML dump | CC BY-SA | ~36,500 net new | Full dump import June 2026 |
| Brainy/Goodreads-style bulk import | Unknown origin | ~1,345,000 | Original seed data |

---

## Legal Sources — Approved for Use

### OpenSubtitles (Priority: HIGH)
- **URL**: opensubtitles.org
- **License**: CC0 for subtitle files
- **Content**: TV show and movie dialogue for tens of thousands of titles
- **Volume**: Potentially millions of lines
- **Approach**: Download .srt files, extract quotable lines by filtering for length, punctuation, and non-action content
- **Gaps filled**: Television (currently only 12,557 quotes), Movies
- **Status**: Not started

### Wikiquote XML Dump (re-sync)
- **URL**: dumps.wikimedia.org/enwikiquote
- **License**: CC BY-SA
- **Content**: Curated quotes across all categories
- **Approach**: Re-run `tools/import_wikiquote_dump.py` periodically as Wikiquote grows
- **Status**: Completed (June 2026). Re-run every 6-12 months.

### Fandom Wikis — CC BY-SA (Priority: MEDIUM)
- **License**: CC BY-SA (same as Wikipedia)
- **Content**: Franchise-specific quotes (games, shows, movies)
- **Approach**: `tools/scrape_fandom_wiki.py` — tracked via `fandomWikiSyncs` collection
- **Limitation**: Low quote density on most wikis; best for headquotes per character
- **Status**: Scraper built, Mass Effect dry-run yielded ~50 quotes total. Not worth running for most game wikis.
- **Best candidates**: TV show wikis (Breaking Bad, Game of Thrones, The Office) may have better episode-level quote sections

### Game Dialogue Files (Priority: MEDIUM — requires game ownership)
- **License**: No explicit restriction on personal extraction; dialogue itself not copyrightable as a database
- **Content**: Complete dialogue for any game that stores text in accessible formats
- **Examples**:
  - Mass Effect: `.tlk` files, extractable via ME3Tweaks tools
  - Fallout: `.esp`/`.esm` files, extractable via FO4Edit/xEdit
  - The Witcher: XML dialogue files in game data
- **Approach**: Per-game extraction pipeline
- **Status**: Not started. Requires game file access.

---

## Sources Evaluated and Rejected

### quotes.net / STANDS4
- **Reason**: ToS explicitly prohibits copying — "No person is authorized to use, copy or distribute any portion the Web Site"
- **API**: Free tier limited to 100 queries/day (impractical for bulk)
- **Also**: Content likely sourced from Wikiquote anyway

### FavQs
- **Reason**: ToS too restrictive (evaluated June 2026)

### BrainyQuote / AZQuotes / Goodreads
- **Reason**: All prohibit scraping. Content is either Wikiquote-derived or user-submitted under restrictive terms.

### GameFAQs game transcripts
- **Reason**: ToS prohibits use; copyright owned by submitters

### OpenSubtitles (scraping)
- **Note**: Scraping the HTML site is not the right approach. Download subtitle files directly via their data dumps or API.

---

## Tracking

Wiki import progress is tracked in MongoDB: `quotations.fandomWikiSyncs`

```
db.fandomWikiSyncs.find({}, {franchise:1, category:1, status:1, quotesImported:1})
```
