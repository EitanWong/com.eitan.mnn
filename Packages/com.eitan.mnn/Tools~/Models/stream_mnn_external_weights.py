"""Share exact serialized MNN weight records with bounded external-weight buffers.

Uses the public serialized MNN 3.6.1 schema parser in the adjacent compactor.
No native layout, operator, quantization or numerical changes are introduced.
This tool does not establish runtime inference correctness.
"""
from pathlib import Path
import hashlib
import importlib.util
import struct

BASE = Path(__file__).resolve().parent
SPEC = importlib.util.spec_from_file_location('existing_external_records',
    BASE / 'deduplicate_mnn_external_weights.py')
EXISTING = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(EXISTING)
CHUNK_BYTES = 4 * 1024 * 1024


def chunks(path, offset, size):
    with path.open('rb') as stream:
        stream.seek(offset)
        remaining = size
        while remaining:
            data = stream.read(min(remaining, CHUNK_BYTES))
            if not data:
                raise ValueError('External weights truncated while reading')
            remaining -= len(data)
            yield data


def digest_range(path, offset, size):
    digest = hashlib.sha256()
    for data in chunks(path, offset, size):
        digest.update(data)
    return digest.digest()


def validate_ranges(entries, size):
    end = 0
    ranges = []
    for offset, lengths, _ in entries:
        stop = offset + sum(lengths)
        if offset < 0 or not lengths or any(value < 0 for value in lengths) or stop > size:
            raise ValueError('External record outside source file')
        ranges.append((offset, stop))
    for start, stop in sorted(ranges):
        if start > end:
            raise ValueError('unreferenced external weight bytes')
        end = max(end, stop)
    if end != size:
        raise ValueError('unreferenced external weight bytes')


def source_models(sources, output, weight_name):
    names = [source.name for source in sources]
    if not sources or len(set(names)) != len(names):
        raise ValueError('Provide sources with distinct graph names')
    if Path(weight_name).name != weight_name or not weight_name.endswith('.weight'):
        raise ValueError('Provide a plain weight file name')
    destinations = {(output / name).resolve() for name in [*names, weight_name]}
    models = []
    for source in sources:
        weights = Path(str(source) + '.weight')
        if source.resolve() in destinations or weights.resolve() in destinations:
            raise ValueError('Use separate output paths')
        model = EXISTING.Model(source.read_bytes())
        entries = model.external_records()
        validate_ranges(entries, weights.stat().st_size)
        models.append((source.name, model, weights, entries))
    return models


def assign_records(models):
    seen, unique, total = {}, [], 0
    for _, model, weights, entries in models:
        for offset, sizes, field in entries:
            size = sum(sizes)
            key = (sizes, digest_range(weights, offset, size))
            if key in seen:
                target, previous, previous_offset = seen[key]
                same = all(left == right for left, right in zip(chunks(weights, offset, size),
                                                                 chunks(previous, previous_offset, size)))
                if not same:
                    raise ValueError('Weight checksum collision')
            else:
                target = total
                seen[key] = (target, weights, offset)
                unique.append((weights, offset, size))
                total += size
            struct.pack_into('<q', model.data, field, target)
    return unique, total


def save_weights(unique, destination):
    temporary = destination.with_name(destination.name + '.tmp')
    digest = hashlib.sha256()
    with temporary.open('wb') as output:
        for path, offset, size in unique:
            for data in chunks(path, offset, size):
                output.write(data)
                digest.update(data)
    temporary.replace(destination)
    return digest.hexdigest()


def compact_models(sources, output_directory, weight_name='shared.mnn.weight'):
    models = source_models(sources, output_directory, weight_name)
    unique, total = assign_records(models)
    output_directory.mkdir(parents=True, exist_ok=True)
    checksum = save_weights(unique, output_directory / weight_name)
    graphs = []
    for name, model, _, entries in models:
        path = output_directory / name
        temporary = path.with_name(path.name + '.tmp')
        temporary.write_bytes(model.data)
        temporary.replace(path)
        graphs.append(dict(path=name, records=len(entries), graph_bytes=len(model.data),
                           sha256=hashlib.sha256(model.data).hexdigest()))
    return dict(source_weight_bytes=sum(weights.stat().st_size for _, _, weights, _ in models),
                weight_file=weight_name, weight_bytes=total, weight_sha256=checksum, graphs=graphs)


def main():
    import argparse
    import json
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, action='append', required=True,
                        help='Externalized MNN graph; repeat for multiple graphs, each with a .weight sidecar')
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--weight-name', default='shared.mnn.weight',
                        help='Consumers must explicitly select this shared external file')
    args = parser.parse_args()
    print(json.dumps(compact_models(args.source, args.output, args.weight_name), indent=2))


if __name__ == '__main__':
    main()
