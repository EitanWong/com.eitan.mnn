#!/bin/bash
set -euo pipefail
if [[ $# -lt 1 ]]; then
    echo "Usage: $0 /path/to/existing/MNN-source [--no-install] [--benchmarks] [--legacy-global-noinline] [--output=TestArtifacts~/path]" >&2
    exit 1
fi
package_dir="$(cd "$(dirname "$0")/.." && pwd)"
project_dir="$(cd "$package_dir/../.." && pwd)"
mnn_source_dir="$(cd "$1" && pwd)"
python3 - "$mnn_source_dir" <<'PY'
from pathlib import Path
import re
import subprocess
import sys
source = Path(sys.argv[1])
defines = (source / "include/MNN/MNNDefine.h").read_text()
version = ".".join(re.search(r"#define MNN_VERSION_" + part + r"\s+(\d+)", defines)[1]
                   for part in ("MAJOR", "MINOR", "PATCH"))
if version != "3.6.2":
    raise SystemExit("The bundled compiler ABI requires MNN 3.6.2; revalidate before upgrading.")
if (source / ".git").exists() and subprocess.check_output(
        ["git", "-C", str(source), "status", "--porcelain", "--untracked-files=no"], text=True):
    raise SystemExit("Use unmodified official sources; the supplied checkout has tracked changes.")
PY
validation_dir="$project_dir/TestArtifacts~/MNNValidation/OfficialNative"
shift
install_plugin=true
build_demo=OFF
inline_flags=''
for option in "$@"; do
    case "$option" in
        --no-install) install_plugin=false ;;
        --benchmarks) build_demo=ON ;;
        --legacy-global-noinline) inline_flags=' -fno-inline-functions' ;;
        --output=*) validation_dir="${option#--output=}" ;;
        *) echo "Unknown option: $option" >&2; exit 1 ;;
    esac
done
validation_dir="$(python3 - "$project_dir" "$validation_dir" <<'PY'
from pathlib import Path
import sys
project, output = map(lambda value: Path(value).resolve(), sys.argv[1:])
if project / "TestArtifacts~" not in output.parents:
    raise SystemExit("--output must be under this project's TestArtifacts~/")
print(output)
PY
)"
for arch in arm64 x86_64; do
    cmake -S "$mnn_source_dir" -B "$validation_dir/$arch" \
        -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_POLICY_VERSION_MINIMUM=3.5 -DCMAKE_CXX_STANDARD=17 \
        -DCMAKE_CXX_FLAGS="-include cmath -femit-all-decls${inline_flags} -D_LIBCPP_DISABLE_VISIBILITY_ANNOTATIONS" \
        -DCMAKE_PROJECT_MNN_INCLUDE="$package_dir/Native~/retain-abi.cmake" \
        -DCMAKE_CXX_FLAGS_RELEASE='-O3 -DNDEBUG -fno-visibility-inlines-hidden -fvisibility=default' \
        -DCMAKE_OSX_ARCHITECTURES="$arch" -DCMAKE_OSX_DEPLOYMENT_TARGET=11.0 \
        -DMNN_BUILD_SHARED_LIBS=ON -DMNN_SEP_BUILD=OFF \
        -DMNN_BUILD_LLM=ON -DMNN_BUILD_LLM_OMNI=ON \
        -DMNN_BUILD_TOOLS=OFF -DMNN_BUILD_TEST=OFF -DMNN_LLM_BUILD_DEMO="$build_demo" \
        -DLLM_SUPPORT_HTTP_RESOURCE=OFF -DMNN_USE_THREAD_POOL=ON \
        -DMNN_LOW_MEMORY=ON -DMNN_SUPPORT_TRANSFORMER_FUSE=ON \
        -DMNN_ARM82=ON -DMNN_SME2=ON \
        -DMNN_METAL=ON -DMNN_AAPL_FMWK=OFF
    cmake --build "$validation_dir/$arch" --target MNN -j 6
    if [[ "$build_demo" == ON ]]; then
        cmake --build "$validation_dir/$arch" --target llm_bench llm_demo -j 6
    fi
    python3 "$package_dir/Native~/export-official-api.py" "$validation_dir/$arch"
done
lipo -create "$validation_dir/arm64/libMNN.dylib" "$validation_dir/x86_64/libMNN.dylib" -output "$validation_dir/libMNN.dylib"
codesign --force --sign - "$validation_dir/libMNN.dylib"
# Atomic replacement preserves existing mappings until Unity restarts.
if "$install_plugin"; then
    mv "$validation_dir/libMNN.dylib" "$package_dir/Runtime/Plugins/macOS/libMNN.dylib"
fi
