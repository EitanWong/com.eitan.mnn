#!/usr/bin/env python3
"""Audit public ModelScope/Hugging Face MNN repositories, downloading metadata only.

Uses stdlib only. Search coverage is a dated union of official owners, MNN tags
and MNN name search, not a claim to inspect every file on either entire hub.
No weights, archives, dependencies, native libraries or inference are downloaded/run.
"""

import argparse
from collections import Counter
from concurrent.futures import ThreadPoolExecutor, as_completed
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import time
from urllib.parse import quote, urljoin, urlsplit
from urllib.error import HTTPError
from urllib.request import Request, urlopen


QUERIES = {
    "modelscope": ("owner=MNN", "filter.library=mnn", "search=mnn"),
    "huggingface": ("author=taobao-mnn", "filter=mnn", "search=mnn"),
}
ARCHIVES = (".zip", ".tar.gz", ".tgz", ".7z")


def fetch(url, output):
    for attempt in range(3):
        try:
            with urlopen(Request(url, headers={"User-Agent": "MNN-Unity-CatalogAudit/1.0"}), timeout=45) as response:
                body = response.read()
                headers = response.headers
            filename = hashlib.sha256(url.encode()).hexdigest() + ".json"
            (output / "responses" / filename).write_bytes(body)
            return json.loads(body), headers
        except Exception as error:
            if isinstance(error, HTTPError) and error.code in (401, 403, 404, 410):
                raise
            if attempt == 2:
                raise
            time.sleep(attempt + 1)


def search(provider, query, output):
    root = "https://www.modelscope.cn" if provider == "modelscope" else "https://huggingface.co"
    url = root + ("/openapi/v1/models?" + query + "&page_size=50&sort=downloads&page_number=1" if provider == "modelscope"
                  else "/api/models?" + query + "&limit=100&full=true")
    seen, pages = {}, set()
    page = 1
    while url:
        if url in pages:
            raise ValueError("Repeated pagination cursor: " + url)
        pages.add(url)
        data, headers = fetch(url, output)
        previous = len(seen)
        if provider == "modelscope":
            if not data.get("success"):
                raise ValueError("Failed ModelScope search: " + str(data))
            items = data["data"]["models"]
        else:
            if not isinstance(data, list):
                raise ValueError("Invalid HF search response")
            items = data
        for item in items:
            seen[item["id"]] = item
        if provider == "modelscope":
            total = data["data"].get("total_count", 0)
            if (total > 0 and len(seen) >= total) or (total <= 0 and not items):
                break
            if previous == len(seen):
                raise ValueError("Truncated/repeated ModelScope pagination")
            page += 1
            url = re.sub(r"page_number=\d+", "page_number=" + str(page), url)
        else:
            match = re.search(r'<([^>]+)>\s*;\s*rel="?next"?', headers.get("Link", ""), re.I)
            url = urljoin(root, match[1]) if match else None
            if url and (urlsplit(url).netloc != urlsplit(root).netloc or urlsplit(url).scheme != "https" or urlsplit(url).path != "/api/models"):
                raise ValueError("HF pagination changed API origin")
    return list(seen.values()), {"provider": provider, "query": query, "pages": len(pages), "candidates": len(seen)}


