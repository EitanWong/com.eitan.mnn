#!/usr/bin/env python3
"""Publish the prepared speech model folders to ModelScope.

The script intentionally uploads only deployable model files and metadata. It
never reads or stores a token from a project file; use MODELSCOPE_API_TOKEN and
MNN_MODELSCOPE_NAMESPACE in the environment.
"""

from __future__ import annotations

import argparse
import fnmatch
import hashlib
import json
import os
from pathlib import PurePosixPath
import re
import subprocess
import sys
from pathlib import Path


MODEL_NAMES = (
    "Silero-v5.1-MNN",
    "SmartTurn-v3.2-FP32-MNN",
    "MarbleNet-VAD-MNN",
    "DTLN-aec-128-MNN",
    "DTLN-aec-256-MNN",
    "DTLN-aec-512-MNN",
    "DTLN-MNN",
    "GTCRN-MNN",
    "FullSubNet-MNN",
    "FullSubNetPlus-MNN",
    "MossFormerGAN-SE-16K-MNN",
    "MossFormer2-SE-48K-MNN",
    "ERes2NetV2-MNN",
    "CAMPlus-MNN",
    "ECAPA-TDNN-MNN",
    "TitaNet-MNN",
    "FSMN-VAD-16K-MNN",
    "FRCRN-SE-16K-MNN",
    "DeepFilterNet3-MNN",
    "WeSpeaker-ResNet34-LM-MNN",
    "LiveKit-Turn-Detector-MNN",
    "MossFormer2-SS-16K-MNN",
    "SepFormer-WSJ02Mix-MNN",
    "ConvTasNet-Libri2Mix-8K-MNN",
    "DPRNN-WHAM-8K-MNN",
    "DPTNet-Libri1Mix-16K-MNN",
    "Sortformer-4spk-v2.1-MNN",
    "NAMO-Turn-Detector-v1-Multilingual-MNN",
    "SudoRMRF-U16-WSJ02Mix-8K-MNN",
    "DCCRNet-Libri1Mix-16K-MNN",
    "DCUNet-Libri1Mix-16K-MNN",
    "SepReformer-B-WSJ02Mix-8K-MNN",
    "SpExPlus-WSJ02Mix-8K-MNN",
    "SEG-YGD-16K-MNN",
    "Resemble-Denoiser-44K-MNN",
    "TIGER-Speech-16K-MNN",
    "DCCRN-Mayavoz-VCTK-16K-MNN",
    "AV-ConvTasNet-LRS2-16K-MNN",
    "AV-DPRNN-VoxCeleb2-16K-MNN",
    "AV-MossFormer2-TSE-16K-MNN",
    "VoxTurn-v1-MNN",
    "Resemble-Enhance-44K-MNN",
    "LS-EEND-Simu-8K-MNN",
    "CueLight-Nano-MNN",
    "CueLight-Nano-SSM-MNN",
    "FS-EEND-Simu-8K-MNN",
    "FS-EEND-CALLHOME-8K-MNN",
    "VBx-ResNet101-16K-MNN",
    "VBx-ResNet101-8K-MNN",
    "TurnSense-SmolLM2-135M-MNN",
)
RUNTIME_BINARY_FILES = frozenset((
    "fs_eend_coefficients.bin",
    "vbx_coefficients.bin",
    "ls_eend_coefficients.bin",
    "cuelight_coefficients.bin",
    "embeddings_bf16.bin", "mel-window.bin", "mel-filters.bin",
    "rotary-cos.bin", "rotary-sin.bin", "head-weight.bin", "head-bias.bin",
))
EXCLUDES = (
    "*.meta",
    ".DS_Store",
    ".mnn-modelscope-installed",
    ".siriai-install.json",
)


def _ignored(path: Path) -> bool:
    return any(fnmatch.fnmatch(path.name, pattern) for pattern in EXCLUDES)


def _digest(path: Path) -> str:
    with path.open("rb") as source:
        return hashlib.file_digest(source, "sha256").hexdigest()


