#!/usr/bin/env python3
"""Share identical external weight records in an official MNN 3.6.1 graph.

Only serialized model data is changed. Net.oplists, Op.main, Convolution2D.external,
Blob.external and Scale.external follow the official schema/default/*.fbs tables. This does not
modify MNN source, native object layouts, operators, values or quantization.
Requires converter --saveExternalData; all source bytes must be accounted for.
"""
from __future__ import annotations

import argparse
import hashlib
from pathlib import Path
import struct


def compact_records(data: bytes, records: list[tuple[int, tuple[int, ...]]]) -> tuple[bytes, list[int]]:
    ranges = []
    for offset, sizes in records:
        if offset < 0 or not sizes or any(size < 0 for size in sizes) or offset + sum(sizes) > len(data):
            raise ValueError('external weight record is outside its source file')
        ranges.append((offset, offset + sum(sizes)))
    end = 0
    for start, stop in sorted(ranges):
        if start > end:
            raise ValueError('unreferenced external weight bytes')
        end = max(end, stop)
    if end != len(data):
        raise ValueError('unreferenced external weight bytes')
    output = bytearray()
    seen = {}
    offsets = []
    for offset, sizes in records:
        value = data[offset:offset + sum(sizes)]
        key = (sizes, hashlib.sha256(value).digest())
        if key not in seen:
            seen[key] = len(output)
            output.extend(value)
        target = seen[key]
        if output[target:target + len(value)] != value:
            raise ValueError('weight checksum collision')
        offsets.append(target)
    return bytes(output), offsets


class Model:
    def __init__(self, data: bytes):
        self.data = bytearray(data)

    def integer(self, at: int, code: str = 'I') -> int:
        return struct.unpack_from('<' + code, self.data, at)[0]

    def field(self, table: int, slot: int) -> int:
        vtable = table - self.integer(table, 'i')
        position = 4 + slot * 2
        if position >= self.integer(vtable, 'H'):
            return 0
        offset = self.integer(vtable + position, 'H')
        return table + offset if offset else 0

    def follow(self, at: int) -> int:
        return at + self.integer(at)

    def external_records(self) -> list[tuple[int, tuple[int, ...], int]]:
        root = self.follow(0)
        subgraphs = self.field(root, 10)
        if subgraphs and self.integer(self.follow(subgraphs)):
            raise ValueError('subgraphs are outside this compactor contract')
        ops = self.follow(self.field(root, 3))
        records = []
        for index in range(self.integer(ops)):
            op = self.follow(ops + 4 + index * 4)
            parameter_type = self.integer(self.field(op, 1), 'B')
            external_slot = {9: 6, 7: 9, 58: 3}.get(parameter_type)
            if external_slot is None:
                continue
            parameter = self.follow(self.field(op, 2))
            external = self.field(parameter, external_slot)
            if not external:
                continue
            vector = self.follow(external)
            values = tuple(self.integer(vector + 4 + i * 8, 'q') for i in range(self.integer(vector)))
            if len(values) < 2:
                raise ValueError('invalid external descriptor')
            records.append((values[0], values[1:], vector + 4))
        if not records:
            raise ValueError('graph contains no supported external weights')
        return records


def compact_model(source: Path, output: Path) -> dict:
    if source.resolve() == output.resolve():
        raise ValueError('use a separate output path')
    model = Model(source.read_bytes())
    records = model.external_records()
    data = Path(str(source) + '.weight').read_bytes()
    compact, offsets = compact_records(data, [(offset, sizes) for offset, sizes, _ in records])
    for (_, _, field), offset in zip(records, offsets):
        struct.pack_into('<q', model.data, field, offset)
    output.parent.mkdir(parents=True, exist_ok=True)
    weights = Path(str(output) + '.weight')
    temporary_weights = weights.with_name(weights.name + '.tmp')
    temporary_graph = output.with_name(output.name + '.tmp')
    temporary_weights.write_bytes(compact)
    temporary_graph.write_bytes(model.data)
    temporary_weights.replace(weights)
    temporary_graph.replace(output)
    return {'records': len(records), 'source_weight_bytes': len(data), 'weight_bytes': len(compact),
            'graph_bytes': len(model.data), 'sha256': hashlib.sha256(model.data).hexdigest(),
            'weight_sha256': hashlib.sha256(compact).hexdigest()}


def compact_models(sources: list[Path], output_directory: Path, weight_name: str = 'shared.mnn.weight') -> dict:
    """Share exact records across graphs; consumers explicitly select this external file."""
    names = [source.name for source in sources]
    if not sources or len(set(names)) != len(names):
        raise ValueError('provide nonempty graph sources with distinct file names')
    if Path(weight_name).name != weight_name or not weight_name.endswith('.weight'):
        raise ValueError('provide a plain external weight file name')
    outputs = {str((output_directory / name).resolve()) for name in [*names, weight_name]}
    if any(str(path.resolve()) in outputs for source in sources for path in [source, Path(str(source) + '.weight')]):
        raise ValueError('use separate output paths')
    models, combined, records = [], bytearray(), []
    source_bytes = 0
    for source in sources:
        model = Model(source.read_bytes())
        entries = model.external_records()
        data = Path(str(source) + '.weight').read_bytes()
        # Validate each file independently so a bad record cannot reach into the next file.
        part, offsets = compact_records(data, [(offset, sizes) for offset, sizes, _ in entries])
        records.extend((len(combined) + offset, sizes) for (_, sizes, _), offset in zip(entries, offsets))
        combined.extend(part)
        source_bytes += len(data)
        models.append((source.name, model, entries))
    shared, offsets = compact_records(bytes(combined), records)
    cursor, graphs = 0, []
    for name, model, entries in models:
        for _, _, field in entries:
            struct.pack_into('<q', model.data, field, offsets[cursor])
            cursor += 1
        graphs.append(dict(path=name, records=len(entries), graph_bytes=len(model.data),
                           sha256=hashlib.sha256(model.data).hexdigest()))
    output_directory.mkdir(parents=True, exist_ok=True)
    pending = [(output_directory / weight_name, shared)]
    pending.extend((output_directory / name, model.data) for name, model, _ in models)
    for path, data in pending:
        temporary = path.with_name(path.name + '.tmp')
        temporary.write_bytes(data)
        temporary.replace(path)
    return dict(source_weight_bytes=source_bytes, weight_file=weight_name, weight_bytes=len(shared),
                weight_sha256=hashlib.sha256(shared).hexdigest(), graphs=graphs)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    import json
    print(json.dumps(compact_model(args.source, args.output), indent=2))


if __name__ == '__main__':
    main()
