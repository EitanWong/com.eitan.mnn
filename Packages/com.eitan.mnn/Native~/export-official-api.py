#!/usr/bin/env python3
"""Relink official object files to retain selected official inline API symbols.
No C++ source or custom entry points are generated.
"""
import pathlib, shlex, subprocess, sys
root = pathlib.Path(sys.argv[1]).resolve()
library = root / "libMNN.dylib"
required = [
    "__ZNK3MNN11Transformer3Llm10getContextEv",
    "__ZNK3MNN6Tensor7getTypeEv",
    "__ZNK3MNN6Tensor10dimensionsEv",
    "__ZNK3MNN6Tensor11elementSizeEv",
    "__ZN3MNN7Express8Variable7readMapIfEEPKT_v",
    "__ZN3MNN7Express4VARPD1Ev",
    # Use the same STL instantiation/allocator as the returned vector. Unity
    # Player interposes operator new/delete; direct system delete is unsafe.
    "__ZNSt3__16vectorIiNS_9allocatorIiEEED1B9nqn220106Ev",
]
def exports():
    output = subprocess.check_output(["nm", "-gU", str(library)], text=True)
    return {line.split()[-1] for line in output.splitlines() if line.split()}
current = exports()
if any(symbol.startswith("_MNN_") for symbol in current):
    raise SystemExit("Custom C exports found: rebuild from official MNN sources in a clean directory.")
listing = root / "official-symbols.txt"
listing.write_text("\n".join(sorted(current | set(required))) + "\n")
command = shlex.split((root / "CMakeFiles/MNN.dir/link.txt").read_text())
command.append("-Wl,-exported_symbols_list," + str(listing))
subprocess.run(command, cwd=root, check=True)
missing = set(required) - exports()
if missing:
    raise SystemExit("Official inline definitions were not emitted: " + str(sorted(missing)))
