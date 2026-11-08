#!/usr/bin/env python3
"""Copy the original approved logo embedded in the canonical CRECISOL Word.
No redrawing, modification, external scripts or remote resource requests.
"""
from hashlib import sha256
from pathlib import Path
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[2]
DOCUMENT = ROOT / "docs" / "CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
DOCUMENT_SHA256 = "e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e"
IMAGE_SHA256 = "9e83d1d0a891565cf80a21a7e90d7afcaa24c9bb63e7d2a589790127dadd549f"
TARGET = ROOT / "frontend" / "public" / "assets" / "crecisol-logo-oficial.png"

def main() -> None:
    if sha256(DOCUMENT.read_bytes()).hexdigest() != DOCUMENT_SHA256:
        raise SystemExit("STOP: the canonical Word has changed. Verify the new approved logo.")
    with ZipFile(DOCUMENT) as archive:
        image = archive.read("word/media/image1.png")
    if not image.startswith(b"\x89PNG\r\n\x1a\n") or sha256(image).hexdigest() != IMAGE_SHA256:
        raise SystemExit("STOP: the embedded official image differs from the approved logo.")
    TARGET.parent.mkdir(parents=True, exist_ok=True)
    if not TARGET.exists() or TARGET.read_bytes() != image:
        TARGET.write_bytes(image)
    print("CRECISOL official logo verified and extracted.")

if __name__ == "__main__":
    main()
