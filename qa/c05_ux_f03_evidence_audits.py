#!/usr/bin/env python3
"""C05 UX F03: verify ACTUAL Chromium PNG/JSON artifacts and source SHA.

Synthetic navigation snapshots are not credentials, tenant authorization,
API integration, human 9/10 review, or a proof of a real 30-minute timeout.
"""
from collections import Counter
from hashlib import sha256
from pathlib import Path
import json
import os
import struct
import sys

EXPECTED = ("guest", "guest-recovery", "account-profile",
            "account-security", "operator-cases", "operator-security")
WIDTHS = (320, 390, 768, 1024, 1440)


def require(condition: bool, description: str) -> None:
    if not condition:
        raise AssertionError("F03 EVIDENCE NO-GO: " + description)


def read_dimensions(path: Path) -> tuple[int, int]:
    with path.open("rb") as stream:
        head = stream.read(24)
    require(head[:8] == b"\x89PNG\r\n\x1a\n" and head[12:16] == b"IHDR",
            "invalid PNG header at " + str(path))
    return struct.unpack(">II", head[16:24])


def main(root: Path) -> None:
    require(root.is_dir(), "missing Playwright output folder")
    files = sorted(path for path in root.rglob("*.json")
                   if path.name not in (".last-run.json", "F03_EVIDENCE_MANIFEST.json"))
    require(len(files) == len(EXPECTED) * len(WIDTHS),
            f"expected 30 screenshot metadata entries, got {len(files)}")
    sha_expected = os.getenv("GITHUB_SHA") or ""
    require(len(sha_expected) == 40, "missing GitHub run commit SHA")
    seen: set[str] = set()
    manifest = []
    per_width: Counter[int] = Counter()
    for metadata in files:
        data = json.loads(metadata.read_text(encoding="utf-8"))
        name = data.get("id")
        require(isinstance(name, str) and name and name not in seen,
                "missing or duplicate screenshot id")
        seen.add(name)
        viewport = data.get("viewport", {})
        width, height = viewport.get("width"), viewport.get("height")
        require(width in WIDTHS and isinstance(height, int) and height >= 650,
                "invalid viewport in " + name)
        scenario = next((scenario for scenario in EXPECTED
                         if name == scenario + "-" + str(width)), None)
        require(scenario is not None, "unexpected scenario: " + name)
        require(data.get("realm") in ("guest", "account", "operator"),
                "unknown or missing synthetic realm " + name)
        require(name.startswith(data["realm"]) or
                (data["realm"] == "guest" and name.startswith("guest-")),
                "realm mismatch in " + name)
        require(data.get("synthetic") is True, "real data in screenshot declaration " + name)
        require(data.get("runnerSha") == sha_expected,
                "screenshot does not match GitHub SHA " + name)
        require(data.get("pathname") == "/cuenta", "outside C05 route: " + name)
        require(isinstance(data.get("overflow"), int) and data["overflow"] <= 2,
                "document overflow >2px: " + name)
        file_name = data.get("png")
        require(isinstance(file_name, str) and Path(file_name).name == file_name
                and file_name.endswith(".png"), "unsafe screenshot filename")
        png = metadata.parent / file_name
        require(png.is_file() and png.stat().st_size > 1024, "missing/empty PNG " + name)
        png_width, png_height = read_dimensions(png)
        require(png_width == width and png_height >= height,
                "PNG viewport discrepancy: " + name)
        per_width[width] += 1
        manifest.append({
            "id": name, "realm": data["realm"], "width": width, "height": height,
            "png": str(png.relative_to(root)),
            "sha256": sha256(png.read_bytes()).hexdigest()
        })
    require(all(per_width[width] == len(EXPECTED) for width in WIDTHS),
            "missing one or more views for a required viewport")
    png_files = sorted(root.rglob("*.png"))
    require(len(png_files) == len(files), "PNG/JSON mismatch or orphan image")
    final = {
        "status": "PASS_synthetic_visual_evidence_only",
        "runnerSha": sha_expected,
        "synthetic": True,
        "screenshots": len(files),
        "perViewport": dict(sorted(per_width.items())),
        "backendAuthorizationVerified": False,
        "finalAestheticNineOfTenCertified": False,
        "files": manifest
    }
    (root / "F03_EVIDENCE_MANIFEST.json").write_text(
        json.dumps(final, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"F03 visual evidence PASS: {len(files)} PNG/JSON pairs, 5 widths, "
          f"6 states each, SHA {sha_expected[:12]}")


if __name__ == "__main__":
    try:
        require(len(sys.argv) == 2, "pass only result directory")
        main(Path(sys.argv[1]))
    except (OSError, ValueError, TypeError, AssertionError, KeyError) as exc:
        print("FAIL:", exc, file=sys.stderr)
        raise SystemExit(1) from exc
