#!/usr/bin/env python3
"""Strict F06 65-surface evidence integrity; NOT a visual score or backend auth test."""
from __future__ import annotations
import csv
import hashlib
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
    # Ratings are optional, but every issued score requires evidence and
    # an explicit buyer decision. Never auto-grade based on browser PASS.
    for item in items:
        score=item.get("nota_comercial_0_10", "").strip()
        decision=item.get("dictamen_compraria", "").strip()
        if not score:
            check(decision == "PENDIENTE",
                  "no emitir dictamen sin calificación: " + item["id"])
            continue
        check(item.get("defecto_observado", "").strip()
              and item.get("evidencia_png_por_viewport", "").strip()
              and item.get("seguridad_y_privacidad", "").strip(),
              "calificación sin argumento/evidencia/seguridad: "+item["id"])
        value=float(score)
        check(0 <= value <= 10, "calificación fuera de escala: "+item["id"])
        check(decision in {"COMPRARIA", "NO_COMPRARIA"},
              "dictamen no emitido: "+item["id"])
        check((value >= 9.5) == (decision == "COMPRARIA"),
              "umbral comercial inconsistente: "+item["id"])
    viewports={320,390,768,1024,1440}
    found=defaultdict(set)
    scrolled=defaultdict(int)
    catalog_types={r["id"]:r["tipo"] for r in items}
    scanned=0
    pngs=set()
    diagnostics=[]
    # A green inventory must not count two distinct UX states with the same image.
    distinct_scenes = (
        ("account-error-synthetic-", "account-success-synthetic-"),
        ("account-loading-synthetic-", "account-resumen-empty-"),
        ("operator-restoring-synthetic-", "operator-cases-empty-synthetic-"),
        ("operator-session-expired-simulated-", "operator-login-"),
    )
    semantic_scenes = {
        "account-loading-synthetic-": "pending",
        "account-error-synthetic-": "error",
        "account-success-synthetic-": "success",
        "account-session-cleared-synthetic-": "info",
        "operator-restoring-synthetic-": "pending",
        "operator-error-synthetic-": "error",
        "operator-session-expired-simulated-": "info",
    }
    scene_hashes=defaultdict(dict)
    operator_tabs_expected = {
        "Casos": "Casos de recuperación y revisión",
        "Sesiones": "Sesiones de plataforma",
        "Perfil": "Mi perfil de operador",
        "Seguridad": "Seguridad de plataforma",
        "Monitor": "Monitor de identidad",
    }
    operator_tab_screens = {
        "mobile-320": 320, "mobile-390": 390, "tablet-768": 768,
        "laptop-1024": 1024, "desktop-1440": 1440
    }
    operator_tabs_seen = defaultdict(set)
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
        check(raw[:8]==b"\x89PNG\r\n\x1a\n" and raw[12:16]==b"IHDR",
              "encabezado PNG inválido: "+str(png))
        pw,ph=struct.unpack(">II",raw[16:24])
        check(pw==width and ph>=viewport.get("height",10**9),
              "geometría PNG no coincide: "+str(png))
        check(png.as_posix() not in pngs,"PNG duplicado: "+str(png))
        pngs.add(png.as_posix())
        scene_id=str(data.get("id", ""))
        operator_view = re.fullmatch(
            r"operator-(Casos|Sesiones|Perfil|Seguridad|Monitor)-"
            r"(mobile-320|mobile-390|tablet-768|laptop-1024|desktop-1440)",
            scene_id,
        )
        if operator_view:
            tab, size_name = operator_view.groups()
            check(width == operator_tab_screens[size_name],
                  "pantalla de operador con ancho mal etiquetado: " + scene_id)
            check(operator_tabs_expected[tab] in (data.get("visibleHeadings") or []),
                  "captura de pestaña " + tab + " refleja otra vista: " + scene_id)
            check(tab not in operator_tabs_seen[width],
                  "pestaña de operador duplicada: " + scene_id)
            operator_tabs_seen[width].add(tab)
        for scene_prefix in set(semantic_scenes) | {prefix for pair in distinct_scenes for prefix in pair}:
            if scene_id.startswith(scene_prefix):
                kinds=data.get("feedbackKinds")
                if scene_prefix in semantic_scenes:
                    check(isinstance(kinds, list) and kinds == [semantic_scenes[scene_prefix]],
                          f"{width}px {scene_id}: estado semántico visible ausente o duplicado ({kinds})")
                check(scene_prefix not in scene_hashes[width],
                      f"{width}px captura duplicada de {scene_prefix}")
                scene_hashes[width][scene_prefix]=hashlib.sha256(png.read_bytes()).hexdigest()
        for surface in data["inventoryIds"]:
            check(surface in required, "id desconocido "+str(surface)+" en "+str(file))
            if catalog_types[surface] == "modal":
                check(data.get("dialogsOpen")==1 and bool(data.get("modalTitle")),
                      "modal inventariado sin dialogo visible: "+str(file))
            if surface == "C05-F01-014":
                check(scene_id.startswith("account-terms-current-loaded-")
                      and data.get("route") == "/cuenta?vista=ingresar"
                      and "Condiciones qa-f01" in (data.get("visibleHeadings") or []),
                      "F01-014 requiere condiciones cargadas, no solo el menú de acceso")
            if surface == "C05-F01-057":
                check(scene_id.startswith("account-historial-empty-")
                      and data.get("route") == "/cuenta?vista=historial"
                      and "Sin actividad disponible" in (data.get("visibleHeadings") or []),
                      "F01-057 debe mostrar historial vacío real; no perfil sin consultar")
            found[width].add(surface)
        if data.get("dialogsOpen")==1:
            modal=data.get("dialog") or {}
            check(modal.get("inViewport") is True, "modal recortado: "+str(file))
            check(modal.get("horizontalOverflow",99)<=2, "modal con overflow: "+str(file))
            check(modal.get("footerInViewport") is True,
                  "pie del modal fuera de pantalla: "+str(file))
        if "-scrolled-footer" in data.get("id", ""):
            check(data.get("dialogsOpen")==1 and
                  (data.get("dialog") or {}).get("scrollTop", 0)>0,
                  "captura de scroll que no desplazo el contenido: "+str(file))
            scrolled[width]+=1
    check(scanned>=325,f"capturas insuficientes: {scanned}, requeridas al menos 325")
    for width in sorted(viewports):
        miss=sorted(required-found[width])
        check(scrolled[width]>=1, f"{width}px no tiene prueba del pie de un modal largo")
        check(operator_tabs_seen[width] == set(operator_tabs_expected),
              f"{width}px pestañas de operador faltantes o mal etiquetadas: "
              + str(set(operator_tabs_expected) - operator_tabs_seen[width]))
        for before, after in distinct_scenes:
            check(before in scene_hashes[width] and after in scene_hashes[width],
                  f"{width}px faltan comparaciones semánticas {before}/{after}")
            check(scene_hashes[width][before] != scene_hashes[width][after],
                  f"{width}px {before} y {after}: capturas visualmente idénticas")
        diagnostics.append({"width":width,"captured":len(found[width]),
                            "longDialogsScrolled":scrolled[width],"missing":miss})
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
