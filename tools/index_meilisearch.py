#!/usr/bin/env python3
"""
Bulk-index all approved quotations from MongoDB into Meilisearch.

Run this once after deploying Meilisearch to Railway to populate the index,
then the .NET backend keeps it in sync on new inserts/updates.

Usage:
  python3 tools/index_meilisearch.py
  python3 tools/index_meilisearch.py --limit 10000   # test run
  python3 tools/index_meilisearch.py --reset          # drop and re-create index first

Requirements:
  pip install pymongo meilisearch-python

Environment variables (or edit defaults below):
  MONGO_URI        - MongoDB connection string
  MEILI_URL        - Meilisearch URL  (e.g. https://your-meili.railway.app)
  MEILI_API_KEY    - Meilisearch master key
"""

import argparse
import os
import sys
import time
import meilisearch
from pymongo import MongoClient
from bson import ObjectId

MONGO_URI = os.environ.get(
    "MONGO_URI",
    "mongodb://mongo:UbSviXPNWvFhMxBjqHlsYiTnOkuOPzdf@acela.proxy.rlwy.net:46933",
)
MEILI_URL     = os.environ.get("MEILI_URL", "http://localhost:7700")
MEILI_API_KEY = os.environ.get("MEILI_API_KEY", "")
MEILI_INDEX   = "quotations"
BATCH_SIZE    = 5_000


def build_doc(q):
    source  = q.get("source") or {}
    author  = q.get("author") or {}
    year    = source.get("year")
    return {
        "id":          str(q["_id"]),
        "text":        q.get("text", ""),
        "authorName":  author.get("name", ""),
        "sourceTitle": source.get("title", ""),
        "sourceType":  source.get("type", "Unknown"),
        "status":      q.get("status", "Approved"),
        "tags":        q.get("tags") or [],
        "year":        int(year) if year and str(year).lstrip("-").isdigit() else None,
    }


def configure_index(index):
    print("Configuring index attributes...")
    t = index.update_searchable_attributes(["text", "authorName", "sourceTitle"])
    index.wait_for_task(t.task_uid)
    t = index.update_filterable_attributes(["status", "sourceType", "tags", "year"])
    index.wait_for_task(t.task_uid)
    print("  Done.")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--limit", type=int, default=0, help="Max docs to index (0=all)")
    parser.add_argument("--reset", action="store_true", help="Delete and re-create the index")
    parser.add_argument("--status", default="Approved", help="Filter by status (default: Approved)")
    args = parser.parse_args()

    client = meilisearch.Client(MEILI_URL, MEILI_API_KEY)
    index  = client.index(MEILI_INDEX)

    if args.reset:
        print("Deleting existing index...")
        try:
            t = client.delete_index(MEILI_INDEX)
            client.wait_for_task(t.task_uid)
            print("  Deleted.")
        except Exception as e:
            print(f"  (No index to delete or error: {e})")

    configure_index(index)

    mongo  = MongoClient(MONGO_URI)
    col    = mongo["quotations"]["quotations"]
    query  = {"status": args.status}
    total  = col.count_documents(query)
    if args.limit:
        total = min(total, args.limit)
    print(f"Indexing {total:,} documents from MongoDB → Meilisearch")
    print(f"  URL: {MEILI_URL}  Index: {MEILI_INDEX}")
    print()

    indexed  = 0
    last_id  = None
    batch    = []
    t_start  = time.time()

    while True:
        find_query = dict(query)
        if last_id is not None:
            find_query["_id"] = {"$gt": last_id}

        cursor = col.find(find_query, sort=[("_id", 1)], limit=BATCH_SIZE)
        chunk  = list(cursor)
        if not chunk:
            break

        for q in chunk:
            batch.append(build_doc(q))
            last_id = q["_id"]
            indexed += 1
            if args.limit and indexed >= args.limit:
                break

            if len(batch) >= BATCH_SIZE:
                task = index.add_documents(batch, primary_key="id")
                batch = []
                elapsed = time.time() - t_start
                rate    = indexed / elapsed if elapsed > 0 else 0
                pct     = indexed / total * 100
                print(f"  Indexed {indexed:,}/{total:,} ({pct:.1f}%)  {rate:.0f} docs/s")
                sys.stdout.flush()

        if args.limit and indexed >= args.limit:
            break

    if batch:
        task = index.add_documents(batch, primary_key="id")

    elapsed = time.time() - t_start
    print()
    print(f"Done: {indexed:,} docs indexed in {elapsed:.1f}s")
    print()
    print("Next steps:")
    print("  1. Wait for Meilisearch to finish indexing (check Railway dashboard logs)")
    print("  2. Set Meilisearch:Enabled = true in Railway environment variables")
    print("  3. After confirming search works, drop MongoDB text_search_idx:")
    print("     db.quotations.dropIndex('text_search_idx')")


if __name__ == "__main__":
    main()