def inspect(provider, item, output):
    identity = item["id"]
    if provider == "modelscope":
        url = "https://www.modelscope.cn/api/v1/models/" + quote(identity, safe="/") + "/repo/files?Revision=master&Recursive=true"
        data, _ = fetch(url, output)
        if data.get("Code") != 200:
            raise ValueError("Invalid ModelScope manifest: " + identity)
        files = [{"path": f["Path"], "bytes": f.get("Size", 0), "sha256": f.get("Sha256"), "revision": f.get("Revision")}
                 for f in data["Data"]["Files"] if f.get("Type", "").lower() == "blob"]
    else:
        files = [{"path": f["rfilename"]} for f in item["siblings"]]
    paths = [f["path"] for f in files]
    graphs = [p for p in paths if p.lower().endswith(".mnn")]
    weights = [p for p in paths if p.lower().endswith(".mnn.weight")]
    def mnn_label(value):
        return re.search(r"(?<![A-Za-z])mnn(?![A-Za-z])", value, re.I) or re.search(r"(?<![A-Za-z])MNN", value)
    labeled = mnn_label(identity) or any(t.lower() in ("mnn", "library:mnn") for t in item.get("tags", []))
    archives = [p for p in paths if p.lower().endswith(ARCHIVES) and (labeled or mnn_label(p))]
    # Card: MNN means Multi-Head Attention Network; its archive is Flickr8k text.
    # Actual Alibaba MNN artifacts override this previously reviewed false positive.
    if identity.lower() == "ritzz01/cnn-mnn-model" and not graphs and not weights:
        return None
    official = identity.lower().startswith("mnn/" if provider == "modelscope" else "taobao-mnn/")
    if not graphs and not weights and not archives and not official:
        return None
    # Hints only: checking filenames does not verify operators, decoder semantics or model quality.
    language_roots = [str(Path(p).parent) for p in paths if Path(p).name == "llm_config.json"
                      and str(Path(p).with_name("tokenizer.txt")) in paths]
    evidence = "graph" if graphs else "archive-uninspected" if archives else "auxiliary-or-empty"
    return {"provider": provider, "repository": identity, "revision": item.get("sha", "master"),
            "url": ("https://modelscope.cn/models/" if provider == "modelscope" else "https://huggingface.co/") + identity,
            "official": official, "evidence": evidence, "license": item.get("license"),
            "tags": item.get("tags", []), "tasks": item.get("tasks", [item["pipeline_tag"]] if item.get("pipeline_tag") else []),
            "graphs": graphs, "weights": weights, "archives": archives, "language_roots": language_roots,
            "files": files, "inference_verified": False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=Path("TestArtifacts~/ModelDiscovery/LiveAudit"))
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[4]
    output = args.output.resolve()
    if not output.is_relative_to(root / "TestArtifacts~"):
        parser.error("Audit outputs must stay inside this project's TestArtifacts~/")
    (output / "responses").mkdir(parents=True, exist_ok=True)
    candidates, searches, failures, unavailable = {}, [], [], []
    for provider, queries in QUERIES.items():
        for query in queries:
            try:
                items, details = search(provider, query, output)
                searches.append(details)
                for item in items:
                    candidates[provider, item["id"]] = item
                print(json.dumps(details), flush=True)
            except Exception as error:
                failures.append({"provider": provider, "query": query, "error": str(error)})
    records = []
    with ThreadPoolExecutor(max_workers=4) as pool:
        jobs = {pool.submit(inspect, provider, item, output): (provider, identity)
                for (provider, identity), item in candidates.items()}
        for index, job in enumerate(as_completed(jobs), 1):
            provider, identity = jobs[job]
            try:
                record = job.result()
                if record:
                    records.append(record)
            except Exception as error:
                report = {"provider": provider, "repository": identity, "error": str(error)}
                if isinstance(error, HTTPError) and error.code in (401, 403, 404, 410):
                    unavailable.append(report)
                else:
                    failures.append(report)
            if index % 100 == 0:
                print("Inspected", index, "of", len(jobs), flush=True)
    records.sort(key=lambda r: (r["provider"], r["repository"].lower()))
    summary = {"captured_at_utc": datetime.now(timezone.utc).isoformat(), "searches": searches,
               "candidates": len(candidates), "included": len(records),
               "counts": dict(Counter(r["provider"] + ":" + r["evidence"] for r in records)),
               "excluded_non_mnn": len(candidates) - len(records) - len(unavailable) - sum("repository" in f for f in failures),
               "unavailable": unavailable, "failures": failures, "inference_verified": False,
               "coverage_limit": "Public indexed repositories returned by official owner, MNN format tag and MNN name search. Untagged repositories without MNN in the name, private/gated content and archive contents are not exhaustively inspected."}
    (output / "catalog.json").write_text(json.dumps(records, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (output / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (output / "repositories.tsv").write_text("provider\trepository\tevidence\tgraphs\tlanguage_roots\n" + "".join(
        f'{r["provider"]}\t{r["repository"]}\t{r["evidence"]}\t{len(r["graphs"])}\t{";".join(r["language_roots"])}\n' for r in records), encoding="utf-8")
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