def _read_json(path: Path, errors: list[str]) -> dict:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
        if isinstance(value, dict):
            return value
    except (OSError, ValueError) as error:
        errors.append(f"cannot read {path.name}: {error}")
        return {}
    errors.append(f"{path.name} must be a JSON object")
    return {}


def _safe_path(value: object) -> bool:
    return (isinstance(value, str) and bool(value) and "\\" not in value and
            not PurePosixPath(value).is_absolute() and
            all(part not in ("", ".", "..") for part in value.split("/")))


def _audit_manifest(directory: Path, manifest: dict, errors: list[str]) -> dict[str, str]:
    entries = manifest.get("files")
    if not isinstance(entries, list):
        errors.append("manifest files must be an array")
        return {}
    declared = {}
    for entry in entries:
        if not isinstance(entry, dict) or not _safe_path(entry.get("path")):
            errors.append("unsafe manifest file path")
            continue
        name = entry["path"]
        if name in declared:
            errors.append(f"duplicate manifest entry: {name}")
        declared[name] = entry.get("sha256")
        path = directory / name
        if any(parent.is_symlink() for parent in path.parents if parent != directory and directory in parent.parents):
            errors.append(f"unsafe symlink parent: {name}")
            continue
        if path.is_symlink():
            errors.append(f"symlink: {name}")
            continue
        if not path.is_file():
            errors.append(f"missing manifest file: {name}")
            continue
        if path.stat().st_size != entry.get("bytes"):
            errors.append(f"byte count mismatch: {name}")
        if _digest(path) != entry.get("sha256"):
            errors.append(f"hash mismatch: {name}")
    return declared


def _audit_player_coverage(player: dict, field: str, deployed: dict[str, str], label: str, errors: list[str]) -> None:
    rows = player.get(field)
    if not isinstance(rows, list) or any(not isinstance(row, dict) for row in rows):
        errors.append(f"invalid Player {label} coverage")
        return
    covered = {row.get("path"): row.get("sha256") for row in rows if isinstance(row.get("path"), str)}
    if len(covered) != len(rows) or set(covered) != set(deployed):
        errors.append(f"Player {label} coverage must equal all deployed {label}s")
    for name, checksum in deployed.items():
        if covered.get(name) != checksum:
            errors.append(f"Player {label} hash mismatch: {name}")


def _audit_player(directory: Path, deployed: dict[str, str], runtime: dict[str, str], errors: list[str]) -> None:
    optimization = _read_json(directory / "mnn_optimization.json", errors)
    player = optimization.get("player_validation")
    if not isinstance(player, dict):
        errors.append("missing built Player source-parity evidence")
        return
    for field in ("platform", "unity", "build_id", "result_xml_sha256"):
        if not isinstance(player.get(field), str) or not player[field]:
            errors.append(f"missing Player evidence field: {field}")
    if not re.fullmatch(r"[0-9a-f]{64}", str(player.get("result_xml_sha256", ""))):
        errors.append("invalid Player result XML hash")
    _audit_player_coverage(player, "graphs", deployed, "graph", errors)
    _audit_player_coverage(player, "runtime_files", runtime, "runtime file", errors)


