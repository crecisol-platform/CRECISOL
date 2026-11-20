#!/usr/bin/env python3
"""Verify 25 synthetic operator PNG/JSON pairs from Chromium against GITHUB_SHA."""
from collections import Counter
from hashlib import sha256
from pathlib import Path
import json
import os
import struct
import sys

VIEWS = ("casos", "sesiones", "perfil", "seguridad", "monitor")
SIZES = (320, 390, 768, 1024, 1440)
NAMES = {"casos": "Casos", "sesiones": "Sesiones", "perfil": "Perfil",
         "seguridad": "Seguridad", "monitor": "Monitor"}


def need(ok: bool, message: str) -> None:
    if not ok:
        raise AssertionError("C05 F04 EVIDENCE NO-GO: " + message)


def main(folder: Path) -> None:
    need(folder.is_dir(), "missing Playwright report directory")
    sha = os.environ.get("GITHUB_SHA", "")
    need(len(sha) == 40, "runner SHA absent")
    jsons = sorted(x for x in folder.rglob("*.json") if x.name != ".last-run.json")
    need(len(jsons) == 25, f"expected 25 synthetic results, found {len(jsons)}")
    counts: Counter[int] = Counter()
    names: set[str] = set()
    manifest = []
    for file in jsons:
        data = json.loads(file.read_text("utf-8"))
        section = data.get("section")
        width = data.get("viewport", {}).get("width")
        height = data.get("viewport", {}).get("height")
        name = data.get("name")
        need(section in VIEWS and width in SIZES, "unknown operator view or width")
        need(name == f"operator-{section}-{width}" and name not in names,
             "duplicate/invalid operator capture")
        names.add(name)
        need(data.get("synthetic") is True and data.get("runnerSha") == sha,
             "wrong SHA or non-synthetic evidence")
        need(data.get("path") == "/cuenta" and data.get("search") == "?vista=operadores",
             "operator view escaped platform route")
        need(data.get("privateAccountNav") is False and data.get("overflow") <= 2,
             "business account nav or page overflow")
        need(data.get("activeSection") == NAMES[section],
             "wrong active operator section")
        need(data.get("png") == name + ".png", "unsafe filename")
        png = file.parent / data["png"]
        need(png.is_file() and png.stat().st_size > 1024, "PNG absent")
        raw = png.read_bytes()
        need(raw[:8] == b"\x89PNG\r\n\x1a\n" and raw[12:16] == b"IHDR", "invalid PNG")
        w, h = struct.unpack(">II", raw[16:24])
        need(w == width and h >= height, "image dimensions mismatch")
        counts[width] += 1
        manifest.append({"view": section, "width": width, "sha256": sha256(raw).hexdigest(),
                         "filename": str(png.relative_to(folder))})
    need(all(counts[w] == 5 for w in SIZES), "incomplete five-view matrix")
    out = {"result": "PASS_F04_OPERATOR_SYNTHETIC_VISUAL", "sha": sha, "images": 25,
           "sizes": dict(sorted(counts.items())), "synthetic": True,
           "backendIsolationProvenByTheseImages": False,
           "humanVisualCertification": False, "files": manifest}
    (folder / "F04_OPERATOR_MANIFEST.json").write_text(json.dumps(out, indent=2), "utf-8")
    print(f"F04 visual evidence PASS: 25 PNG/JSON, 5 views x 5 widths, SHA {sha[:12]}")


if __name__ == "__main__":
    try:
        need(len(sys.argv) == 2, "expected test-results directory")
        main(Path(sys.argv[1]))
    except (AssertionError, ValueError, OSError, TypeError, KeyError) as error:
        print("FAIL:", error, file=sys.stderr)
        raise SystemExit(1) from error
