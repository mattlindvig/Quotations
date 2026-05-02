#!/usr/bin/env python3
"""
Bulk quote importer for the Quotations app.
Supports CSV (quote, author, category) and JSON (quoteText, quoteAuthor) formats.

Usage:
  python tools/import_quotes.py quotes.csv more_quotes.json
  python tools/import_quotes.py --status Pending quotes.csv
  python tools/import_quotes.py --mongo-uri mongodb://... quotes.csv
"""

import argparse
import csv
import json
import sys
from datetime import datetime, timezone
from pathlib import Path

try:
    from pymongo import MongoClient
    from pymongo.errors import BulkWriteError
    from bson import ObjectId
except ImportError:
    print("pymongo is required. Install with:  pip install pymongo")
    sys.exit(1)

MONGO_URI = "mongodb://admin:password123@localhost:27017"
DB_NAME = "quotations"
COLLECTION = "quotations"
BATCH_SIZE = 500

# Name suffixes that can appear before a comma without indicating a source title
NAME_SUFFIXES = {"jr", "sr", "ii", "iii", "iv", "v", "md", "phd", "esq", "jd"}


def parse_author_source(raw: str) -> tuple[str, str | None]:
    """
    Split a raw author field into (author_name, source_title).

    Handles cases like:
      "Marilyn Monroe"                         -> ("Marilyn Monroe", None)
      "Stephen Chbosky, The Perks of ..."      -> ("Stephen Chbosky", "The Perks of ...")
      "Martin Luther King Jr., A Testament..." -> ("Martin Luther King Jr.", "A Testament...")
    """
    raw = raw.strip()
    if "," not in raw:
        return raw, None

    parts = raw.split(",")
    for i in range(len(parts) - 1):
        candidate = ",".join(parts[: i + 1]).strip()
        remainder = ",".join(parts[i + 1 :]).strip()
        last_word = candidate.split()[-1].lower().rstrip(".") if candidate.split() else ""
        if last_word in NAME_SUFFIXES:
            continue
        return candidate, remainder or None

    # Every comma was a name suffix — fall back to last comma as split point
    return ",".join(parts[:-1]).strip(), parts[-1].strip() or None


def make_doc(
    text: str,
    author_name: str,
    source_title: str | None,
    tags: list[str],
    status: str,
) -> dict:
    now = datetime.now(timezone.utc)
    return {
        "_id": ObjectId(),
        "text": text.strip(),
        "author": {"id": "", "name": author_name.strip()},
        "source": {
            "id": "",
            "title": source_title.strip() if source_title else "",
            "type": "Book" if source_title else "Other",
        },
        "tags": [t.strip() for t in tags if t.strip()],
        "status": status,
        "submittedBy": None,
        "submittedAt": now,
        "reviewedBy": None,
        "reviewedAt": now if status == "Approved" else None,
        "rejectionReason": None,
        "aiReview": {
            "status": "NotReviewed",
            "modelUsed": None,
            "reviewedAt": None,
            "retryCount": 0,
            "lastAttemptAt": None,
            "failureReason": None,
            "quoteAccuracy": None,
            "attributionAccuracy": None,
            "sourceAccuracy": None,
            "summary": None,
            "suggestedTags": [],
        },
        "createdAt": now,
        "updatedAt": now,
    }


def parse_csv(path: Path) -> list[tuple]:
    rows = []
    with open(path, newline="", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        for row in reader:
            text = row.get("quote", "").strip()
            if not text:
                continue
            author_name, source_title = parse_author_source(row.get("author", ""))
            tags = [t.strip() for t in row.get("category", "").split(",") if t.strip()]
            rows.append((text, author_name, source_title, tags))
    return rows


def parse_json(path: Path) -> list[tuple]:
    with open(path, encoding="utf-8") as f:
        data = json.load(f)
    rows = []
    for item in data:
        text = (
            item.get("quoteText") or item.get("text") or item.get("quote") or ""
        ).strip()
        if not text:
            continue
        raw_author = item.get("quoteAuthor") or item.get("author") or ""
        author_name = raw_author if isinstance(raw_author, str) else raw_author.get("name", "")
        rows.append((text, author_name.strip(), None, []))
    return rows


def import_file(path: Path, collection, status: str) -> tuple[int, int, int]:
    """Returns (processed, inserted, skipped)."""
    ext = path.suffix.lower()
    if ext == ".csv":
        rows = parse_csv(path)
    elif ext == ".json":
        rows = parse_json(path)
    else:
        print(f"  Skipping {path.name}: unsupported extension (use .csv or .json)")
        return 0, 0, 0

    existing = set(collection.distinct("text"))

    docs = []
    skipped = 0
    for text, author_name, source_title, tags in rows:
        if text in existing:
            skipped += 1
            continue
        docs.append(make_doc(text, author_name, source_title, tags, status))
        existing.add(text)

    if not docs:
        return len(rows), 0, skipped

    inserted = 0
    for i in range(0, len(docs), BATCH_SIZE):
        batch = docs[i : i + BATCH_SIZE]
        try:
            result = collection.insert_many(batch, ordered=False)
            inserted += len(result.inserted_ids)
        except BulkWriteError as e:
            inserted += e.details.get("nInserted", 0)
            errors = len(e.details.get("writeErrors", []))
            print(f"  Warning: {errors} write errors in batch")

    return len(rows), inserted, skipped


def main():
    parser = argparse.ArgumentParser(description="Bulk import quotes into MongoDB")
    parser.add_argument("files", nargs="+", help="CSV or JSON files to import")
    parser.add_argument(
        "--status",
        choices=["Approved", "Pending"],
        default="Approved",
        help="Status assigned to imported quotes (default: Approved)",
    )
    parser.add_argument(
        "--mongo-uri",
        default=MONGO_URI,
        help=f"MongoDB URI (default: {MONGO_URI})",
    )
    args = parser.parse_args()

    client = MongoClient(args.mongo_uri)
    collection = client[DB_NAME][COLLECTION]

    total_processed = total_inserted = total_skipped = 0

    for file_str in args.files:
        path = Path(file_str)
        if not path.exists():
            print(f"File not found: {path}")
            continue
        print(f"Importing {path.name}...")
        processed, inserted, skipped = import_file(path, collection, args.status)
        print(f"  {processed} rows → {inserted} inserted, {skipped} skipped (duplicates)")
        total_processed += processed
        total_inserted += inserted
        total_skipped += skipped

    print(f"\nDone: {total_processed} total → {total_inserted} inserted, {total_skipped} skipped")
    client.close()


if __name__ == "__main__":
    main()