def audit_model(directory: Path) -> list[str]:
    """Reject dirty/stale packages and absent recorded Player parity before upload.

    These checks validate evidence references; they do not re-run inference or
    independently authenticate the recorded test report.
    """
    errors = []
    if directory.is_symlink() or not directory.is_dir():
        return ["model directory is missing or a symlink"]
    if not directory.name.endswith("-MNN"):
        errors.append("model name must end with -MNN")
    manifest = _read_json(directory / "manifest.json", errors)
    if manifest.get("name") != directory.name or manifest.get("format") != "mnn":
        errors.append("manifest name/format mismatch")
    declared = _audit_manifest(directory, manifest, errors)
    actual = set()
    for path in directory.rglob("*"):
        name = path.relative_to(directory).as_posix()
        if path.is_symlink():
            errors.append(f"symlink: {name}")
        elif path.is_file() and not _ignored(path) and name != "manifest.json":
            actual.add(name)
            if path.name != "LICENSE" and path.suffix.lower() not in (".mnn", ".json", ".md", ".txt") and not name.endswith(".mnn.weight") and name not in RUNTIME_BINARY_FILES:
                errors.append(f"non-deployment file: {name}")
    for name in sorted(actual - set(declared)):
        errors.append(f"unlisted deployment file: {name}")
    for name in ("README.md", "LICENSE", "mnn_optimization.json"):
        if name not in actual:
            errors.append(f"missing required file: {name}")
    graphs = {name: checksum for name, checksum in declared.items() if name.endswith(".mnn") and name in actual}
    weights = {name: checksum for name, checksum in declared.items() if name.endswith(".mnn.weight") and name in actual}
    for name in weights:
        if name[:-7] not in graphs:
            errors.append(f"orphan external MNN weights: {name}")
    if not graphs:
        errors.append("missing MNN graphs")
    for key, value in manifest.items():
        if key.startswith("preferred_graph") and value not in graphs:
            errors.append(f"{key} does not name a deployed graph")
    runtime = {name: checksum for name, checksum in declared.items()
               if name in actual and name not in ("manifest.json", "mnn_optimization.json") and
               (name.endswith((".mnn", ".mnn.weight", ".json", ".txt")) or name in RUNTIME_BINARY_FILES)}
    _audit_player(directory, {**graphs, **weights}, runtime, errors)
    return errors


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--models-root",
        type=Path,
        default=Path("Assets/StreamingAssets/MNN/Models"),
        help="Unity model directory containing the prepared profiles.",
    )
    parser.add_argument(
        "--namespace",
        default=os.environ.get("MNN_MODELSCOPE_NAMESPACE"),
        help="ModelScope namespace; defaults to MNN_MODELSCOPE_NAMESPACE.",
    )
    parser.add_argument(
        "--model",
        action="append",
        dest="models",
        choices=MODEL_NAMES,
        help="Publish only this model; repeat the option to publish several.",
    )
    parser.add_argument("--dry-run", action="store_true", help="Print commands without uploading.")
    parser.add_argument("--audit-only", action="store_true", help="Check deployment/evidence without credentials or upload.")
    args = parser.parse_args()

    root = args.models_root.resolve()
    if not root.is_dir():
        parser.error(f"model root does not exist: {root}")
    if not args.audit_only and not args.namespace:
        parser.error("set MNN_MODELSCOPE_NAMESPACE to the ModelScope owner namespace")
    if not args.audit_only and not args.dry_run and not os.environ.get("MODELSCOPE_API_TOKEN"):
        parser.error("set MODELSCOPE_API_TOKEN before uploading")

    selected = args.models or list(MODEL_NAMES)
    missing = [name for name in selected if not (root / name).is_dir()]
    if missing:
        parser.error("missing model directories: " + ", ".join(missing))

    rejected = False
    for name in selected:
        errors = audit_model(root / name)
        print(f"{name}: " + ("preflight passed" if not errors else "publication blocked"))
        for error in errors:
            print("  " + error, file=sys.stderr)
        rejected |= bool(errors)
    if rejected:
        return 2
    if args.audit_only:
        return 0

    for name in selected:
        command = [
            "modelscope",
            "upload",
            f"{args.namespace}/{name}",
            str(root / name),
            "--repo-type",
            "model",
            "--commit-message",
            f"Publish {name} MNN deployment package",
            "--disable-tqdm",
        ]
        for pattern in EXCLUDES:
            command.extend(("--exclude", pattern))
        print("$ " + " ".join(repr(part) for part in command))
        if args.dry_run:
            continue
        completed = subprocess.run(command, check=False)
        if completed.returncode != 0:
            print(f"upload failed for {name} with exit code {completed.returncode}", file=sys.stderr)
            return completed.returncode
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
