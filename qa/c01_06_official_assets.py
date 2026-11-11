#!/usr/bin/env python3
"""C01-06: build approved platform logo and emblem, byte-exact from canonical Word.

No external assets, dependencies, network, tenant data or graphics substitution.
The complete original file keeps its SHA; emblem is an RGB pixel crop only.
"""
import argparse
from hashlib import sha256
from pathlib import Path
import struct
import zlib

from c01_06_logo_candidate import WORD, candidate, check_png, check
from master_source_guard import CURRENT_WORD_SHA, canonical_active_word

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "frontend/public/assets"
FULL = "crecisol-oficial.png"
EMBLEM = "crecisol-emblem.png"
APPROVED_SHA = "9e83d1d0a891565cf80a21a7e90d7afcaa24c9bb63e7d2a589790127dadd549f"
BOX = (390, 16, 948, 553)  # official blue emblem alone, no wordmark distortion


def chunk(kind, body):
    return (struct.pack(">I", len(body)) + kind + body
            + struct.pack(">I", zlib.crc32(kind + body) & 0xffffffff))


def crop_emblem(png):
    """Unfilter and crop 8-bit RGB PNG; never resample or modify original pixels."""
    check(check_png(png) == (1298, 859), "Unexpected source image dimensions")
    width, height, depth, color, comp, filtering, interlace = struct.unpack(">IIBBBBB", png[16:29])
    check((depth, color, comp, filtering, interlace) == (8, 2, 0, 0, 0), "Unsupported source PNG")
    parts = []
    pos = 8
    while pos < len(png):
        n = struct.unpack(">I", png[pos:pos+4])[0]
        if png[pos+4:pos+8] == b"IDAT":
            parts.append(png[pos+8:pos+8+n])
        pos += n + 12
    raw = zlib.decompress(b"".join(parts))
    stride = width * 3
    check(len(raw) == height * (stride + 1), "Unexpected PNG decoded length")
    x0, y0, x1, y1 = BOX
    check(0 <= x0 < x1 <= width and 0 <= y0 < y1 <= height, "Invalid emblem crop")
    prev = bytearray(stride)
    out = bytearray()
    offset = 0
    for y in range(height):
        mode = raw[offset]
        row = bytearray(raw[offset+1:offset+1+stride])
        offset += stride + 1
        check(0 <= mode <= 4, "PNG filter not supported")
        for i in range(stride):
            a = row[i-3] if i >= 3 else 0
            b = prev[i]
            c = prev[i-3] if i >= 3 else 0
            if mode == 1:
                row[i] = (row[i] + a) & 255
            elif mode == 2:
                row[i] = (row[i] + b) & 255
            elif mode == 3:
                row[i] = (row[i] + ((a+b)//2)) & 255
            elif mode == 4:
                p = a+b-c
                da, db, dc = abs(p-a), abs(p-b), abs(p-c)
                predictor = a if da <= db and da <= dc else b if db <= dc else c
                row[i] = (row[i] + predictor) & 255
        if y0 <= y < y1:
            out.append(0)
            out.extend(row[x0*3:x1*3])
        prev = row
    header = struct.pack(">IIBBBBB", x1-x0, y1-y0, 8, 2, 0, 0, 0)
    result = (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header)
              + chunk(b"IDAT", zlib.compress(bytes(out), 9))
              + chunk(b"IEND", b""))
    check(check_png(result) == (x1-x0, y1-y0), "Output PNG invalid")
    return result


def output_assets():
    # The approved commercial appendix changed the DOCX bytes, not the official logo.
    # Validate the current Word/PDF and immutable historic prefix before extraction.
    canonical_active_word()
    data, ext, member, w, h = candidate(WORD, CURRENT_WORD_SHA)
    check((ext, member, w, h) == ("png", "word/media/image1.png", 1298, 859),
          "Canonical image relationship changed")
    check(sha256(data).hexdigest() == APPROVED_SHA, "Owner-approved image hash mismatch")
    return ((FULL, data), (EMBLEM, crop_emblem(data)))


def main():
    parser = argparse.ArgumentParser()
    modes = parser.add_mutually_exclusive_group(required=True)
    modes.add_argument("--write", action="store_true")
    modes.add_argument("--check", action="store_true")
    args = parser.parse_args()
    files = output_assets()
    if args.write:
        OUTPUT.mkdir(parents=True, exist_ok=True)
    for name, data in files:
        destination = OUTPUT/name
        if args.write:
            destination.write_bytes(data)
        check(destination.is_file() and destination.read_bytes() == data,
              "Generated asset missing or modified: " + name)
        print("PASS", name, len(data), sha256(data).hexdigest())
    print("PASS approved CRECISOL assets", "written" if args.write else "verified")


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, KeyError, AssertionError) as error:
        raise SystemExit("FAIL C01-06: " + str(error))
