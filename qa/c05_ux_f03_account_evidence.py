#!/usr/bin/env python3
"""Validate 30 authenticated-view Chromium PNG/JSON pairs on the exact git SHA.

Visual samples use synthetic identities and do not prove subjective 9/10,
live backend role enforcement, IDOR isolation, MFA hardware or tenant policies.
"""
from collections import Counter
from hashlib import sha256
from pathlib import Path
import json
import os
import struct
import sys

VIEWS = ("resumen", "perfil", "llaves", "seguridad", "historial", "administracion")
SIZES = (320, 390, 768, 1024, 1440)


def require(ok: bool, message: str) -> None:
    if not ok:
        raise AssertionError("C05 F03 ACCOUNT EVIDENCE NO-GO: " + message)


def main(folder: Path) -> None:
    require(folder.is_dir(), "missing account QA folder")
    sha = os.environ.get("GITHUB_SHA", "")
    require(len(sha) == 40, "GitHub HEAD SHA absent")
    reports = sorted(x for x in folder.rglob("*.json") if x.name != ".last-run.json")
    require(len(reports) == 30, f"expected 30 JSON reports; got {len(reports)}")
    names: set[str] = set()
    widths: Counter[int] = Counter()
    evidence = []
    for report in reports:
        data = json.loads(report.read_text("utf-8"))
        name = data.get("id")
        require(isinstance(name, str) and name not in names, "duplicate or invalid snapshot")
        names.add(name)
        require(data.get("synthetic") is True and data.get("runnerSha") == sha,
                "wrong commit or non-synthetic data in " + name)
        width = data.get("viewport", {}).get("width")
        height = data.get("viewport", {}).get("height")
        view = data.get("view")
        require(width in SIZES and view in VIEWS, "invalid view/viewport")
        require(name == f"account-{view}-{width}", "unexpected screenshot id " + name)
        require(data.get("pathname") == "/cuenta" and
                data.get("query") == "?vista=" + view,
                "wrong account route in " + name)
        require(isinstance(data.get("documentOverflow"), int) and
                data["documentOverflow"] <= 2,
                "horizontal overflow in " + name)
        file_name = data.get("png")
        require(file_name == name + ".png", "unsafe screenshot reference")
        png = report.parent / file_name
        require(png.is_file() and png.stat().st_size >= 1024, "PNG missing for " + name)
        raw = png.read_bytes()
        require(raw[:8] == b"\x89PNG\r\n\x1a\n" and raw[12:16] == b"IHDR",
                "invalid PNG in " + name)
        image_width, image_height = struct.unpack(">II", raw[16:24])
        require(image_width == width and image_height >= height,
                "PNG width/height mismatch in " + name)
        require(isinstance(data.get("heading"), str) and len(data["heading"]) > 3,
                "missing screen heading " + name)
        widths[width] += 1
        evidence.append({
            "id": name, "width": width, "view": view,
            "sha256": sha256(raw).hexdigest(),
            "filename": str(png.relative_to(folder))
        })
    for width in SIZES:
        require(widths[width] == 6, f"missing account view at {width}px")
    report = {
        "status": "PASS_SYNTHETIC_F03_ACCOUNT_EVIDENCE",
        "runnerSha": sha,
        "totalViews": 6,
        "screenshots": 30,
        "sizes": dict(sorted(widths.items())),
        "synthetic": True,
        "realBackendAuthorizationTested": False,
        "individualNineOfTenAestheticReviewComplete": False,
        "evidence": evidence
    }
    (folder / "F03_ACCOUNT_MANIFEST.json").write_text(
        json.dumps(report, indent=2, ensure_ascii=False), "utf-8")
    print(f"F03 account evidence PASS: 30 PNG/JSON, 6 views, 5 widths, SHA {sha[:12]}")


if __name__ == "__main__":
    try:
        require(len(sys.argv) == 2, "use account result folder")
        main(Path(sys.argv[1]))
    except (AssertionError, OSError, ValueError, KeyError, TypeError) as error:
        print("FAIL:", error, file=sys.stderr)
        raise SystemExit(1) from error
