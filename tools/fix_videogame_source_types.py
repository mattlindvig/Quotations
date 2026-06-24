#!/usr/bin/env python3
"""
Fix source.type for known video game titles.

Sets source.type = 'VideoGame' for any quotation whose source title
starts with a known game name. Run with --dry-run first to preview.

Usage:
  python tools/fix_videogame_source_types.py --dry-run
  python tools/fix_videogame_source_types.py
"""

import argparse
import os
import re
from pymongo import MongoClient, UpdateMany
from pymongo.errors import BulkWriteError

MONGO_URI = os.environ["MONGO_URI"]
DB_NAME   = "quotations"
COLL_NAME = "quotations"

# Each entry is a regex prefix that matches the source title.
# Keep sorted for readability.
GAME_TITLE_PATTERNS = [
    r"^Ace Combat",
    r"^Baldur'?s Gate",
    r"^BioShock",
    r"^Colossal Cave Adventure",
    r"^Deus Ex",
    r"^Disco Elysium",
    r"^Dragon Age",
    r"^Dysfunctional Systems",
    r"^Fallout",
    r"^Fatal Frame",
    r"^Freespace",
    r"^Frenetic Five",
    r"^Gabriel Knight",
    r"^Grim Fandango",
    r"^Half-Life",
    r"^Halo",
    r"^The House in Fata Morgana",
    r"^The Journeyman Project",
    r"^The Last of Us",
    r"^The Longing",
    r"^The Witcher",
    r"^Mass Effect",
    r"^Medieval II",
    r"^MINERVA",
    r"^Monster Farm",
    r"^NetHack",
    r"^Nexus Mods",
    r"^No One Lives Forever",
    r"^Penumbra",
    r"^Plants vs\.? Zombies",
    r"^Planescape",
    r"^Portal",
    r"^Red Dead Redemption",
    r"^Senran Kagura",
    r"^Shantae",
    r"^SimCity",
    r"^Star Wars: Knights of the Old Republic",
    r"^StarCraft",
    r"^System Shock",
    r"^Touhou Project",
    r"^Umineko",
    r"^Undertale",
    r"^Wild ARMs",
    r"^Zork",
]


def main():
    parser = argparse.ArgumentParser(description="Fix source.type=VideoGame for known game titles")
    parser.add_argument("--dry-run", action="store_true", help="Preview changes without writing")
    parser.add_argument("--mongo-uri", default=MONGO_URI)
    args = parser.parse_args()

    client = MongoClient(args.mongo_uri)
    col = client[DB_NAME][COLL_NAME]

    print(f"Mode: {'DRY RUN' if args.dry_run else 'LIVE'}\n")

    total_matched = 0
    total_updated = 0

    for pattern in GAME_TITLE_PATTERNS:
        query = {
            'source.title': {'$regex': pattern, '$options': 'i'},
            'source.type':  {'$ne': 'VideoGame'},
        }
        count = col.count_documents(query)
        if count == 0:
            continue

        total_matched += count
        label = pattern.lstrip('^')
        print(f"  {count:5d}  {label}")

        if not args.dry_run:
            result = col.update_many(query, {'$set': {'source.type': 'VideoGame'}})
            total_updated += result.modified_count

    print(f"\nMatched:  {total_matched:,} documents")
    if args.dry_run:
        print("(dry run — no changes written)")
    else:
        print(f"Updated:  {total_updated:,} documents")


if __name__ == "__main__":
    main()
