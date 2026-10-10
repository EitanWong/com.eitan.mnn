#!/bin/bash
set -euo pipefail
if [[ $# != 1 ]]; then
    echo "Usage: $0 /path/to/existing/MNN-source" >&2
    exit 1
fi
package_dir="$(cd "$(dirname "$0")/.." && pwd)"
project_dir="$(cd "$package_dir/../.." && pwd)"
mnn_source_dir="$(cd "$1" && pwd)"
validation_dir="$project_dir/TestArtifacts~/MNNValidation/OfficialNative"
for arch in arm64 x86_64; do
    cmake -S "$mnn_source_dir" -B "$validation_dir/$arch" \
        -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_CXX_FLAGS='-femit-all-decls -fno-inline-functions -D_LIBCPP_DISABLE_VISIBILITY_ANNOTATIONS' \
        -DCMAKE_CXX_FLAGS_RELEASE='-O3 -DNDEBUG -fno-visibility-inlines-hidden -fvisibility=default' \
        -DCMAKE_OSX_ARCHITECTURES="$arch" -DCMAKE_OSX_DEPLOYMENT_TARGET=11.0 \
        -DMNN_BUILD_SHARED_LIBS=ON -DMNN_SEP_BUILD=OFF \
        -DMNN_BUILD_LLM=ON -DMNN_BUILD_LLM_OMNI=ON \
        -DMNN_BUILD_TOOLS=OFF -DMNN_BUILD_TEST=OFF -DMNN_LLM_BUILD_DEMO=OFF \
        -DMNN_METAL=ON -DMNN_AAPL_FMWK=OFF
    cmake --build "$validation_dir/$arch" --target MNN -j 6
    python3 "$package_dir/Native~/export-official-api.py" "$validation_dir/$arch"
done
lipo -create "$validation_dir/arm64/libMNN.dylib" "$validation_dir/x86_64/libMNN.dylib" -output "$validation_dir/libMNN.dylib"
codesign --force --sign - "$validation_dir/libMNN.dylib"
# Atomic replacement preserves existing mappings until Unity restarts.
mv "$validation_dir/libMNN.dylib" "$package_dir/Runtime/Plugins/macOS/libMNN.dylib"
