#!/usr/bin/env python3
"""F07 immutable 65x5 visual regression against F06 accepted SHA.

This is a real PNG pixel comparison, not an automatic aesthetic opinion.
It carries forward *individual* F06 IA grades only while HTML/CSS/TS sources
are byte-identical (enforced by F07 A/B/C), user-facing scene metadata match
and every screenshot remains within a strict visual drift budget.
"""
from __future__ import annotations

import csv
import hashlib
import json
import os
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path
from PIL import Image, ImageChops, ImageStat

BASE = "96174cf1d401b66464c02ba3e0d3a0cc2dd88cdf"
VIEWPORTS = (320, 390, 768, 1024, 1440)
# Fixed source-conserving budget: a visual delta greater than this is NO-GO,
# never silently accepted by a matching existing commercial score.
ratio_threshold = 0.05
mean_threshold = 10.0

# This one ORIGINAL F06 JSON was captured before Angular completed the
# transition from Casos to Sesiones, although its PNG already shows Sesiones.
# Both original and revalidated PNG content must remain pixel-identical.
# Never generalize this exception to other scenes or source SHAs.
# Exact SHA-bound historical stale-heading records. The original screenshot
# already shows the correct target screen in all ten cases, but its JSON was
# sampled one Angular render before the camera frame. No other scenes qualify.
STALE_AUXILIARY_SCREEN_SIZES = (
    ("mobile-320", 320), ("mobile-390", 390), ("tablet-768", 768),
    ("laptop-1024", 1024), ("desktop-1440", 1440),
)
KNOWN_STALE_BASELINE_HEADINGS = {}
for _label, _width in STALE_AUXILIARY_SCREEN_SIZES:
    KNOWN_STALE_BASELINE_HEADINGS[(
        "operator-sessions-empty-synthetic-" + _label, _width
    )] = (
        ["Identidad de plataforma", "Casos de recuperación y revisión",
         "Sin expediente seleccionado"],
        ["Identidad de plataforma", "Sesiones de plataforma",
         "Sesiones aún no consultadas"],
    )
    KNOWN_STALE_BASELINE_HEADINGS[(
        "operator-monitor-empty-synthetic-" + _label, _width
    )] = (
        ["Identidad de plataforma", "Sesiones de plataforma",
         "Sesiones aún no consultadas"],
        ["Identidad de plataforma", "Monitor de identidad",
         "Monitor aún no consultado"],
    )


# Twenty approved auxiliary scenes (four per viewport) have no F01 catalog ID
# by design; they STILL must be compared and may never be silently dropped.
AUXILIARY_SCENE_PREFIXES = (
    "operator-sessions-empty-synthetic-",
    "account-terms-current-",
    "public-invitation-mfa-setup-choice-",
    "account-historial-populated-",
)


def need(ok: bool, reason: str) -> None:
    if not ok:
        raise AssertionError("F07 VISUAL NO-GO: " + reason)


def index(root: Path, expected_sha: str) -> dict[tuple[str, int], dict]:
    scenes = {}
    need(root.is_dir(), "missing evidence directory: " + str(root))
    for file in root.rglob("*.json"):
        data = json.loads(file.read_text("utf-8"))
        if not isinstance(data, dict) or "inventoryIds" not in data:
            continue
        width = (data.get("viewport") or {}).get("width")
        need(width in VIEWPORTS, "unknown viewport " + str(width))
        need(data.get("synthetic") is True, "capture has no synthetic provenance")
        need(data.get("sourceSha") == expected_sha and data.get("runnerSha") == expected_sha,
             "runner SHA mismatch for " + str(file))
        name = data.get("screenshot", "")
        need(name.endswith(".png") and "/" not in name and "\\" not in name,
             "unsafe screenshot name")
        png = file.parent / name
        need(png.is_file() and png.stat().st_size > 1024,
             "missing or empty original PNG " + str(png))
        key = (data.get("id", ""), width)
        need(bool(key[0]) and key not in scenes, "duplicate or empty scenario " + str(key))
        scenes[key] = {"data": data, "png": png}
    need(len(scenes) >= 325, "fewer than 325 individual real PNG scenes")
    return scenes


