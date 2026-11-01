#!/usr/bin/env python3
"""Run the official, unmodified llm_bench with reproducible workload and identities.

Uses existing weights and binaries only. Measures synthetic throughput, not answer
quality or Unity overhead. Pair results with real managed/Player correctness tests.
Android uses already staged model assets; only the runner and library are copied.
"""
import argparse
import hashlib
import itertools
import json
import os
from pathlib import Path
import re
import shlex
import subprocess
import uuid


def digest(path):
    result = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            result.update(block)
    return result.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--runner", type=Path, required=True, help="Existing official llm_bench executable.")
    parser.add_argument("--library-directory", type=Path, required=True, help="Contains the exact libMNN.dylib or libMNN.so to measure.")
    parser.add_argument("--model", required=True, help="Existing config.json; on the device when --adb-serial is set.")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--threads", default="2,4")
    parser.add_argument("--backends", default="0", help="Official backend IDs: CPU=0, Metal=1, OpenCL=3, Vulkan=7.")
    parser.add_argument("--precision", default="0,2")
    parser.add_argument("--power", default="0,1")
    parser.add_argument("--prompt-tokens", type=int, default=128)
    parser.add_argument("--decode-tokens", type=int, default=64)
    parser.add_argument("--repeats", type=int, default=5, help="Measured repetitions, excluding the official runner's warmup.")
    parser.add_argument("--timeout", type=int, default=1800)
    parser.add_argument("--adb-serial", help="Explicit connected device; stage only the supplied runner/library, never weights.")
    args = parser.parse_args()
    project = Path(__file__).resolve().parents[4]
    output = args.output.resolve()
    if (project / "TestArtifacts~").resolve() not in output.parents:
        parser.error("--output must be under this project's TestArtifacts~/.")
    if output.exists() and any(output.iterdir()):
        parser.error("Use a fresh --output directory so prior results cannot be accepted.")
    for name, allowed in [("threads", range(1, 65)), ("backends", (0, 1, 3, 7)), ("precision", (0, 1, 2)), ("power", (0, 1, 2))]:
        value = getattr(args, name)
        if not re.fullmatch(r"\d+(,\d+)*", value) or any(int(item) not in allowed for item in value.split(",")):
            parser.error("Invalid --" + name)
    if min(args.prompt_tokens, args.decode_tokens, args.repeats, args.timeout) < 1:
        parser.error("Workload sizes, repeats and timeout must be positive.")
    runner = args.runner.resolve()
    library = args.library_directory.resolve() / ("libMNN.so" if args.adb_serial else "libMNN.dylib")
    if not runner.is_file() or not library.is_file():
        parser.error("The runner and matching MNN library must already exist.")
    if not args.adb_serial and not Path(args.model).is_file():
        parser.error("The local model config must already exist.")
    output.mkdir(parents=True, exist_ok=True)
    benchmark = ["-m", args.model if args.adb_serial else str(Path(args.model).resolve()),
                 "-pg", str(args.prompt_tokens) + "," + str(args.decode_tokens), "-p", "0", "-n", "0",
                 "-rep", str(args.repeats), "-load", "false"]
    backend_names = {0: "cpu", 1: "metal", 3: "opencl", 7: "vulkan"}
    matrix = list(itertools.product(*(map(int, getattr(args, name).split(","))
                                     for name in ("threads", "backends", "precision", "power"))))
    report = {"kind": "official_synthetic_llm_throughput", "answer_correctness_verified": False,
              "unity_inference_verified": False, "runner_sha256": digest(runner),
              "library_sha256": digest(library), "arguments": benchmark, "matrix": matrix,
              "commands": [], "passed": False,
              "note": "llm_bench adjusts dynamic_option bit 3 by prompt length; compare identical workloads. Native throughput excludes some host work."}
    remote = "/data/local/tmp/mnn-unity-perf-" + uuid.uuid4().hex
    adb = ["adb", "-s", args.adb_serial] if args.adb_serial else None
    try:
        if adb:
            subprocess.run(adb + ["shell", "mkdir", "-p", remote], check=True, timeout=30)
            subprocess.run(adb + ["push", str(runner), remote + "/llm_bench"], check=True, timeout=60)
            subprocess.run(adb + ["push", str(library), remote + "/libMNN.so"], check=True, timeout=60)
            identity = subprocess.check_output(adb + ["shell", "getprop ro.product.model; getprop ro.build.version.release; sha256sum " + shlex.quote(args.model)], text=True, timeout=30)
            report["device_and_config_identity"] = identity.strip()
            env = None
        else:
            report["config_sha256"] = digest(Path(args.model))
            env = dict(os.environ, DYLD_LIBRARY_PATH=str(library.parent))
        reports = []
        # Upstream JSON labels all rows with the first configuration. One process
        # per configuration preserves identities and avoids accepting stale rows.
        for index, (threads, backend, precision, power) in enumerate(matrix):
            case = output / ("case-%03d" % index)
            case.mkdir()
            options = benchmark + ["-t", str(threads), "-a", backend_names[backend],
                                   "-c", str(precision), "--power", str(power)]
            if adb:
                words = [remote + "/llm_bench"] + options + ["-j", remote + "/bench.json"]
                command = adb + ["shell", "cd " + shlex.quote(remote) + " && LD_LIBRARY_PATH=" + shlex.quote(remote) + " " + shlex.join(words)]
            else:
                command = [str(runner)] + options + ["-j", str(case / "bench.json")]
            report["commands"].append(command)
            with (case / "benchmark.log").open("w") as log:
                result = subprocess.run(command, cwd=case, env=env, stdout=log, stderr=subprocess.STDOUT, timeout=args.timeout)
            if result.returncode:
                raise RuntimeError("Official benchmark failed in " + case.name + "; exit code " + str(result.returncode))
            log = (case / "benchmark.log").read_text(errors="replace")
            if re.search(r"Init Metal Error|Can't Find type=\d+ backend, use 0 instead|Init Vulkan Error", log):
                raise RuntimeError("Requested accelerator was unavailable in " + case.name)
            if adb:
                subprocess.run(adb + ["pull", remote + "/bench.json", str(case / "bench.json")], check=True, timeout=30)
            entry = json.loads((case / "bench.json").read_text())
            if any(entry.get(key) != value for key, value in
                   (("threads", threads), ("backend", backend_names[backend].upper()),
                    ("precision", precision), ("power", power))):
                raise RuntimeError("Benchmark configuration mismatch in " + case.name)
            rows = entry.get("results", [])
            if len(rows) != 1 or rows[0].get("prompt_len") != args.prompt_tokens or rows[0].get("generate_len") != args.decode_tokens:
                raise RuntimeError("Benchmark did not execute the requested shared-KV workload.")
            if min(rows[0].get("prefill_tps", 0), rows[0].get("decode_tps", 0)) <= 0:
                raise RuntimeError("Invalid throughput sample.")
            reports.append(entry)
            (output / "bench.json").write_text(json.dumps(reports, indent=2) + "\n")
        report["passed"] = True
    except (subprocess.SubprocessError, OSError, ValueError, RuntimeError) as error:
        report["error"] = str(error)
    finally:
        if adb:
            subprocess.run(adb + ["shell", "rm", "-rf", remote], timeout=30, check=False)
        (output / "identity.json").write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps(report, indent=2))
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
