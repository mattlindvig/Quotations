#!/usr/bin/env python3
"""
Run AI review using a local Ollama model against the live API.

The script:
  1. Logs in as an admin user to get a JWT token
  2. Fetches batches of unreviewed quotations from the API
  3. Sends each quote to a local Ollama model
  4. Posts the result back to the API

Usage:
  python tools/local_review.py
  python tools/local_review.py --model qwen2.5:32b --batch-size 20 --concurrency 4

Environment variables:
  API_URL      Base URL of the Railway API  (default: https://quotationhub.net/api/v1)
  API_USER     Admin username               (required)
  API_PASS     Admin password               (required)
  OLLAMA_URL   Ollama base URL              (default: http://localhost:11434)
"""

import argparse
import json
import logging
import os
import re
import sys
import time
from concurrent.futures import ThreadPoolExecutor, as_completed
from typing import Optional

import requests

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s  %(levelname)-7s  %(message)s",
    datefmt="%H:%M:%S",
)
log = logging.getLogger(__name__)

# ── Prompt (mirrors AnthropicService.BuildLeanPrompt) ────────────────────────

CANONICAL_TAGS = [
    "wisdom", "life", "love", "happiness", "success", "courage", "friendship",
    "truth", "justice", "freedom", "hope", "perseverance", "humor", "death",
    "faith", "science", "politics", "history", "philosophy", "literature",
    "art", "music", "nature", "war", "peace", "family", "education", "money",
    "time", "change", "leadership", "creativity", "kindness", "anger",
    "fear", "loneliness", "ambition", "identity", "morality", "religion",
]

BANNED_TAGS = [
    "quotes", "quotations", "famous", "inspirational", "motivational",
    "sayings", "words", "thoughts", "ideas", "people", "best", "great",
]

LEAN_PROMPT = (
    "You are a quotation editor. Given a quote, author, source, and current tags:\n\n"
    "1. Tags — return the COMPLETE FINAL tag list (fully replaces existing tags):\n"
    "   - Remove useless generic tags such as: "
    + ", ".join(sorted(BANNED_TAGS)[:20])
    + ", and similar.\n"
    "   - Remove tags that duplicate the author's name or source title (those are shown as separate fields).\n"
    "   - Remove navigation garbage: anything containing '>', '{', '}', '|', or MediaWiki template syntax.\n"
    "   - Consolidate near-duplicates to the simpler form (e.g. keep \"mass-effect\" not \"mass-effect-video-game\").\n"
    "   - Keep specific accurate tags (character names, franchise names, work titles).\n"
    "   - Add relevant thematic tags from this list where appropriate: "
    + ", ".join(CANONICAL_TAGS)
    + "\n"
    "   - Aim for 2-8 meaningful tags total.\n\n"
    "2. Fix the author if blank, \"Unknown\", \"Anonymous\", a placeholder, or parsing garbage (URL, HTML, Wikipedia article title, chapter heading, episode name, or any non-person text).\n"
    "   → Always return your best identification — even at lower confidence. Return null only if the existing value is already a correct person name.\n\n"
    "3. Fix the source title if blank, \"Unknown\", \"Other\", a placeholder, or parsing garbage.\n"
    "   → Always return your best identification — even at lower confidence. Return null only if the existing value is already correct.\n\n"
    "4. Fix the source type if it is wrong or \"Other\" and you can determine the real type.\n"
    "   Available types: Book, Movie, Television, Speech, Interview, Poem, Song, Play, Musical,\n"
    "   VideoGame, Comic, Article, Letter, Podcast, Documentary, Scripture, Proverb, Memoir, Standup, Organization, Other.\n"
    "   Return null if the current type is already correct.\n\n"
    "5. If the quote text has a clear wording error vs. the widely-known canonical version — provide the corrected text.\n\n"
    "6. Set reject=true if the text is NOT a real quotation (episode list, file metadata, navigation text, raw HTML, etc.).\n\n"
    "7. Write a one-sentence summary of the quote's core idea (summary).\n\n"
    "8. Assess authenticity: set isLikelyAuthentic=true if correctly attributed, false if likely misattributed, null if genuinely uncertain. "
    "Explain briefly in authenticityReasoning. If misattributed, give the likely correct attribution in correctAttribution.\n\n"
    "9. Estimate when the quote originates (approximateEra): \"ancient\", \"medieval\", \"17th century\", \"18th century\", "
    "\"19th century\", \"early 20th century\", \"mid 20th century\", \"late 20th century\", \"21st century\", or null.\n\n"
    "10. Detect the language as an ISO 639-1 code (language): \"en\", \"fr\", \"de\", \"es\", etc.\n\n"
    "11. Rate significance and memorability 1-5 (qualityScore): 1=trivial, 2=minor, 3=notable, 4=memorable, 5=iconic.\n\n"
    "12. Identify the dominant tone (mood): \"inspirational\", \"philosophical\", \"humorous\", \"melancholic\", "
    "\"critical\", \"cautionary\", \"reflective\", or \"neutral\".\n\n"
    "Rules: Set a field to null if no change is needed. Only correct text if highly confident.\n\n"
    'Respond ONLY with valid JSON:\n'
    '{"tags":["tag1"],"author":null,"source":null,"sourceType":null,"text":null,"reject":false,'
    '"summary":"One-sentence theme.","isLikelyAuthentic":true,"authenticityReasoning":"Brief explanation.",'
    '"correctAttribution":null,"approximateEra":"20th century","language":"en","qualityScore":4,"mood":"inspirational"}'
)


