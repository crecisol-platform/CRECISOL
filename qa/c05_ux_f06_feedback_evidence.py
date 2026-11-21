#!/usr/bin/env python3
"""Validate 30 real Chromium PNG/JSON evidence files on the exact GitHub Actions SHA."""
from pathlib import Path
from hashlib import sha256
from collections import Counter
import json, os, struct, sys
SCENES = ("account-pending", "account-success", "account-error", "operator-error",
          "field-error", "modal-pending")
WIDTHS = (320,390,768,1024,1440)

def need(ok: bool, label: str) -> None:
    if not ok: raise AssertionError("F06 EVIDENCE NO_GO: " + label)

def verify(folder: Path) -> None:
    need(folder.is_dir(), "Chromium output missing")
    head = os.getenv("GITHUB_SHA", "")
    need(len(head) == 40, "exact runner SHA missing")
    jsons = list(folder.rglob("feedback-*.json"))
    need(len(jsons) == 30, "expected 30 JSON results, got " + str(len(jsons)))
    seen, counts, manifest = set(), Counter(), []
    for path in sorted(jsons):
        data = json.loads(path.read_text("utf-8"))
        scene, width = data.get("scene"), data.get("viewport", {}).get("width")
        key = "feedback-" + str(scene) + "-" + str(width)
        need(scene in SCENES and width in WIDTHS and key == data.get("filename"), "wrong scene/width")
        need((scene,width) not in seen, "duplicate scene/width")
        seen.add((scene,width)); counts[width] += 1
        need(data.get("runnerSha") == head and data.get("synthetic") is True,
             "runner SHA or synthetic origin wrong")
        need(data.get("violations") == 0, "accessibility violation")
        need(data.get("png") == key + ".png", "wrong png reference")
        png = path.with_name(key + ".png")
        need(png.is_file() and png.stat().st_size > 1024, "missing PNG")
        raw = png.read_bytes()
        need(raw[:8] == b"\x89PNG\r\n\x1a\n" and raw[12:16] == b"IHDR", "PNG header invalid")
        x,y = struct.unpack(">II",raw[16:24])
        need(x == width and y >= 300, "PNG resolution invalid")
        manifest.append({"scene":scene,"width":width,
                         "png":str(png.relative_to(folder)),
                         "height":y,"sha256":sha256(raw).hexdigest()})
    need(all(counts[w] == 6 for w in WIDTHS), "matrix incomplete")
    report = {"status":"PASS_F06_CHROMIUM_EVIDENCE","runnerSha":head,
              "synthetic":True,"authorizationProvenByScreenshots":False,
              "imageCount":30,"files":manifest}
    (folder / "F06_FEEDBACK_MANIFEST.json").write_text(
      json.dumps(report,indent=2,ensure_ascii=False),"utf-8")
    print("F06 EVIDENCE PASS: 30 PNG originals, WCAG zero violations, exact SHA " + head[:12])
if __name__ == "__main__":
    try:
        need(len(sys.argv) == 2, "supply feedback output directory")
        verify(Path(sys.argv[1]))
    except (OSError, AssertionError, ValueError, KeyError, TypeError) as error:
        print(error, file=sys.stderr); raise SystemExit(1) from error
