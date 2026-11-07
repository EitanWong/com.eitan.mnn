#!/bin/bash
set -euo pipefail
usage() {
    cat <<'USAGE'
Usage: build-linux.sh SOURCE [--output=TestArtifacts~/PATH] [--jobs=N] [--install|--no-install]

Build the pinned official MNN 3.6.2 Linux x86_64 CPU library using Docker.
SOURCE must be a clean checkout at 7e3c6fb5225d3ccf22807ee641332669df34650d.
Default: prepare and audit a candidate; install only with explicit --install.
All build files and logs stay under this Unity project's TestArtifacts~/.
No models, custom C++ sources, wrappers or C ABI exports are generated.
USAGE
}
if [[ $# -eq 0 || "$1" == --help || "$1" == -h ]]; then
    usage
    exit 0
fi
source_arg="$1"
shift
output_arg='TestArtifacts~/MNNValidation/OfficialNativeLinux'
install_plugin=false
jobs=6
for option in "$@"; do
    case "$option" in
        --output=*) output_arg="${option#--output=}" ;;
        --jobs=*) jobs="${option#--jobs=}" ;;
        --install) install_plugin=true ;;
        --no-install) install_plugin=false ;;
        --help|-h) usage; exit 0 ;;
        *) echo "Unknown option: $option" >&2; exit 2 ;;
    esac
done
if [[ ! "$jobs" =~ ^[1-9][0-9]*$ ]]; then
    echo '--jobs must be a positive integer.' >&2
    exit 2
fi
native_dir="$(cd "$(dirname "$0")" && pwd)"
package_dir="$(cd "$native_dir/.." && pwd)"
for dependency in python3 git docker; do
    command -v "$dependency" >/dev/null || { echo "Missing required command: $dependency" >&2; exit 2; }
done
# Resolve paths before creating output, starting Docker or changing the package.
path_record=$(python3 - "$package_dir" "$source_arg" "$output_arg" <<'PY'
from pathlib import Path
import re
import subprocess
import sys
package, source_arg, output_arg = sys.argv[1:]
package = Path(package).resolve()
project = next((parent for parent in package.parents
                if (parent / 'ProjectSettings/ProjectVersion.txt').is_file()
                and (parent / 'Packages/manifest.json').is_file()), None)
if project is None:
    raise SystemExit('Could not find the owning Unity project.')
source = Path(source_arg).resolve(strict=True)
output = Path(output_arg)
if not output.is_absolute():
    output = project / output
output = output.resolve()
artifacts = project / 'TestArtifacts~'
if artifacts not in output.parents:
    raise SystemExit('--output must be below the owning Unity project under TestArtifacts~/.')
if output == source or source in output.parents:
    raise SystemExit('--output must be outside the official source checkout.')
revision = subprocess.check_output(['git', '-C', str(source), 'rev-parse', 'HEAD'], text=True).strip()
if revision != '7e3c6fb5225d3ccf22807ee641332669df34650d':
    raise SystemExit('Use the exact pinned official MNN revision.')
if subprocess.check_output(['git', '-C', str(source), 'status', '--porcelain'], text=True):
    raise SystemExit('Use a clean, unmodified official MNN checkout.')
defines = (source / 'include/MNN/MNNDefine.h').read_text()
version = '.'.join(re.search(r'#define MNN_VERSION_' + part + r'\s+(\d+)', defines)[1]
                   for part in ('MAJOR', 'MINOR', 'PATCH'))
if version != '3.6.2':
    raise SystemExit('The pinned managed ABI requires MNN 3.6.2.')
print(source)
print(output)
PY
)
source_dir="${path_record%%$'\n'*}"
output_dir="${path_record#*$'\n'}"
mkdir -p "$output_dir"
cp "$native_dir/retain-linux-abi.cmake" "$output_dir/retain-linux-abi.cmake"
image=codex-mnn-linux-gcc13
if ! docker image inspect "$image" >/dev/null 2>&1; then
    docker build --platform linux/amd64 -t "$image" \
        -f "$native_dir/build-linux.Dockerfile" "$native_dir" \
        >"$output_dir/image-build.stdout" 2>"$output_dir/image-build.stderr"