def build_quote_context(q: dict) -> str:
    tags = ", ".join(q.get("tags") or []) or "(none)"
    return (
        f'Quotation text: "{q["text"]}"\n'
        f'Attributed to: {q["authorName"]}\n'
        f'Source: {q["sourceTitle"]} ({q["sourceType"]})\n'
        f'Current tags: {tags}'
    )


# ── Ollama call ───────────────────────────────────────────────────────────────

def call_ollama(q: dict, model: str, ollama_url: str, timeout: int = 120) -> Optional[dict]:
    url = ollama_url.rstrip("/") + "/api/chat"
    payload = {
        "model": model,
        "stream": False,
        "messages": [
            {"role": "user", "content": LEAN_PROMPT + "\n\n" + build_quote_context(q)},
        ],
        "options": {"temperature": 0},
    }
    try:
        r = requests.post(url, json=payload, timeout=timeout)
        r.raise_for_status()
        content = r.json()["message"]["content"]
        return parse_result(content, model)
    except Exception as e:
        log.warning("Ollama error for %s: %s", q["id"], e)
        return None


def parse_result(content: str, model: str) -> Optional[dict]:
    try:
        start = content.index("{")
        end = content.rindex("}") + 1
        data = json.loads(content[start:end])
        tags = [
            t.strip().lower()
            for t in (data.get("tags") or [])
            if isinstance(t, str) and t.strip() and t.strip().lower() not in BANNED_TAGS
        ]
        qs = data.get("qualityScore")
        return {
            "tags": tags,
            "author": data.get("author") or None,
            "source": data.get("source") or None,
            "sourceType": data.get("sourceType") or None,
            "text": data.get("text") or None,
            "reject": bool(data.get("reject")),
            "modelUsed": model,
            "summary": data.get("summary") or None,
            "isLikelyAuthentic": data.get("isLikelyAuthentic"),
            "authenticityReasoning": data.get("authenticityReasoning") or None,
            "correctAttribution": data.get("correctAttribution") or None,
            "approximateEra": data.get("approximateEra") or None,
            "language": data.get("language") or None,
            "qualityScore": int(qs) if isinstance(qs, (int, float)) else None,
            "mood": data.get("mood") or None,
        }
    except Exception as e:
        log.warning("Parse error: %s — raw: %s", e, content[:200])
        return None