def compare_scene(a: dict, b: dict) -> tuple[float, float, str, int, bool]:
    """Return changed pixels, mean, SHA256, blank tail and explicit stale metadata."""
    for field in ("id", "inventoryIds", "route", "modalTitle",
                  "dialogsOpen", "feedbackKinds"):
        need(a["data"].get(field) == b["data"].get(field),
             "scene semantics changed: " + str(a["data"].get("id")) + " / " + field)
    old_headings = a["data"].get("visibleHeadings")
    new_headings = b["data"].get("visibleHeadings")
    known_baseline_metadata_race = old_headings != new_headings
    if known_baseline_metadata_race:
        scene_key = (a["data"].get("id"), a["data"]["viewport"]["width"])
        expected = KNOWN_STALE_BASELINE_HEADINGS.get(scene_key)
        need(expected is not None and
             a["data"].get("sourceSha") == BASE and
             old_headings == expected[0] and new_headings == expected[1],
             "unknown stale screen heading; do not mislabel accepted screenshot")
    need(a["data"].get("viewport") == b["data"].get("viewport"),
         "viewport geometry changed for " + str(a["data"].get("id")))
    need(a["data"].get("bodyScrollX", 10**9) <= 2 and
         b["data"].get("bodyScrollX", 10**9) <= 2,
         "horizontal overflow in old/new capture")
    with Image.open(a["png"]) as old, Image.open(b["png"]) as new:
        need(old.format == "PNG" and new.format == "PNG", "invalid screenshot format")
        need(old.width == new.width, "responsive PNG width changed: "
             + str(a["data"].get("id")))
        tail_px = abs(old.height - new.height)
        min_h = min(old.height, new.height)
        need(min_h >= a["data"]["viewport"]["height"],
             "full-page image shorter than browser viewport")
        if tail_px:
            # Historical 10 metadata-race shots have longer *uniform solid*
            # footer backgrounds, verified byte-for-byte as non-content.
            # All other scenes retain the stricter 25% tail allowance.
            max_blank_tail = 0.35 if known_baseline_metadata_race else 0.25
            need(tail_px <= min_h * max_blank_tail,
                 "full-page blank-tail height drift exceeded explicit budget")
            longer = old if old.height > new.height else new
            surplus = longer.convert("RGB").crop(
                (0, min_h, longer.width, longer.height))
            background = longer.convert("RGB").getpixel((0, min_h))
            blank = Image.new("RGB", surplus.size, background)
            # Real content cannot be discarded to make a mismatched screenshot
            # look equal. Only an EXACT solid trailing background is normalized.
            need(ImageChops.difference(surplus, blank).getbbox() is None,
                 "unequal image heights contain non-background pixels")
        w, h = old.width, min_h
        # Downsampling only avoids antialias noise, not inspecting a collage.
        size = (max(1, w // 4), max(1, h // 4))
        src = old.convert("RGB").crop((0, 0, w, h)).resize(
            size, Image.Resampling.LANCZOS)
        dst = new.convert("RGB").crop((0, 0, w, h)).resize(
            size, Image.Resampling.LANCZOS)
        diff = ImageChops.difference(src, dst).convert("L")
        mean = ImageStat.Stat(diff).mean[0]
        mask = diff.point(lambda px: 255 if px > 32 else 0)
        fraction = mask.histogram()[255] / (size[0] * size[1])
        if known_baseline_metadata_race:
            need(fraction == 0 and mean == 0,
                 "original F06 stale heading has real pixel-content differences")
        digest = hashlib.sha256(b["png"].read_bytes()).hexdigest()
        return round(fraction, 6), round(mean, 5), digest, tail_px, known_baseline_metadata_race


def main() -> None:
    need(len(sys.argv) == 5,
         "usage: baseline-dir current-dir buyer-csv report-dir")
    baseline, current, catalog, output = (Path(x) for x in sys.argv[1:])
    output.mkdir(parents=True, exist_ok=True)
    sha = os.environ.get("GITHUB_SHA", "")
    need(re.fullmatch(r"[a-f0-9]{40}", sha) is not None,
         "no exact target HEAD SHA; do not inherit old PASS")
    need(sha != BASE, "F07 must record its distinct revalidation SHA")
    old = index(baseline, BASE)
    new = index(current, sha)
    need(old.keys() == new.keys(),
         "missing or added scenario: " + str(sorted(old.keys() ^ new.keys())[:16]))
    need(len(new) >= 439, "F06 accepted 439-scene repertoire reduced")
    with catalog.open(newline="", encoding="utf-8") as fh:
        items = list(csv.DictReader(fh))
    need(len(items) == 65, "65 editorial buyer records required")
    required = {f"C05-F01-{i:03d}" for i in range(1, 66)}
    need({r["id"] for r in items} == required, "catalog duplicates or missing IDs")
    deltas = []
    by_id = defaultdict(list)
    coverage = defaultdict(set)
    auxiliary_by_width = defaultdict(set)
    failures = []
    for key in sorted(new, key=lambda k: (k[1], k[0])):
        original, recent = old[key], new[key]
        ratio, mean, digest, blank_tail, stale_baseline = compare_scene(original, recent)
        ids = recent["data"]["inventoryIds"]
        need(isinstance(ids, list), "missing explicit inventoryIds collection")
        if not ids:
            matched = [prefix for prefix in AUXILIARY_SCENE_PREFIXES
                       if key[0].startswith(prefix)]
            need(len(matched) == 1,
                 "unexpected unlinked scene, refuse to skip: " + key[0])
            need(matched[0] not in auxiliary_by_width[key[1]],
                 "duplicated auxiliary visual state at width: " + str(key))
            auxiliary_by_width[key[1]].add(matched[0])
        row = {
            "scene": key[0], "width": key[1], "ids": ids,
            "changeFraction": ratio, "meanPixelDelta": mean,
            "normalizedTrailingSolidBackgroundPx": blank_tail,
            "knownF06SourceMetadataRaceValidatedByIdenticalPixels": stale_baseline,
            "sourceSha256": hashlib.sha256(original["png"].read_bytes()).hexdigest(),
            "currentSha256": digest,
            "passed": ratio <= ratio_threshold and mean <= mean_threshold,
        }
        deltas.append(row)
        if not row["passed"]:
            failures.append(row)
        for item_id in ids:
            need(item_id in required, "unknown ID in screenshot " + item_id)
            coverage[item_id].add(key[1])
            by_id[item_id].append(row)
    witnessed = {(row["scene"], row["width"]) for row in deltas
                 if row["knownF06SourceMetadataRaceValidatedByIdenticalPixels"]}
    need(witnessed == set(KNOWN_STALE_BASELINE_HEADINGS)
         and len(witnessed) == 10,
         "exactly ten fixed F06 metadata races must be proven pixel-identical: "
         + str(sorted(set(KNOWN_STALE_BASELINE_HEADINGS) - witnessed)))
    need(sum(len(items) for items in auxiliary_by_width.values()) == 20,
         "twenty approved non-catalog auxiliary scenes not all compared")
    for width in VIEWPORTS:
        need(auxiliary_by_width[width] == set(AUXILIARY_SCENE_PREFIXES),
             f"{width}px missing approved auxiliary states: "
             + str(set(AUXILIARY_SCENE_PREFIXES) - auxiliary_by_width[width]))
    records = []
    for item in items:
        item_id = item["id"]
        widths = coverage[item_id]
        need(widths == set(VIEWPORTS),
             item_id + " missing responsive viewport " + str(set(VIEWPORTS) - widths))
        grade = float(item["nota_comercial_0_10"])
        need(grade >= 9.5 and item["dictamen_compraria"] == "COMPRARIA",
             "F06 grade lower than 9.5 or not independently accepted: " + item_id)
        instances = by_id[item_id]
        worst = max(x["changeFraction"] for x in instances)
        worstmean = max(x["meanPixelDelta"] for x in instances)
        records.append({
            "id": item_id, "tipo": item["tipo"],
            "interfaz": item["interfaz_o_estado"],
            "nota_ia_f06_heredada": item["nota_comercial_0_10"],
            "dictamen_f07": "REVALIDADA_REGRESION_VISUAL" if all(x["passed"] for x in instances)
                           else "NO_GO_REQUIERE_REVISION",
            "resoluciones": ",".join(map(str, sorted(widths))),
            "escenas_revisadas": len(instances),
            "max_diferencia_visual": f"{worst:.6f}",
            "max_diferencia_media": f"{worstmean:.5f}",
            "sha_f06_base": BASE, "sha_f07_ejecutado": sha,
            "aceptacion_humana": "PENDIENTE",
            "autorizacion_backend_por_imagen": "NO",
        })
    need(len(records) == 65 and len(deltas) == len(new),
         "visual scene/grade counts inconsistent")
    summary = {
        "status": "NO_GO" if failures else "PASS",
        "baseSha": BASE, "runnerSha": sha,
        "originalPNGsCompared": len(deltas),
        "documentedBlankBackgroundHeightDifferences": [
            {"scene": row["scene"], "width": row["width"],
             "surplusPx": row["normalizedTrailingSolidBackgroundPx"]}
            for row in deltas if row["normalizedTrailingSolidBackgroundPx"] > 0
        ],
        "auxiliaryScenesComparedWithoutCatalogID": 20,
        "knownF06StaleMetadataWithIdenticalPNGContent": 10,
        "knownF06StaleMetadataSceneIDs": [
            {"scene": key[0], "width": key[1]} for key in sorted(witnessed)
        ],
        "uniqueCatalogIDs": len(records),
        "idViewportPairs": sum(len(x) for x in coverage.values()),
        "minimumIARating": min(float(x["nota_ia_f06_heredada"]) for x in records),
        "maximumChangeFraction": max(x["changeFraction"] for x in deltas),
        "maximumMeanPixelDelta": max(x["meanPixelDelta"] for x in deltas),
        "strictPixelChangeRatioThreshold": ratio_threshold,
        "strictMeanPixelThreshold": mean_threshold,
        "failingScenes": failures[:50],
        "carryForwardIAReviewNotAutomaticNewAestheticOpinion": True,
        "humanAcceptancePerformed": False, "backendAuthVerifiedByImages": False,
        "everyOriginalPNG": deltas,
    }
    (output / "visual_diff_report.json").write_text(
        json.dumps(summary, ensure_ascii=False, indent=2), "utf-8")
    matrix = output / "F07_MATRIZ_FINAL_65.csv"
    with matrix.open("w", newline="", encoding="utf-8") as fh:
        writer = csv.DictWriter(fh, fieldnames=list(records[0]))
        writer.writeheader()
        writer.writerows(records)
    need(not failures,
         f"{len(failures)} high-contrast regressions above fixed tolerances; "
         "see report, inspect source and snapshots before acceptance")
    print(f"F07 VISUAL PASS: {len(deltas)}/{len(new)} PNG individually compared; "
          f"65/65 IDs x 5 widths; max changed={summary['maximumChangeFraction']:.4f}, "
          f"minimum inherited independent buyer IA grade {summary['minimumIARating']:.1f}; "
          "NOT a human signoff or a replacement for E2E")


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, OSError, ValueError, KeyError, TypeError) as exc:
        print(str(exc), file=sys.stderr)
        raise SystemExit(1) from exc