fi
docker image inspect "$image" >"$output_dir/image.json"
image_id=$(python3 - "$output_dir/image.json" <<'PY'
import json
import sys
print(json.load(open(sys.argv[1]))[0]['Id'])
PY
)
cat >"$output_dir/container-build-linux.sh" <<'CONTAINER'
#!/bin/sh
set -eu
[ "$(uname -m)" = x86_64 ] || { echo 'Expected Linux x86_64.' >&2; exit 2; }
[ "$(gcc -dumpfullversion)" = 13.3.0 ] || { echo 'Expected GCC 13.3.0.' >&2; exit 2; }
[ "$(g++ -dumpfullversion)" = 13.3.0 ] || { echo 'Expected G++ 13.3.0.' >&2; exit 2; }
gcc --version
cmake --version
cmake -S /mnn-source -B /out/build \
    -DCMAKE_BUILD_TYPE=Release -DCMAKE_CXX_STANDARD=17 \
    -DCMAKE_PROJECT_MNN_INCLUDE=/out/retain-linux-abi.cmake \
    -DCMAKE_CXX_FLAGS='-include cmath' \
    -DCMAKE_CXX_FLAGS_RELEASE='-O3 -DNDEBUG -fno-visibility-inlines-hidden -fvisibility=default' \
    -DMNN_BUILD_SHARED_LIBS=ON -DMNN_SEP_BUILD=OFF \
    -DMNN_BUILD_LLM=ON -DMNN_BUILD_LLM_OMNI=ON \
    -DMNN_BUILD_TOOLS=OFF -DMNN_BUILD_TEST=OFF -DMNN_LLM_BUILD_DEMO=OFF \
    -DLLM_SUPPORT_HTTP_RESOURCE=OFF -DMNN_USE_THREAD_POOL=ON \
    -DMNN_LOW_MEMORY=ON -DMNN_SUPPORT_TRANSFORMER_FUSE=ON \
    -DMNN_OPENCL=OFF -DMNN_VULKAN=OFF -DMNN_AVX512=OFF
cmake --build /out/build --target MNN -j "$1"
cp /out/build/libMNN.so /out/libMNN.so
nm -D --defined-only /out/libMNN.so > /out/exports.txt
readelf -d /out/libMNN.so > /out/dynamic.txt
readelf --version-info /out/libMNN.so > /out/versions.txt
CONTAINER
build_status=0
docker run --rm --platform linux/amd64 \
    -v "$source_dir:/mnn-source:ro" -v "$output_dir:/out" \
    "$image_id" sh /out/container-build-linux.sh "$jobs" \
    >"$output_dir/build-linux.stdout" 2>"$output_dir/build-linux.stderr" || build_status=$?
if [[ "$build_status" -ne 0 ]]; then
    echo "Native build failed ($build_status); inspect $output_dir/build-linux.stderr" >&2
    exit "$build_status"
fi
python3 - "$package_dir" "$source_dir" "$output_dir" <<'PY'
from pathlib import Path
import hashlib
import json
import re
import subprocess
import sys
package, source, output = map(Path, sys.argv[1:])
# Keep the import decision in the managed symbol map. The Linux core selects
# common GNU entries plus its GNU std::vector<int> resize overload.
text = (package / 'Runtime/Interop/MNNInterop.DesktopSymbols.cs').read_text()
required = {name: symbol for name, symbol in re.findall(
    r'internal const string (\w+) = "([^"]+)";', text)
    if symbol.startswith('_ZN') and (name != 'ResizeTensor' or 'ERKSt6vector' in symbol)}
if len(required) != 27:
    raise SystemExit('Expected exactly 27 Linux core imports; update the audited contract first.')
exports = {line.split()[-1] for line in (output / 'exports.txt').read_text().splitlines() if line.strip()}
missing = sorted(set(required.values()) - exports)
custom = sorted(symbol for symbol in exports if symbol.startswith('MNN_'))
image = json.loads((output / 'image.json').read_text())[0]['Id']
report = {'kind': 'linux_official_cpp_symbol_audit', 'mnn_version': '3.6.2',
          'revision': '7e3c6fb5225d3ccf22807ee641332669df34650d',
          'abi': 'GCC 13.3.0 / GNU libstdc++ C++11 ABI / Linux x86_64 SysV / Mono',
          'image_id': image, 'required_imports': len(required),
          'missing': missing, 'custom_exports': custom,
          'sha256': hashlib.sha256((output / 'libMNN.so').read_bytes()).hexdigest(),
          'native_inference_verified': False, 'unity_player_verified': False}
(output / 'symbol-audit.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report, indent=2))
if missing or custom:
    raise SystemExit('Native symbol audit failed; the package was not changed.')
if subprocess.check_output(['git', '-C', str(source), 'status', '--porcelain'], text=True):
    raise SystemExit('Official source checkout changed during build; refusing install.')
PY
if "$install_plugin"; then
    destination="$package_dir/Runtime/Plugins/Linux/x86_64/libMNN.so"
    temporary="$(mktemp "${destination}.XXXXXX")"
    trap 'rm -f "$temporary"' EXIT
    cp "$output_dir/libMNN.so" "$temporary"
    chmod 644 "$temporary"
    mv "$temporary" "$destination"
    trap - EXIT
    echo "Installed audited Linux plugin: $destination"
else
    echo "Audited candidate: $output_dir/libMNN.so (not installed)"
fi