# ── API helpers ───────────────────────────────────────────────────────────────

class ApiClient:
    def __init__(self, base_url: str, username: str, password: str):
        self.base = base_url.rstrip("/")
        self.session = requests.Session()
        self._login(username, password)

    def _login(self, username: str, password: str):
        r = self.session.post(
            f"{self.base}/auth/login",
            json={"username": username, "password": password},
            timeout=15,
        )
        r.raise_for_status()
        token = r.json()["data"]["token"]
        self.session.headers["Authorization"] = f"Bearer {token}"
        log.info("Logged in as %s", username)

    def fetch_batch(self, size: int) -> list:
        r = self.session.get(
            f"{self.base}/ai-review/local/batch",
            params={"size": size},
            timeout=30,
        )
        r.raise_for_status()
        return r.json()["data"]

    def submit_result(self, qid: str, result: dict) -> str:
        r = self.session.post(
            f"{self.base}/ai-review/local/{qid}/result",
            json=result,
            timeout=15,
        )
        r.raise_for_status()
        return r.json()["data"]["action"]


# ── Main loop ─────────────────────────────────────────────────────────────────

def process_one(q: dict, model: str, ollama_url: str, client: ApiClient) -> tuple[str, str]:
    result = call_ollama(q, model, ollama_url)
    if result is None:
        return q["id"], "error"
    action = client.submit_result(q["id"], result)
    return q["id"], action


def main():
    parser = argparse.ArgumentParser(description="Run local AI review via Ollama")
    parser.add_argument("--model",       default="qwen2.5:32b",       help="Ollama model name")
    parser.add_argument("--batch-size",  type=int, default=50,         help="Quotes per fetch")
    parser.add_argument("--concurrency", type=int, default=4,          help="Parallel Ollama calls")
    parser.add_argument("--api-url",     default=os.getenv("API_URL", "https://quotationhub.net/api/v1"))
    parser.add_argument("--ollama-url",  default=os.getenv("OLLAMA_URL", "http://localhost:11434"))
    parser.add_argument("--limit",       type=int, default=0,          help="Stop after N reviews (0 = unlimited)")
    args = parser.parse_args()

    username = os.getenv("API_USER")
    password = os.getenv("API_PASS")
    if not username or not password:
        sys.exit("Set API_USER and API_PASS environment variables")

    # Verify Ollama is reachable
    try:
        requests.get(args.ollama_url.rstrip("/") + "/api/tags", timeout=5).raise_for_status()
    except Exception as e:
        sys.exit(f"Cannot reach Ollama at {args.ollama_url}: {e}")

    client = ApiClient(args.api_url, username, password)

    reviewed = deleted = errors = 0
    start = time.time()

    while True:
        batch = client.fetch_batch(args.batch_size)
        if not batch:
            log.info("No more unreviewed quotations. Done.")
            break

        log.info("Fetched %d quotes — processing with %d workers", len(batch), args.concurrency)

        with ThreadPoolExecutor(max_workers=args.concurrency) as pool:
            futures = {
                pool.submit(process_one, q, args.model, args.ollama_url, client): q["id"]
                for q in batch
            }
            for future in as_completed(futures):
                qid, action = future.result()
                if action == "reviewed":
                    reviewed += 1
                elif action == "deleted":
                    deleted += 1
                else:
                    errors += 1

        total = reviewed + deleted + errors
        elapsed = time.time() - start
        rate = total / elapsed if elapsed > 0 else 0
        log.info("Progress: %d reviewed  %d deleted  %d errors  (%.1f/s)", reviewed, deleted, errors, rate)

        if args.limit and total >= args.limit:
            log.info("Reached --limit %d, stopping.", args.limit)
            break

    elapsed = time.time() - start
    log.info("Finished in %.0fs — reviewed=%d  deleted=%d  errors=%d", elapsed, reviewed, deleted, errors)


if __name__ == "__main__":
    main()
