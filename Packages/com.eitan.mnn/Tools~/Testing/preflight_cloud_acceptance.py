#!/usr/bin/env python3
"""Validate cloud acceptance inputs and credential presence without making requests.

Read a nonsecret JSON plan, never credential files. A ready preflight does not
validate model access, service responses, performance or billing authorization.
"""

import argparse
import ipaddress
import json
import math
import os
from pathlib import Path
import re
from urllib.parse import urlsplit
import wave

ROOT = Path(__file__).resolve().parents[4]
TASKS = {"OpenAIChat": "chat", "OpenAIRecognition": "recognition", "OpenAISpeech": "synthesis",
         "DashScopeRecognition": "recognition", "DashScopeSpeech": "synthesis",
         "VolcengineRecognition": "recognition", "VolcengineSpeech": "synthesis", "MiniMaxSpeech": "synthesis"}
FIELDS = frozenset(("id", "task", "protocol", "endpoint", "model", "credential_env", "timeout_seconds",
                    "maximum_latency_seconds", "prompt", "expected_text", "audio", "text", "voice",
                    "application_id_env", "resource_id_env"))


def bounded_text(value, maximum):
    return isinstance(value, str) and bool(value.strip()) and len(value) <= maximum


def valid_endpoint(value):
    if not isinstance(value, str):
        return False
    try:
        uri = urlsplit(value)
        loopback = uri.hostname == "localhost"
        if uri.hostname and not loopback:
            try:
                loopback = ipaddress.ip_address(uri.hostname).is_loopback
            except ValueError:
                pass
        return bool(uri.hostname) and (uri.scheme == "https" or uri.scheme == "http" and loopback) and not (
            uri.username or uri.password or uri.query or uri.fragment) and uri.port != 0
    except (TypeError, ValueError):
        return False


def inspect_audio(value, directory):
    if not bounded_text(value, 4096):
        raise ValueError("Recognition needs an existing PCM16 mono WAV reference.")
    path = (directory / value).resolve()
    if not path.is_file() or path.stat().st_size > 3 * 1024 * 1024:
        raise ValueError("Recognition reference is missing or exceeds 3 MiB.")
    try:
        with wave.open(str(path), "rb") as audio:
            frames, rate = audio.getnframes(), audio.getframerate()
            if audio.getnchannels() != 1 or audio.getsampwidth() != 2 or audio.getcomptype() != "NONE" or not (
                    8000 <= rate <= 48000 and 0 < frames <= rate * 30):
                raise ValueError("Recognition needs at most 30 seconds of PCM16 mono WAV at 8-48 kHz.")
            if len(audio.readframes(frames)) != frames * 2:
                raise ValueError("Recognition reference is truncated.")
            return {"path": str(path), "frames": frames, "sample_rate": rate}
    except (wave.Error, EOFError, OSError) as error:
        raise ValueError("Recognition reference is not a readable PCM16 WAV.") from error


