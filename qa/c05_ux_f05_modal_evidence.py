#!/usr/bin/env python3
"""C05 UX F05 evidence: 55 exact-SHA synthetic PNG/JSON screenshots, individual files."""
from pathlib import Path
from hashlib import sha256
from collections import Counter
import json, os, struct, sys

SCENES = (
    "recuperacion-operador-solicitud", "recuperacion-operador-doble-control",
    "mfa-operador-verificacion", "mfa-operador-vinculacion",
    "passkey-registro", "passkey-revocacion", "cambio-contrasena",
    "aprobacion-membresia", "invitacion", "revocacion-sesion", "renovacion-sesion")
SIZES = (320,390,768,1024,1440)

def need(ok, reason):
    if not ok: raise AssertionError("F05 EVIDENCE NO-GO: " + reason)

def main(root: Path):
    need(root.is_dir(), "missing Chromium F05 output")
    sha = os.getenv("GITHUB_SHA", "")
    need(len(sha) == 40, "runner exact commit SHA missing")
    files = sorted(root.rglob("modal-*.json"))
    need(len(files) == len(SCENES) * len(SIZES), f"expected 55 JSON files, found {len(files)}")
    checks = set()
    counts = Counter()
    manifest = []
    for p in files:
        v = json.loads(p.read_text("utf-8"))
        scene, width = v.get("scenario"), v.get("viewport",{}).get("width")
        expected = f"modal-{scene}-{width}"
        need(scene in SCENES and width in SIZES and v.get("key") == expected,
             f"unexpected filename or scenario: {p.name}")
        need((scene,width) not in checks, "duplicate screenshot scenario/size")
        checks.add((scene,width)); counts[width] += 1
        need(v.get("synthetic") is True and v.get("runnerSha") == sha, "not synthetic or wrong HEAD")
        need(v.get("png") == expected + ".png", "wrong PNG reference")
        need(v.get("metrics",{}).get("overflow",99) <= 2, "horizontal overflow in modal")
        target = p.with_name(expected + ".png")
        need(target.exists() and target.stat().st_size > 1024, "PNG missing or empty")
        raw = target.read_bytes()
        need(raw[:8] == b"\x89PNG\r\n\x1a\n" and raw[12:16] == b"IHDR", "invalid PNG signature")
        size_x,size_y = struct.unpack(">II",raw[16:24])
        need(size_x == width and size_y == v["viewport"]["height"], "PNG size mismatch")
        manifest.append({"scene":scene,"width":width,"filename":str(target.relative_to(root)),
                         "sha256":sha256(raw).hexdigest()})
    need(all(counts[size] == len(SCENES) for size in SIZES), "incomplete width matrix")
    report = {"result":"PASS_F05_INDIVIDUAL_MODAL_EVIDENCE", "commit":sha,
      "synthetic":True, "backendAuthorizationDemonstratedByImages":False,
      "imageCount":len(manifest),"perWidth":dict(sorted(counts.items())),"files":manifest}
    (root / "F05_MODAL_MANIFEST.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),"utf-8")
    print(f"F05 evidence PASS: {len(manifest)} individual full-resolution PNG, zero SHA mismatch, {sha[:12]}")
if __name__ == "__main__":
    try:
        need(len(sys.argv)==2,"supply F05 output folder")
        main(Path(sys.argv[1]))
    except (AssertionError,OSError,TypeError,ValueError,KeyError) as error:
        print(error,file=sys.stderr); raise SystemExit(1) from error
