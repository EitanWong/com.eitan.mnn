"""One package/repository location contract for standalone Harness tooling."""
from pathlib import Path

PACKAGE_ROOT = Path(__file__).resolve().parents[2]
PROJECT_ROOT = PACKAGE_ROOT.parents[1]
