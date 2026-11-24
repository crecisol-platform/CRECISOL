#!/usr/bin/env python3
"""CRECISOL C05 F06 — verify 14 entire interfaces in 5 real Chromium viewports.
PNG/JSON evidence only. This is not a subjective premium score or authorization test.
"""
from __future__ import annotations
from pathlib import Path
from hashlib import sha256
from collections import Counter
import json
import os
import struct
import sys

SCREENS = (
    "guest-login", "guest-register", "guest-recovery",
    "account-resumen", "account-perfil", "account-llaves",
    "account-seguridad", "account-historial", "account-administracion",
    "operator-casos", "operator-sesiones", "operator-perfil",
    "operator-monitor", "operator-seguridad",
)
WIDTHS = (320, 390, 768, 1024, 1440)

def gate(ok: bool, issue: str) -> None:
    if not ok:
        raise AssertionError("F06 PREMIUM EVIDENCE NO_GO: " + issue)

def audit(root: Path) -> None:
    sha = os.environ.get("GITHUB_SHA", "")
    gate(len(sha) == 40 and all(c in "0123456789abcdef" for c in sha), "runner SHA missing")
    gate(root.is_dir(), "Chromium evidence directory missing")
    jsons = sorted(root.rglob("premium-*.json"))
    gate(len(jsons) == len(SCREENS) * len(WIDTHS), f"expected 70 JSON cases; found {len(jsons)}")
    seen: set[tuple[str, int]] = set()
    sizes: Counter[int] = Counter()
    items = []
    for path in jsons:
        obj = json.loads(path.read_text("utf-8"))
        scene, view = obj.get("surface"), obj.get("viewport")
        width = view.get("width") if isinstance(view, dict) else None
        key = (scene, width)
        gate(scene in SCREENS and width in WIDTHS and key not in seen,
             "invalid or repeated scene/viewport " + repr(key))
        seen.add(key)
        sizes[width] += 1
        name = f"premium-{scene}-{width}"
        gate(obj.get("name") == name, "wrong screenshot identity")
        gate(obj.get("runnerSha") == sha and obj.get("synthetic") is True,
             "SHA or test realm mismatch")
        gate(obj.get("decision") == "pending-human-visual-review",
             "synthetic harness must not assign human premium rating")
        image = path.with_name(name + ".png")
        gate(image.is_file() and image.stat().st_size > 1_024, "PNG missing: " + name)
        png = image.read_bytes()
        gate(png[:8] == b"\x89PNG\r\n\x1a\n" and png[12:16] == b"IHDR",
             "PNG signature invalid: " + name)
        w,h = struct.unpack(">II", png[16:24])
        gate(w == width and h >= 300, "PNG has wrong width/height: " + name)
        items.append({"screen":scene,"width":width,"height":h,
                      "path":str(image.relative_to(root)),"sha256":sha256(png).hexdigest()})
    gate(all(sizes[w] == len(SCREENS) for w in WIDTHS), "incomplete per-size matrix")
    gate(len(seen) == 70, "incomplete per-screen matrix")
    report = {"status":"PASS_CHROMIUM_EVIDENCE_ONLY","sha":sha,
              "screens":len(SCREENS),"sizes":list(WIDTHS),"pngCount":len(items),
              "synthetic":True,"humanPremiumRating":"PENDING",
              "authorizationProvenByScreenshots":False,"items":items}
    (root / "F06_PREMIUM_MANIFEST.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2), "utf-8")
    print("F06 PREMIUM EVIDENCE PASS: 14 interfaces x 5 widths = 70 PNG/JSON; SHA " + sha[:12])

if __name__ == "__main__":
    try:
        gate(len(sys.argv) == 2, "usage: premium_evidence.py <test-results>")
        audit(Path(sys.argv[1]))
    except (AssertionError,OSError,TypeError,ValueError,KeyError) as error:
        print(error, file=sys.stderr)
        raise SystemExit(1) from error
