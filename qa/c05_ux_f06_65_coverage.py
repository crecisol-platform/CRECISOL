#!/usr/bin/env python3
"""Strict F06 65-surface evidence integrity; NOT a visual score or backend auth test."""
from __future__ import annotations
import csv
import json
import os
import re
import struct
import sys
from collections import defaultdict
from pathlib import Path

def check(ok: bool, why: str) -> None:
    if not ok:
        raise AssertionError("F06-65 NO-GO: "+why)

def main(root: Path, catalog: Path) -> None:
    sha=os.environ.get("GITHUB_SHA", "")
    check(bool(re.fullmatch(r"[a-f0-9]{40}", sha)), "SHA exacto de Actions ausente")
    with catalog.open(newline="", encoding="utf-8") as fh:
        items=list(csv.DictReader(fh))
    check(len(items)==65, f"catálogo incompleto: {len(items)} != 65")
    required={f"C05-F01-{i:03}" for i in range(1,66)}
    actual=[r["id"] for r in items]
    check(set(actual)==required and len(set(actual))==65,
          "IDs C05-F01-001..065 ausentes o duplicados")
    check(all(r["nota_comercial_0_10"]=="" for r in items),
          "las notas no se inventan antes de una revisión de comprador")
    viewports={320,390,768,1024,1440}
    found=defaultdict(set)
    scanned=0
    pngs=set()
    diagnostics=[]
    for file in sorted(root.rglob("*.json")):
        if file.name==".last-run.json":
            continue
        data=json.loads(file.read_text(encoding="utf-8"))
        if not isinstance(data,dict) or "inventoryIds" not in data:
            continue
        scanned+=1
        check(data.get("synthetic") is True, "captura no marcada sintética: "+str(file))
        check(data.get("sourceSha")==sha and data.get("runnerSha")==sha,
              "evidencia sin SHA actual: "+str(file))
        viewport=data.get("viewport") or {}
        width=viewport.get("width")
        check(width in viewports, "viewport inesperado: "+str(width))
        check(data.get("route","").startswith("/cuenta"), "captura fuera de C05: "+str(file))
        check(data.get("bodyScrollX",10**9)<=2,"desbordamiento horizontal: "+str(file))
        png=file.parent/str(data.get("screenshot",""))
        check(png.is_file(),"PNG ausente para "+str(file))
        raw=png.open("rb").read(24)
        check(raw[:8]==b"\\x89PNG\\r\\n\\x1a\\n" and raw[12:16]==b"IHDR",
              "encabezado PNG inválido: "+str(png))
        pw,ph=struct.unpack(">II",raw[16:24])
        check(pw==width and ph>=viewport.get("height",10**9),
              "geometría PNG no coincide: "+str(png))
        check(png.as_posix() not in pngs,"PNG duplicado: "+str(png))
        pngs.add(png.as_posix())
        for surface in data["inventoryIds"]:
            check(surface in required, "id desconocido "+str(surface)+" en "+str(file))
            found[width].add(surface)
        if data.get("dialogsOpen")==1:
            modal=data.get("dialog") or {}
            check(modal.get("inViewport") is True, "modal recortado: "+str(file))
            check(modal.get("horizontalOverflow",99)<=2, "modal con overflow: "+str(file))
    check(scanned>=325,f"capturas insuficientes: {scanned}, requeridas al menos 325")
    for width in sorted(viewports):
        miss=sorted(required-found[width])
        diagnostics.append({"width":width,"captured":len(found[width]),"missing":miss})
    summary={
      "status":"PASS" if all(not r["missing"] for r in diagnostics) else "NO_GO",
      "sha":sha,"catalog":len(items),"screenshots":scanned,
      "viewports":diagnostics,"backendAuthorizationProved":False,
      "commercialRatingCertified":False
    }
    (root/"F06_65_COVERAGE_SUMMARY.json").write_text(
      json.dumps(summary,ensure_ascii=False,indent=2),encoding="utf-8")
    print(json.dumps(summary,ensure_ascii=False,indent=2))
    for report in diagnostics:
        check(not report["missing"], f"{report['width']}px faltan: {report['missing']}")
    print("F06-65 evidence and catalog PASS (NOT rating or security certification)")

if __name__=="__main__":
    try:
        check(len(sys.argv)==3, "usar: results-root catalog.csv")
        main(Path(sys.argv[1]),Path(sys.argv[2]))
    except (OSError,AssertionError,KeyError,TypeError,ValueError) as exc:
        print("F06-65 FAIL:",str(exc),file=sys.stderr)
        raise SystemExit(1) from exc
