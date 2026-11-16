"""Keep reproducible conversion inputs, reports and dependencies in an explicit workspace."""
import os
from pathlib import Path


def workspace():
    project = Path(os.environ.get('MNN_PROJECT_ROOT', Path(__file__).resolve().parents[5])).resolve()
    value = os.environ.get('MNN_TURN_WORK_ROOT')
    if not value:
        raise ValueError('Set MNN_TURN_WORK_ROOT to an existing project TestArtifacts~/ workspace')
    work = Path(value).resolve()
    if not work.is_dir() or not work.is_relative_to(project / 'TestArtifacts~'):
        raise ValueError('MNN_TURN_WORK_ROOT must be an existing directory inside project TestArtifacts~/')
    return work, project