def inspect_scenario(item, directory, environment, advertised_models):
    result = {"id": "invalid", "ready": False, "errors": [], "credentials": []}
    errors = result["errors"]
    if not isinstance(item, dict) or set(item) - FIELDS:
        errors.append("Use only documented nonsecret scenario fields; inline credentials are forbidden.")
        return result
    for field, value in item.items():
        if field not in ("timeout_seconds", "maximum_latency_seconds") and not isinstance(value, str):
            errors.append("All scenario identifiers and reference fields must be strings.")
    identity = item.get("id")
    if not isinstance(identity, str) or not re.fullmatch(r"[a-zA-Z0-9_-]{1,64}", identity):
        errors.append("Use a short scenario id containing letters, digits, underscores or hyphens.")
    else:
        result["id"] = identity
    task, protocol = item.get("task"), item.get("protocol")
    if not isinstance(protocol, str) or protocol not in TASKS or TASKS[protocol] != task:
        errors.append("Select a supported protocol matching the scenario task.")
    else:
        result.update(task=task, protocol=protocol)
    if not valid_endpoint(item.get("endpoint")):
        errors.append("Use an HTTPS base URL without credentials, query or fragment, or HTTP loopback.")
    model = item.get("model")
    if not bounded_text(model, 256) or isinstance(model, str) and model.startswith("REPLACE_"):
        errors.append("Select a bounded deployed model identifier.")
    elif advertised_models is not None and model not in advertised_models:
        errors.append("The selected model was not advertised by the supplied service model inventory.")
    timeout, latency = item.get("timeout_seconds"), item.get("maximum_latency_seconds")
    if (type(timeout) is not int or not 1 <= timeout <= 300 or type(latency) not in (int, float)
            or not math.isfinite(latency) or not 0 < latency <= timeout):
        errors.append("Require a 1-300 second timeout and positive latency budget no greater than that timeout.")
    for field in ("credential_env", "application_id_env", "resource_id_env"):
        if field != "credential_env" and field not in item:
            continue
        variable = item.get(field)
        if not isinstance(variable, str) or not re.fullmatch(r"[A-Z_][A-Z0-9_]{0,127}", variable):
            errors.append("Use a named environment variable for each credential or service identifier.")
            continue
        present = bool(environment.get(variable) and environment[variable].strip())
        result["credentials"].append({"source": "environment:" + variable, "present": present})
        if not present:
            errors.append("Required environment variable is absent or empty: " + variable)
    if task in ("chat", "recognition") and not bounded_text(item.get("expected_text"), 1024):
        errors.append("Provide expected_text from a deterministic reference for meaningful acceptance.")
    if task == "chat" and not bounded_text(item.get("prompt"), 4096):
        errors.append("Chat acceptance needs a bounded reference prompt.")
    if task == "recognition":
        try:
            result["audio"] = inspect_audio(item.get("audio"), directory)
        except ValueError as error:
            errors.append(str(error))
    if task == "synthesis" and (not bounded_text(item.get("text"), 1024) or not bounded_text(item.get("voice"), 1024)
                                or isinstance(item.get("voice"), str) and item["voice"].startswith("REPLACE_")):
        errors.append("Synthesis acceptance needs bounded text and a deployed voice identifier.")
    result["ready"] = not errors
    return result


def preflight(plan, directory, environment, advertised_models=None):
    report = {"kind": "cloud_acceptance_preflight", "ready": False, "acceptance_verified": False,
              "network_requests": 0, "scenarios": [], "errors": [],
              "advertised_models_checked": advertised_models is not None, "model_access_verified": False}
    if (not isinstance(plan, dict) or set(plan) != {"schema_version", "scenarios"}
            or plan["schema_version"] != 1 or not isinstance(plan["scenarios"], list)
            or not 1 <= len(plan["scenarios"]) <= 32):
        report["errors"].append("Use schema_version 1 and one to 32 nonsecret scenarios.")
        return report
    identities = set()
    for item in plan["scenarios"]:
        result = inspect_scenario(item, Path(directory), environment, advertised_models)
        if result["id"] in identities:
            result["errors"].append("Scenario ids must be unique.")
            result["ready"] = False
        identities.add(result["id"])
        report["scenarios"].append(result)
    report["ready"] = all(item["ready"] for item in report["scenarios"])
    return report


def read_advertised_models(path):
    if path is None:
        return None
    if path.stat().st_size > 1024 * 1024:
        raise ValueError("Model inventory too large")
    document = json.loads(path.read_text(encoding="utf-8"))
    rows = document.get("data") if isinstance(document, dict) else document
    if not isinstance(rows, list) or not rows:
        raise ValueError("Invalid model inventory")
    ids = [row.get("id") if isinstance(row, dict) else row for row in rows]
    if not all(bounded_text(identity, 256) for identity in ids):
        raise ValueError("Invalid model identifiers")
    return set(ids)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", type=Path, required=True, help="Nonsecret acceptance JSON; relative audio paths use its directory")
    parser.add_argument("--models", type=Path, help="Existing service model inventory: id list or OpenAI /models JSON; missing models block")
    parser.add_argument("--output", type=Path, default=ROOT / "TestArtifacts~/CloudAcceptance/preflight.json")
    args = parser.parse_args()
    output = args.output.resolve()
    if not output.is_relative_to(ROOT / "TestArtifacts~"):
        parser.error("output must be under this repository's TestArtifacts~/")
    try:
        if args.config.stat().st_size > 256 * 1024:
            raise ValueError("Oversized configuration")
        report = preflight(json.loads(args.config.read_text(encoding="utf-8")), args.config.resolve().parent, os.environ,
                           read_advertised_models(args.models))
    except (OSError, ValueError, RecursionError):
        report = {"ready": False, "acceptance_verified": False, "network_requests": 0,
                  "errors": ["Configuration is missing, oversized or invalid JSON."]}
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))
    return 0 if report["ready"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
