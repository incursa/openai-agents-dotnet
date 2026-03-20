#!/usr/bin/env python3

"""Import a single audio fixture from MLCommons/peoples_speech microset."""

from __future__ import annotations

import argparse
from pathlib import Path

import pyarrow.ipc as ipc
from datasets import load_dataset


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Download a single microset audio sample from MLCommons/peoples_speech and write it to disk."
    )
    parser.add_argument(
        "--output",
        required=True,
        help="Destination file path for the extracted audio sample.",
    )
    parser.add_argument(
        "--index",
        type=int,
        default=0,
        help="Zero-based row index to export from the microset train split.",
    )
    parser.add_argument(
        "--config",
        default="microset",
        help="Dataset config to load. Defaults to microset.",
    )
    parser.add_argument(
        "--split",
        default="train",
        help="Dataset split to load. Defaults to train.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    output_path = Path(args.output).expanduser().resolve()
    output_path.parent.mkdir(parents=True, exist_ok=True)

    dataset = load_dataset("MLCommons/peoples_speech", args.config, split=args.split)
    cache_file = Path(dataset.cache_files[0]["filename"])

    with cache_file.open("rb") as stream:
        reader = ipc.RecordBatchStreamReader(stream)
        table = reader.read_all()

    if args.index < 0 or args.index >= table.num_rows:
        raise IndexError(f"Index {args.index} is out of range for dataset with {table.num_rows} rows.")

    row = table.slice(args.index, 1).to_pylist()[0]
    audio = row["audio"]
    audio_bytes = audio["bytes"]
    source_path = audio["path"]

    output_path.write_bytes(audio_bytes)
    print(f"Wrote {len(audio_bytes)} bytes from {source_path} to {output_path}")
    print(f"Row id: {row['id']}")
    print(f"Transcript: {row['text']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
