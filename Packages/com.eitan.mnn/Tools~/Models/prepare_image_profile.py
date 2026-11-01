#!/usr/bin/env python3
"""Prepare a pinned image profile with an existing official MNN 3.6.1 converter.

No C++ is generated or modified. Runtime inference uses only the converted MNN
graph. --download explicitly obtains the original ONNX/TFLite asset; otherwise
it must already exist. All models and diagnostics stay within this project.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import struct
import subprocess
import urllib.request

PROFILES = {
    "candy": {"revision": "f3fa44f82a867df0a3d08a2dd2c7eed88f2b0d92",
              "url": "https://raw.githubusercontent.com/DefTruth/lite.ai.toolkit/f3fa44f82a867df0a3d08a2dd2c7eed88f2b0d92/examples/hub/onnx/cv/style-candy-8.onnx",
              "source": "style-candy-8.onnx", "format": "ONNX", "graph": "style-candy-8.mnn",
              "source_sha": "e3c6f442e12014c5fec12b4307c14ca303df185cda19f67522afeef40c4d22b8",
              "graph_sha": "70c1069827b8df36212313e915abf727a046baf8b4fb95efb8eac612ee66047a", "size": 6734176,
              "uuid": b"e3c6f442-e120-44c5-bec1-2b4307c14ca3", "folder": "DefTruth--FastStyleTransfer-Candy",
              "index": 8, "biz": "MNNUnityStyleCandy"},
    "deeplab": {"revision": None,
                "url": "https://storage.googleapis.com/download.tensorflow.org/models/tflite/gpu/deeplabv3_257_mv_gpu.tflite",
                "source": "deeplabv3_257_mv_gpu.tflite", "format": "TFLITE", "graph": "deeplabv3.mnn",
                "source_sha": "68a539782c2c6a72f8aac3724600124a85ed977162b44e84cbae5db717c933c6",
                "graph_sha": "b510c32e45d689ebc7b8a0bf5070a89dafb77411c350a564f1e5b13f0038b640", "size": 2769072,
                "uuid": b"68a53978-2c2c-4a72-b8aa-c3724600124a", "folder": "TensorFlow--DeepLabV3-MobileNetV2",
                "index": 9, "biz": "MNNUnityDeepLab"},
    "movenet": {"revision": "ed0f314bb7356fd1dbf1e4f52c2d40791bf6534f",
                "url": "https://huggingface.co/Xenova/movenet-singlepose-lightning/resolve/ed0f314bb7356fd1dbf1e4f52c2d40791bf6534f/onnx/model.onnx",
                "source": "movenet-lightning.onnx", "format": "ONNX", "graph": "movenet-lightning.mnn",
                "source_sha": "1ad4f8d6c2f776a9967db3993c9ca740bc350104f9d37c151dc183fc29a464ad",
                "graph_sha": "79b22749faaf89303f3714ba6fdf0c66a647b20425076cbd7ea64d8d9ec39d69", "size": 9349132,
                "uuid": b"1ad4f8d6-c2f7-46a9-967d-b3993c9ca740", "folder": "TensorFlow--MoveNet-SinglePose-Lightning",
                "index": 10, "biz": "MNNUnityMoveNet"},
    "depth": {"revision": "4472b7362082ad9968fee890ca0f1e5aca36b93d",
              "url": "https://huggingface.co/onnx-community/depth-anything-v2-small/resolve/4472b7362082ad9968fee890ca0f1e5aca36b93d/onnx/model.onnx",
              "source": "depth-anything-v2-small.onnx", "format": "ONNX", "graph": "depth-anything-v2-small.mnn",
              "source_sha": "afb6a5c28f3b6bf1618c6e43f02073ef9dfdc70e937502d51603e57b0a1df10c",
              "graph_sha": "a91629bc3c318f129ba0461384a8a5f9a58e6c998f8e01a6121ec8bb30b5f9fa", "size": 99030556,
              "uuid": b"afb6a5c2-8f3b-4bf1-a18c-6e43f02073ef", "folder": "DepthAnything--V2-Small",
              "index": 11, "biz": "MNNUnityDepthAnythingV2", "arguments": ["--useGeluApproximation", "0", "--convertMatmulToConv", "0"]},
}


def prepare_depth_source(source, destination):
    """Preserve dynamic DPT geometry while spelling ONNX cubic edge semantics
    with standard Gather/arithmetic ops. Official MNN 3.6.1 CPU cubic truncates
    negative half-pixel coordinates instead of flooring them. No native edits.
    Requires existing numpy/onnx packages; this tool never installs dependencies.
    """
    import onnx
    import numpy as np
    from onnx import helper as H, numpy_helper as N
    m = onnx.load(source)
    node = next(n for n in m.graph.node if n.name == '/Resize')
    if node.op_type != 'Resize' or next(a.s for a in node.attribute if a.name == 'mode') != b'cubic':
        raise ValueError('The pinned positional interpolation is missing.')
    nodes = []
    prefix = "depth_pos/"

    def const(name, value):
        name = prefix + name
        m.graph.initializer.append(N.from_array(np.asarray(value), name))
        return name

    def op(kind, *inputs, **attributes):
        name = prefix + str(len(nodes)) + "_" + kind
        nodes.append(H.make_node(kind, list(inputs), [name], name=name, **attributes))
        return name

    zero = const("zero", np.int64(0))
    one = const("one", np.int64(1))
    maximum = const("max", np.int64(36))
    scale = const("scale", np.float32(37))
    half = const("half", np.float32(.5))
    factors = {str(v): const("f" + str(v), np.float32(v)) for v in [-.75, 1, 1.25, 2.25, 1.5, .75]}
    value = node.input[0]
    for axis in [2, 3]:
        size = op("Gather", node.input[3], const("axis" + str(axis), np.int64(axis)), axis=0)
        positions = op("Range", zero, size, one)
        # Original grid is 37x37. Compute half-pixel source coordinates without
        # clamping them: floor(-epsilon) must be -1, then clamp each tap's index.
        position = op("Sub", op("Div", op("Mul", op("Add", op("Cast", positions, to=1), half), scale), op("Cast", size, to=1)), half)
        floor = op("Floor", position)
        t = op("Sub", position, floor)
        t2 = op("Mul", t, t)
        t3 = op("Mul", t2, t)
        # Cubic a=-.75 weights for floor(position)+[-1,0,1,2]. Keep
        # expression/initializer order stable for the pinned graph hash.
        weights = [
            op("Mul", factors["-0.75"], op("Add", op("Sub", t3, op("Add", t2, t2)), t)),
            op("Add", op("Sub", op("Mul", factors["1.25"], t3), op("Mul", factors["2.25"], t2)), factors["1"]),
            op("Add", op("Sub", op("Mul", factors["1.5"], t2), op("Mul", factors["1.25"], t3)), op("Mul", factors["0.75"], t)),
            op("Mul", factors["-0.75"], op("Sub", t2, t3)),
        ]
        terms = []
        for i in range(4):
            index = op("Min", op("Max", op("Add", op("Cast", floor, to=7), const("offset" + str(axis) + str(i), np.int64(i - 1))), zero), maximum)
            shape = [1, 1, 1, 1]
            shape[axis] = -1
            weight = op("Reshape", weights[i], const("shape" + str(axis) + str(i), np.array(shape, np.int64)))
            terms.append(op("Mul", op("Gather", value, index, axis=axis), weight))
        value = op("Add", op("Add", terms[0], terms[1]), op("Add", terms[2], terms[3]))
    nodes.append(H.make_node("Identity", [value], list(node.output), name="depth_pos/output"))
    index = list(m.graph.node).index(node)
    del m.graph.node[index]
    for i, replacement in enumerate(nodes):
        m.graph.node.insert(index + i, replacement)
    onnx.checker.check_model(m)
    onnx.save(m, destination)
    return destination


def canonical_graph(data, profile):
    """Canonicalize only the public schema's Net.mnn_uuid (field 11).

    The converter gives identical graph contents a new UUID each time. A stable
    profile UUID allows byte-for-byte verification across repeated conversions;
    weights, operators, names, shapes and other metadata remain untouched.
    """
    data = bytearray(data)
    if len(data) != profile["size"]:
        raise ValueError("Unexpected converted graph size; use the pinned source and official 3.6.1 converter.")
    root = struct.unpack_from("<I", data, 0)[0]
    if not 4 <= root < len(data) - 4:
        raise ValueError("Invalid Net root.")
    table = root - struct.unpack_from("<i", data, root)[0]
    if not 0 <= table < len(data) - 28 or struct.unpack_from("<H", data, table)[0] < 28:
        raise ValueError("Missing Net UUID field.")
    offset = struct.unpack_from("<H", data, table + 4 + 2 * 11)[0]
    if offset == 0:
        raise ValueError("Missing Net UUID.")
    field = root + offset
    string = field + struct.unpack_from("<I", data, field)[0]
    if not 0 <= string <= len(data) - 41 or struct.unpack_from("<I", data, string)[0] != 36:
        raise ValueError("Unexpected Net UUID representation.")
    data[string + 4:string + 40] = profile["uuid"]
    if hashlib.sha256(data).hexdigest() != profile["graph_sha"]:
        raise ValueError("Converted operators/weights differ from the validated image profile; refusing installation.")
    return data


def main():
    root = Path(__file__).resolve().parents[4]
    models = root / "Assets/StreamingAssets/MNN/Models"
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile", choices=PROFILES, default="candy")
    parser.add_argument("--converter", required=True, type=Path, help="Existing official MNN 3.6.1 MNNConvert executable.")
    parser.add_argument("--directory", type=Path, default=None)
    parser.add_argument("--download", action="store_true")
    args = parser.parse_args()
    profile = PROFILES[args.profile]
    directory = (args.directory or models / profile["folder"]).resolve()
    if models.resolve() not in directory.parents:
        parser.error("The prepared model must be inside Assets/StreamingAssets/MNN/Models/.")
    converter = args.converter.resolve()
    if not converter.is_file():
        parser.error("Provide an existing official converter; this tool does not download or build native executables.")
    directory.mkdir(parents=True, exist_ok=True)
    artifacts = root / ("TestArtifacts~/ImageProfilePreparation/" + args.profile)
    artifacts.mkdir(parents=True, exist_ok=True)
    source = directory / profile["source"]
    if not source.exists() and args.download:
        partial = artifacts / "source.download"
        urllib.request.urlretrieve(profile["url"], partial)
        if hashlib.sha256(partial.read_bytes()).hexdigest() != profile["source_sha"]:
            raise ValueError("Downloaded source does not match the pinned source hash.")
        shutil.move(str(partial), source)
    if not source.is_file() or hashlib.sha256(source.read_bytes()).hexdigest() != profile["source_sha"]:
        parser.error("Place the pinned source model in the model directory, or explicitly use --download.")
    converted = artifacts / "converted.mnn"
    converted.unlink(missing_ok=True)
    conversion_source = prepare_depth_source(source, artifacts / "depth-dynamic.onnx") if args.profile == "depth" else source
    command = [str(converter), "-f", profile["format"], "--modelFile", str(conversion_source), "--MNNModel", str(converted), "--bizCode", profile["biz"]] + profile.get("arguments", [])
    with (artifacts / "convert.log").open("w") as log:
        subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, check=True)
    data = canonical_graph(converted.read_bytes(), profile)
    (directory / profile["graph"]).write_bytes(data)
    (directory / "mnn_image_task.json").write_text(json.dumps({"profile": profile["index"]}, indent=2) + "\n")
    provenance = {"source_url": profile["url"], "source_sha256": profile["source_sha"], "source_revision": profile["revision"],
                  "graph_sha256": profile["graph_sha"], "graph_size": len(data), "converter_sha256": hashlib.sha256(converter.read_bytes()).hexdigest(),
                  "official_mnn_commit": "024a946b0b8fcf87c8a418229fadd4cd7858ffba", "conversion_command": command,
                  "metadata_change": "Public Net.mnn_uuid canonicalized; no weights or operators changed.",
                  "profile": args.profile, "source_transform": "Standard ONNX cubic decomposition (dynamic sizes, a=-0.75, half_pixel, clamped indices). Exact GELU; no MatMul-to-Conv." if args.profile == "depth" else None}

    (artifacts / "provenance.json").write_text(json.dumps(provenance, indent=2) + "\n")
    print("Prepared image profile:", args.profile, directory)
    print("Verified MNN SHA-256:", profile["graph_sha"])
    print("Select this local folder in the corresponding MNNStudio image task.")


if __name__ == "__main__":
    main()
