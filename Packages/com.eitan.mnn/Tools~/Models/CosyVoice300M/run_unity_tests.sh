#!/bin/bash
set -euo pipefail
# Reuse a local isolated Unity project; never launch the user's development project.
project_root="$(cd "$(dirname "$0")/../../../../.." && pwd)"
cv_unity="${MNN_UNITY_EDITOR:-/Applications/Unity/Hub/Editor/2021.3.45f2/Unity.app/Contents/MacOS/Unity}"
cv_project="${MNN_COSYVOICE300M_TEST_PROJECT:-$project_root/TestArtifacts~/CosyVoice/UnityProject}"
cv_output="${MNN_TEST_ARTIFACT_ROOT:-$project_root/TestArtifacts~/CosyVoice300M/PackageIntegration}"
cv_models="${MNN_TEST_COSYVOICE300M_ROOT:-$project_root/Assets/StreamingAssets/MNN/Models/CosyVoice-300M-MNN}"
python3 - "$project_root" "$cv_project" "$cv_output" <<'PY'
from pathlib import Path
import sys
root, project, output = (Path(p).resolve() for p in sys.argv[1:])
for path in [project, output]:
    if root / 'TestArtifacts~' not in path.parents:
        raise SystemExit('Test project and evidence must stay under TestArtifacts~/')
if not (project / 'ProjectSettings').is_dir():
    raise SystemExit('Set MNN_COSYVOICE300M_TEST_PROJECT to an existing isolated Unity test project.')
output.mkdir(parents=True, exist_ok=True)
PY
rsync -a --delete "$project_root/Packages/com.eitan.mnn/" "$cv_project/Packages/com.eitan.mnn/"
MNN_TEST_COSYVOICE300M_ROOT="$cv_models" MNN_TEST_ARTIFACT_ROOT="$cv_output" \
"$cv_unity" -batchmode -projectPath "$cv_project" -runTests -testPlatform editmode \
 -testFilter "${MNN_COSYVOICE300M_TEST_FILTER:-CosyVoice300MUnitTests;CosyVoice300MIntegrationTests;CosyVoiceUnitTests;StudioTests;MNNSessionConfigTests;MNNGenerationGraphTests}" \
 -testResults "$cv_output/unity-tests.xml" -logFile "$cv_output/unity-tests.log"
python3 - "$cv_output/unity-tests.xml" <<'PY'
import sys
import xml.etree.ElementTree as ET
root = ET.parse(sys.argv[1]).getroot()
print(root.attrib)
if root.get('result') != 'Passed' or int(root.get('total', '0')) < 1 or int(root.get('failed', '0')) or int(root.get('skipped', '0')):
    raise SystemExit('Unity checks did not all pass.')
PY
