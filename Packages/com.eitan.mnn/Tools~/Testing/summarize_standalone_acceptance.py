#!/usr/bin/env python3
"""Summarize completed standalone measurements without inventing performance targets.

Uses nearest-rank percentiles, keeps simulated rendering separate from real
model latency, and never copies transcript text into the report.
"""

import argparse
import csv
import hashlib
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
LATENCIES = ("first_token", "first_pcm", "first_nonzero_render", "presentation", "total")


def finite(value):
    number = float(value)
    if not math.isfinite(number) or number < 0:
        raise ValueError("Measurements must be finite and nonnegative.")
    return number


def statistics(values):
    ordered = sorted(values)
    return {"samples": len(ordered),
            **{name: ordered[max(0, math.ceil(len(ordered) * percentile) - 1)]
               for name, percentile in (("p50", .5), ("p95", .95), ("p99", .99))},
            "maximum": ordered[-1]}


def read_turns(path):
    if path.stat().st_size > 256 * 1024 * 1024:
        raise ValueError("Oversized measurements file.")
    rows, startups = [], []
    previous_session = 0
    with path.open(newline="") as stream:
        for record in csv.DictReader(stream):
            turn, session = int(record["turn"]), int(record["session"])
            if turn != len(rows) + 1 or session not in (previous_session, previous_session + 1) or session < 1:
                raise ValueError("Missing, duplicate or out-of-order turn/session.")
            numbers = {key: finite(record[key]) for key in (
                "startup_ms", "render_hop_p95_ms", "render_hop_p99_ms", "rss_bytes", "managed_bytes", "unity_tracked_bytes",
                *(name + "_ms" for name in LATENCIES))}
            if session != previous_session:
                startups.append(numbers["startup_ms"])
            elif numbers["startup_ms"] != startups[-1]:
                raise ValueError("Inconsistent startup duration within one session.")
            previous_session = session
            rows.append(numbers)
    return rows, startups


def summarize(directory):
    directory = Path(directory)
    summary_path, csv_path = directory / "summary.json", directory / "turns.csv"
    summary = json.loads(summary_path.read_text())
    if summary.get("status") != "passed":
        raise ValueError("A completed passing measurement run is required.")
    rows, startups = read_turns(csv_path)
    if not rows or len(rows) != summary["turns"] or len(startups) != summary["sessions"]:
        raise ValueError("Measurement rows and source counters disagree.")
    elapsed, requested = finite(summary["elapsedSeconds"]), finite(summary["requestedSeconds"])
    if elapsed < requested:
        raise ValueError("The requested measurement duration did not complete.")
    latencies = {name: statistics([row[name + "_ms"] for row in rows]) for name in LATENCIES}
    latencies["startup"] = statistics(startups)
    return {
        "kind": "standalone_measured_latency_distribution", "sessions": len(startups), "turns": len(rows),
        "elapsed_seconds": elapsed, "requested_seconds": requested,
        "percentile_method": "nearest rank over completed turns; startup counted once per session",
        "latency_ms": latencies,
        "simulated_render_ms": {"maximum_session_p95": max(row["render_hop_p95_ms"] for row in rows),
                                "maximum_session_p99": max(row["render_hop_p99_ms"] for row in rows),
                                "scope": "Maxima of the recorded session-window render percentiles; not a global callback distribution or native inference hop."},
        "memory": {"maximum_turn_rss_bytes": max(row["rss_bytes"] for row in rows),
                   "maximum_turn_managed_bytes": max(row["managed_bytes"] for row in rows),
                   "maximum_turn_unity_tracked_bytes": max(row["unity_tracked_bytes"] for row in rows),
                   "scope": "Turn-boundary process observations; sampled process peaks remain in summary.json. Native-only and GPU peaks are unavailable."},
        "scope": summary.get("validationScope", "Real local text/TTS; simulated output presentation; ASR preparation only."),
        "performance_targets_verified": False,
        "limitation": "A completed functional measurement run does not establish latency, RSS, native/GPU memory, interrupt or throughput targets.",
        "source_sha256": {path.name: hashlib.sha256(path.read_bytes()).hexdigest() for path in (summary_path, csv_path)},
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path, help="Completed run containing summary.json and turns.csv")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    directory = args.directory.resolve()
    output = args.output.resolve() if args.output else directory / "latency-distribution.json"
    artifact_root = ROOT / "TestArtifacts~"
    if artifact_root not in directory.parents or artifact_root not in output.parents:
        parser.error("Inputs and output must be within this repository's TestArtifacts~/.")
    if output in (directory / "summary.json", directory / "turns.csv"):
        parser.error("Do not overwrite measurement sources.")
    try:
        report = summarize(directory)
    except (OSError, ValueError, KeyError, csv.Error) as error:
        parser.error(str(error))
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps(report, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
