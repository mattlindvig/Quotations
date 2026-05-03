#!/usr/bin/env python3
"""
Strip non-allowed tags from all quotations in MongoDB.
Maps common aliases to allowed tags, removes everything else.

Usage:
  uv run --with pymongo python3 tools/cleanup_tags.py
  uv run --with pymongo python3 tools/cleanup_tags.py --dry-run
"""

import argparse
from pymongo import MongoClient

ALLOWED_TAGS = {
    "inspiration", "wisdom", "humor", "love", "friendship", "leadership",
    "success", "failure", "philosophy", "science", "politics", "history",
    "art", "literature", "courage", "justice", "nature", "faith",
    "education", "change", "family", "peace", "ambition", "perseverance",
    "fiction", "non-fiction",
}

# Map known aliases → allowed tag (lowercase keys)
TAG_MAP = {
    "inspirational": "inspiration",
    "inspirational-quotes": "inspiration",
    "motivational": "inspiration",
    "motivation": "inspiration",
    "inspired": "inspiration",
    "inspire": "inspiration",
    "romance": "love",
    "romantic": "love",
    "relationship": "love",
    "relationships": "love",
    "life-lessons": "wisdom",
    "life-lesson": "wisdom",
    "truth": "wisdom",
    "mindset": "wisdom",
    "growth": "wisdom",
    "self-improvement": "wisdom",
    "self-help": "wisdom",
    "poetry": "literature",
    "writing": "literature",
    "books": "literature",
    "reading": "literature",
    "novel": "fiction",
    "storytelling": "literature",
    "music": "art",
    "creativity": "art",
    "design": "art",
    "painting": "art",
    "theater": "art",
    "theatre": "art",
    "film": "art",
    "cinema": "art",
    "god": "faith",
    "religion": "faith",
    "spiritual": "faith",
    "spirituality": "faith",
    "faith-hope": "faith",
    "funny": "humor",
    "comedy": "humor",
    "fun": "humor",
    "witty": "humor",
    "knowledge": "education",
    "learning": "education",
    "teaching": "education",
    "school": "education",
    "study": "education",
    "power": "leadership",
    "management": "leadership",
    "business": "leadership",
    "entrepreneurship": "ambition",
    "entrepreneur": "ambition",
    "goals": "ambition",
    "goal": "ambition",
    "dream": "ambition",
    "dreams": "ambition",
    "determination": "perseverance",
    "persistence": "perseverance",
    "resilience": "perseverance",
    "hardship": "perseverance",
    "environment": "nature",
    "environmental": "nature",
    "animals": "nature",
    "freedom": "justice",
    "rights": "justice",
    "equality": "justice",
    "war": "history",
    "revolution": "history",
    "community": "family",
    "society": "family",
}


def normalize_tag(tag: str) -> str | None:
    """Return the canonical allowed tag for a raw tag, or None to drop it."""
    clean = tag.strip().lower()
    if clean in ALLOWED_TAGS:
        return clean
    return TAG_MAP.get(clean)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--mongo-url", default="mongodb://localhost:27017")
    parser.add_argument("--db", default="quotations")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    client = MongoClient(args.mongo_url)
    col = client[args.db]["quotations"]

    total = col.count_documents({})
    print(f"Total quotations: {total:,}")

    BATCH = 1000
    updated = 0
    already_clean = 0
    offset = 0

    while True:
        docs = list(col.find({}, {"_id": 1, "tags": 1}).skip(offset).limit(BATCH))
        if not docs:
            break
        offset += BATCH

        for doc in docs:
            raw_tags = doc.get("tags") or []
            new_tags = list(dict.fromkeys(
                t for t in (normalize_tag(r) for r in raw_tags) if t
            ))

            if new_tags == raw_tags:
                already_clean += 1
                continue

            updated += 1
            if not args.dry_run:
                col.update_one({"_id": doc["_id"]}, {"$set": {"tags": new_tags}})

        print(f"  processed {min(offset, total):,}/{total:,}  updated so far: {updated:,}", end="\r")

    print()
    label = "[DRY RUN] Would update" if args.dry_run else "Updated"
    print(f"{label} {updated:,} quotations ({already_clean:,} already clean)")
    client.close()


if __name__ == "__main__":
    main()
