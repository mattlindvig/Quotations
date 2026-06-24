#!/usr/bin/env python3
"""
Index quotations from MongoDB into Meilisearch.

Modes:
  Full (default)  — indexes all Approved quotes. Run once after first deploy.
  Delta (--delta) — indexes only quotes updated since the last run. Safe to run
                    on a schedule (e.g. every 15 min via Railway cron) to catch
                    direct DB writes that bypass the API.

State for delta mode is stored in MongoDB (quotations.meilisearchSync) so it
survives container restarts.

Usage:
  python3 tools/index_meilisearch.py                  # full index
  python3 tools/index_meilisearch.py --delta          # sync changes since last run
  python3 tools/index_meilisearch.py --reset          # drop index and re-index everything
  python3 tools/index_meilisearch.py --limit 10000    # test with subset

Environment variables:
  MONGO_URI      - MongoDB connection string
  MEILI_URL      - Meilisearch URL (e.g. https://your-meili.railway.app)
  MEILI_API_KEY  - Meilisearch master key
"""

import argparse
import os
import sys
import time
from datetime import datetime, timezone
import meilisearch
from pymongo import MongoClient

MONGO_URI = os.environ["MONGO_URI"]
MEILI_URL     = os.environ.get("MEILI_URL", "http://localhost:7700")
MEILI_API_KEY = os.environ.get("MEILI_API_KEY", "")
MEILI_INDEX   = "quotations"
BATCH_SIZE    = 5_000
STATE_DOC_ID  = "state"


def build_doc(q):
    source = q.get("source") or {}
    author = q.get("author") or {}
    year   = source.get("year")
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


def configure_index(meili_index):
    print("Configuring index attributes...")
    t = meili_index.update_searchable_attributes(["text", "authorName", "sourceTitle"])
    meili_index.wait_for_task(t.task_uid)
    t = meili_index.update_filterable_attributes(["status", "sourceType", "tags", "year"])
    meili_index.wait_for_task(t.task_uid)
    print("  Done.")


def get_last_sync(sync_col):
    doc = sync_col.find_one({"_id": STATE_DOC_ID})
    return doc["lastSyncedAt"] if doc else None


def set_last_sync(sync_col, dt, docs_indexed):
    sync_col.update_one(
        {"_id": STATE_DOC_ID},
        {"$set": {
            "lastSyncedAt":  dt,
            "lastRunAt":     datetime.now(timezone.utc),
            "docsIndexed":   docs_indexed,
        }},
        upsert=True,
    )


def flush(meili_index, batch, indexed, total, t_start):
    if not batch:
        return
    meili_index.add_documents(batch, primary_key="id")
    elapsed = time.time() - t_start
    rate    = indexed / elapsed if elapsed > 0 else 0
    pct     = (indexed / total * 100) if total else 0
    print(f"  Indexed {indexed:,}/{total:,} ({pct:.1f}%)  {rate:.0f} docs/s")
    sys.stdout.flush()


def run_full(meili_index, col, limit):
    query = {"status": "Approved"}
    total = col.count_documents(query)
    if limit:
        total = min(total, limit)
    print(f"Full index: {total:,} docs → Meilisearch")

    indexed = 0
    last_id = None
    batch   = []
    t_start = time.time()

    while True:
        find_query = dict(query)
        if last_id is not None:
            find_query["_id"] = {"$gt": last_id}

        chunk = list(col.find(find_query, sort=[("_id", 1)], limit=BATCH_SIZE))
        if not chunk:
            break

        for q in chunk:
            batch.append(build_doc(q))
            last_id  = q["_id"]
            indexed += 1
            if limit and indexed >= limit:
                break
            if len(batch) >= BATCH_SIZE:
                flush(meili_index, batch, indexed, total, t_start)
                batch = []

        if limit and indexed >= limit:
            break

    flush(meili_index, batch, indexed, total, t_start)
    return indexed


def run_delta(meili_index, col, since):
    query = {
        "status":    "Approved",
        "updatedAt": {"$gt": since},
    }
    total = col.count_documents(query)
    print(f"Delta sync: {total:,} docs updated since {since.isoformat()}")

    if total == 0:
        return 0

    indexed = 0
    last_id = None
    batch   = []
    t_start = time.time()

    while True:
        find_query = dict(query)
        if last_id is not None:
            find_query["_id"] = {"$gt": last_id}

        chunk = list(col.find(find_query, sort=[("_id", 1)], limit=BATCH_SIZE))
        if not chunk:
            break

        for q in chunk:
            batch.append(build_doc(q))
            last_id  = q["_id"]
            indexed += 1
            if len(batch) >= BATCH_SIZE:
                flush(meili_index, batch, indexed, total, t_start)
                batch = []

    flush(meili_index, batch, indexed, total, t_start)
    return indexed


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--delta",  action="store_true", help="Only sync docs updated since last run")
    parser.add_argument("--reset",  action="store_true", help="Drop index and re-index everything")
    parser.add_argument("--limit",  type=int, default=0, help="Max docs (full mode only, for testing)")
    args = parser.parse_args()

    meili  = meilisearch.Client(MEILI_URL, MEILI_API_KEY)
    index  = meili.index(MEILI_INDEX)
    mongo  = MongoClient(MONGO_URI)
    col    = mongo["quotations"]["quotations"]
    sync_col = mongo["quotations"]["meilisearchSync"]

    if args.reset:
        print("Dropping existing index...")
        try:
            t = meili.delete_index(MEILI_INDEX)
            meili.wait_for_task(t.task_uid)
            print("  Dropped.")
        except Exception as e:
            print(f"  ({e})")
        configure_index(index)

    sync_started = datetime.now(timezone.utc)

    if args.delta:
        since = get_last_sync(sync_col)
        if since is None:
            print("No previous sync found — running full index instead.")
            indexed = run_full(index, col, limit=0)
        else:
            indexed = run_delta(index, col, since)
    else:
        if not args.reset:
            configure_index(index)
        indexed = run_full(index, col, args.limit)

    set_last_sync(sync_col, sync_started, indexed)

    elapsed = time.time() - time.mktime(sync_started.timetuple())
    print(f"\nDone: {indexed:,} docs in {elapsed:.1f}s")

    if not args.delta:
        print("\nNext steps:")
        print("  1. Wait for Meilisearch to finish indexing (check its Railway logs)")
        print("  2. Set Meilisearch__Enabled=true in the API's Railway variables")
        print("  3. After confirming search works, drop the old MongoDB text index:")
        print("     db.quotations.dropIndex('text_search_idx')")


if __name__ == "__main__":
    main()
