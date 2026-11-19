#!/usr/bin/env python3
"""C05 UX F01 verify REAL PNG/metadata evidence. No screenshot is a backend auth test."""
import json
import re
import struct
import sys
from collections import Counter
from pathlib import Path

BASE = "f61a79d242288c686a88a7c3dd711f2be9a48c18"
EXPECTED = {320: 63, 390: 63, 768: 63, 1440: 63}
REQUIRED = (
    "public-ingresar", "public-registro", "public-confirmar",
    "public-recuperar", "public-invitacion", "public-acceso-existente",
    "account-resumen", "account-perfil", "account-llaves",
    "account-seguridad", "account-administracion",
    "account-mfa-challenge", "account-mfa-enrollment-required",
    "account-mfa-enrollment-qr", "account-mfa-enrollment-manual",
    "operator-login", "operator-recovery-dialog", "operator-mfa-challenge",
    "operator-Casos", "operator-Sesiones", "operator-Perfil",
    "operator-Seguridad", "operator-Monitor", "operator-error",
    "operator-session-expired", "operator-policy-readonly",
    "operator-sessions-empty", "operator-monitor-empty",
    "account-error", "account-loading", "account-success",
    "account-session-cleared",
)


def require(value: bool, why: str) -> None:
    if not value:
        raise AssertionError("C05 F01 visual NO-GO: " + why)


def dimensions(path: Path) -> tuple[int, int]:
    with path.open("rb") as f:
        raw = f.read(24)
    require(raw[:8] == b"\x89PNG\r\n\x1a\n" and raw[12:16] == b"IHDR",
            "invalid PNG header: " + str(path))
    return struct.unpack(">II", raw[16:24])


def main(root: Path) -> None:
    evidence = sorted(root.rglob("*.json"))
    require(len(evidence) >= 252,
            f"expected >=252 visual states before scroll variants, got {len(evidence)}")
    rows = []
    for file in evidence:
        doc = json.loads(file.read_text(encoding="utf-8"))
        require(doc.get("sourceSha") == BASE, "unverified source SHA: " + str(file))
        require(doc.get("synthetic") is True, "non-synthetic capture: " + str(file))
        require(doc.get("runnerSha") and doc["runnerSha"] != "local",
                "missing exact GitHub Actions SHA: " + str(file))
        require(doc.get("bodyScrollX", 99) <= 2,
                "horizontal page overflow: " + str(file))
        png = file.parent / str(doc.get("screenshot", ""))
        require(png.is_file(), "missing screenshot: " + str(png))
        width, height = dimensions(png)
        viewport = doc.get("viewport") or {}
        require(width == viewport.get("width"), "PNG viewport width mismatch: " + str(png))
        require(height >= viewport.get("height", 10**9), "PNG height smaller than viewport: " + str(png))
        require(doc.get("route", "").startswith("/cuenta"), "outside C05 route")
        require(doc.get("dialogsOpen") in (0, 1), "invalid number of native dialogs")
        require(not (doc.get("dialogsOpen") == 1 and not doc.get("dialog")),
                "missing modal viewport/scroll metadata")
        require(not (doc.get("dialogsOpen") == 0 and doc.get("dialog")),
                "closed modal claims open geometry")
        rows.append(doc)
    counts = Counter((row.get("viewport") or {}).get("width") for row in rows)
    require(set(counts) == set(EXPECTED), f"unexpected viewport set: {counts}")
    for width, count in counts.items():
        require(count >= EXPECTED[width], f"missing states for {width}: {count}")
        ids = [row["id"] for row in rows if row["viewport"]["width"] == width]
        for prefix in REQUIRED:
            require(any(identifier.startswith(prefix) for identifier in ids),
                    f"{width}px missing required scenario: {prefix}")
        tops = [row for row in rows if row["viewport"]["width"] == width
                and row["dialogsOpen"] == 1 and "-scrolled-footer" not in row["id"]]
        require(len(tops) == 27,
                f"{width}px should document all 27 modal families; found {len(tops)}")
    for width in (320, 390):
        require(any("-scrolled-footer" in row["id"] and row["viewport"]["width"] == width
                    and (row["dialog"] or {}).get("scrollTop", 0) > 0 for row in rows),
                f"no scrolled long-modal evidence for {width}px")
    ids = [row["id"] for row in rows]
    require(len(set(ids)) == len(ids), "duplicate evidence IDs")
    require(len({row["runnerSha"] for row in rows}) == 1,
            "inconsistent workflow HEAD SHA in screenshots")
    summary = {
        "status": "PASS_evidence_integrity_only",
        "runnerSha": rows[0]["runnerSha"],
        "sourceSha": BASE, "synthetic": True,
        "screenshots": len(rows), "countByViewport": counts,
        "topModals": sum(x["dialogsOpen"] == 1 and "-scrolled-footer" not in x["id"] for x in rows),
        "scrolledModals": sum("-scrolled-footer" in x["id"] for x in rows),
        "horizontalPageOverflowOver2": sum(x["bodyScrollX"] > 2 for x in rows),
        "backendSecurityVerifiedByThisAudit": False
    }
    report = root / "F01_EVIDENCE_INTEGRITY_SUMMARY.json"
    report.write_text(json.dumps(summary, indent=2, sort_keys=True), encoding="utf-8")
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    try:
        require(len(sys.argv) == 2, "pass only screenshot output directory")
        main(Path(sys.argv[1]))
    except (OSError, ValueError, AssertionError, KeyError, TypeError) as exc:
        print("F01 evidence integrity FAIL:", exc, file=sys.stderr)
        raise SystemExit(1) from exc
