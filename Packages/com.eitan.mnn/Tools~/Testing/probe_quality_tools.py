#!/usr/bin/env python3
"""Report provisioned quality tools; never infer coverage or metric passes from presence.

Use --require to block automation until the named tools have been provisioned.
No installation, dependency download, tool execution or code instrumentation occurs.
"""

import argparse
import json
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[4]
COMMANDS = ("crapper", "dryer", "mutator", "dotnet-stryker", "coverlet", "ReportGenerator", "jscpd", "lizard")


def inventory(root, required, lookup=shutil.which):
    tools = {}
    for name in COMMANDS:
        path = lookup(name)
        tools[name] = {"available": path is not None, "path": path, "executed": False}
    coverage = sorted((Path(root) / "Library/PackageCache").glob("com.unity.testtools.codecoverage@*"))
    tools["unity-coverage"] = {"available": bool(coverage), "path": str(coverage[0]) if coverage else None, "executed": False}
    missing = [name for name in required if not tools[name]["available"]]
    return {"kind": "quality_tool_preflight", "ready": not missing, "required": list(required), "missing_required": missing,
            "tools": tools, "quality_metrics_verified": False,
            "limitation": "Presence does not verify CRAP, duplication rank, coverage or mutation results. No quality tool was executed."}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--require", nargs="+", choices=COMMANDS + ("unity-coverage",), default=[])
    parser.add_argument("--output", type=Path, default=ROOT / "TestArtifacts~/QualityTools/inventory.json")
    args = parser.parse_args()
    output = args.output.resolve()
    if not output.is_relative_to(ROOT / "TestArtifacts~"):
        parser.error("output must be under this repository's TestArtifacts~/")
    report = inventory(ROOT, args.require)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))
    return 0 if report["ready"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
