#!/bin/bash
set -euo pipefail
# Paired measurements use symlinks to existing model assets, with no second download.
project_root="$(cd "$(dirname "$0")/../../../../.." && pwd)"
cv_models="${MNN_TEST_COSYVOICE300M_ROOT:-$project_root/Assets/StreamingAssets/MNN/Models/CosyVoice-300M-MNN}"
cv_output="${MNN_TEST_ARTIFACT_ROOT:-$project_root/TestArtifacts~/CosyVoice300M/UnityOptimization/Final}"
cv_reference="$project_root/TestArtifacts~/CosyVoice300M/UnityOptimization/ReferenceBaseline"
python3 - "$project_root" "$cv_models" "$cv_reference" <<'PY'
from pathlib import Path
import json
import sys
root, models, reference = (Path(arg).resolve() for arg in sys.argv[1:])
assert root / 'TestArtifacts~' in reference.parents
assert (models / 'fp16/llm_decode_compact.mnn.weight').is_file()
reference.mkdir(parents=True, exist_ok=True)
for name in ['common']:
    target = reference / name
    if target.is_symlink():
        assert target.resolve() == models / name
    elif target.exists():
        raise SystemExit('Refusing to replace existing reference fixture: ' + str(target))
    else:
        target.symlink_to(models / name)
config = json.loads((models / 'mnn_cosyvoice300m.json').read_text())
config.pop('decoder_graph', None)
(reference / 'mnn_cosyvoice300m.json').write_text(json.dumps(config, indent=2) + '\n')
(reference / 'fp16').mkdir(exist_ok=True)
for stage in ['text_encoder', 'llm_cached_projected', 'flow_encoder', 'flow_regulator', 'flow_estimator', 'hift_f0', 'hift_core']:
    for suffix in ['.mnn', '.mnn.weight']:
        target = reference / 'fp16' / (stage + suffix)
        source = models / 'fp16' / target.name
        if target.is_symlink():
            assert target.resolve() == source
        elif target.exists():
            raise SystemExit('Refusing to replace reference asset: ' + str(target))
        else:
            target.symlink_to(source)
PY
MNN_TEST_COSYVOICE300M_REFERENCE_ROOT="$cv_reference" MNN_TEST_COSYVOICE300M_ROOT="$cv_models" \
MNN_TEST_ARTIFACT_ROOT="$cv_output" \
MNN_COSYVOICE300M_TEST_FILTER='CosyVoice300MUnitTests;CosyVoice300MIntegrationTests;CosyVoice300MOptimizationTests;CosyVoiceUnitTests;StudioTests;MNNSessionConfigTests;MNNGenerationGraphTests' \
bash "$project_root/Packages/com.eitan.mnn/Tools~/Models/CosyVoice300M/run_unity_tests.sh"
