#!/usr/bin/env python3
"""C01-06: exact byte extraction of UNAPPROVED logo candidate; stdlib only."""
import argparse
from hashlib import sha256
from pathlib import Path
import posixpath
import re
import struct
import sys
import tempfile
from zipfile import ZipFile
import xml.etree.ElementTree as ET
import zlib

ROOT = Path(__file__).resolve().parents[1]
WORD = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
EXPECTED = "e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e"
RN = "{http://schemas.openxmlformats.org/officeDocument/2006/relationships}"
AN = "{http://schemas.openxmlformats.org/drawingml/2006/main}"
PN = "{http://schemas.openxmlformats.org/package/2006/relationships}"
IMAGE_REL = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image"

def check(ok, msg):
    if not ok:
        raise ValueError(msg)

def check_png(data):
    """Check PNG framing, chunk integrity/order and bounded IDAT decompression."""
    check(data.startswith(b"\x89PNG\r\n\x1a\n"), "Bad PNG signature")
    offset, dims, idat_parts, idat_bytes, idat_ended, iend = 8, None, [], 0, False, False
    while offset < len(data):
        check(offset + 12 <= len(data), "Truncated PNG chunk")
        n = struct.unpack(">I", data[offset:offset+4])[0]
        name = data[offset+4:offset+8]
        end = offset + n + 12
        check(n <= 12_000_000 and end <= len(data), "Invalid PNG chunk size")
        check(re.fullmatch(rb"[A-Za-z]{4}", name) is not None and not (name[2] & 32),
              "Invalid PNG chunk type or reserved bit")
        body = data[offset+8:offset+8+n]
        crc = struct.unpack(">I", data[offset+8+n:end])[0]
        check(zlib.crc32(name + body) & 0xffffffff == crc, "PNG CRC mismatch")
        if name == b"IHDR":
            check(offset == 8 and n == 13 and dims is None, "Bad PNG IHDR")
            dims = struct.unpack(">II", body[:8])
            depth, color, compression, filtering, interlace = struct.unpack(">BBBBB", body[8:])
            valid_depths = {0:(1,2,4,8,16), 2:(8,16), 3:(1,2,4,8), 4:(8,16), 6:(8,16)}
            check(0 < dims[0] <= 12000 and 0 < dims[1] <= 12000
                  and dims[0] * dims[1] <= 16_000_000, "Excessive PNG dimensions")
            check(depth in valid_depths.get(color, ()), "Invalid PNG depth/color combination")
            check(compression == filtering == 0 and interlace in (0,1),
                  "Unsupported PNG compression/filter/interlace")
        elif name == b"IDAT":
            check(dims is not None and not idat_ended, "PNG IDAT is out of order")
            idat_bytes += n
            check(idat_bytes <= 12_000_000, "Excessive PNG compressed bytes")
            idat_parts.append(body)
        elif name == b"IEND":
            check(dims is not None and bool(idat_parts) and n == 0 and end == len(data),
                  "PNG missing IDAT or trailing data")
            iend = True
        else:
            check(dims is not None and not iend, "PNG ancillary chunk before IHDR")
            check((name[0] & 32) != 0 or name == b"PLTE", "Unknown critical PNG chunk")
            if name == b"PLTE":
                check(not idat_parts, "PNG palette after IDAT")
            if idat_parts:
                idat_ended = True
        offset = end
    check(dims is not None and bool(idat_parts) and iend, "Missing PNG required chunks")
    decoder = zlib.decompressobj()
    try:
        decoded = decoder.decompress(b"".join(idat_parts), 32_000_001)
    except zlib.error as error:
        raise ValueError("Corrupt PNG compressed pixel data") from error
    check(decoder.eof and not decoder.unused_data and not decoder.unconsumed_tail
          and 0 < len(decoded) <= 32_000_000, "PNG decompression invalid or excessive")
    return dims

def check_jpg(data):
    check(data.startswith(b"\xff\xd8") and data.endswith(b"\xff\xd9"), "Bad JPEG framing")
    i = 2
    while i < len(data)-3:
        check(data[i] == 255, "Malformed JPEG marker")
        while i < len(data) and data[i] == 255:
            i += 1
        check(i < len(data), "Truncated JPEG marker")
        m = data[i]
        i += 1
        if m in (1, 216, 217) or 208 <= m <= 215:
            continue
        check(i + 2 <= len(data), "Truncated JPEG segment")
        n = struct.unpack(">H", data[i:i+2])[0]
        check(n >= 2 and i+n <= len(data), "Invalid JPEG segment")
        if m in (192, 193, 194, 195, 197, 198, 199, 201, 202, 203, 205, 206, 207):
            check(n >= 7, "Missing JPEG dimensions")
            h, w = struct.unpack(">HH", data[i+3:i+7])
            return w, h
        i += n
    raise ValueError("No JPEG dimensions")

def candidate(path, expected):
    check(path.is_file() and path.stat().st_size <= 10_000_000, "Word absent or oversized")
    check(sha256(path.read_bytes()).hexdigest() == expected, "Canonical Word SHA mismatch")
    with ZipFile(path) as z:
        check(z.testzip() is None, "Corrupted DOCX")
        doc = ET.fromstring(z.read("word/document.xml"))
        rels = ET.fromstring(z.read("word/_rels/document.xml.rels"))
        refs = set(x.get(RN+"embed") for x in doc.findall(".//"+AN+"blip") if x.get(RN+"embed"))
        check(len(refs) == 1, "Embedded image reference must be unique")
        matches = [x for x in rels.findall(PN+"Relationship") if x.get("Id") in refs]
        check(len(matches) == 1 and matches[0].get("Type") == IMAGE_REL, "Image rel invalid")
        rel = matches[0]
        check(rel.get("TargetMode", "Internal") == "Internal", "External image rejected")
        target = rel.get("Target", "")
        check(target and not target.startswith("/") and "\\" not in target
              and ".." not in target.split("/"), "Path traversal rejected")
        member = posixpath.normpath(posixpath.join("word", target))
        check(re.fullmatch(r"word/media/[A-Za-z0-9_.-]+\.(png|jpg|jpeg)", member, re.I),
              "Unsafe/non-raster image format")
        entry = z.getinfo(member)
        check(0 < entry.file_size <= 12_000_000, "Unsafe image size")
        data = z.read(entry)
    ext = member.rsplit(".", 1)[-1].lower()
    w, h = check_png(data) if ext == "png" else check_jpg(data)
    check(0 < w <= 12000 and 0 < h <= 12000 and max(w/h, h/w) <= 15,
          "Invalid image dimensions or ratio")
    return data, ext, member, w, h

def fake_png():
    def chunk(tag, payload):
        return struct.pack(">I", len(payload)) + tag + payload + struct.pack(
            ">I", zlib.crc32(tag+payload) & 0xffffffff)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB",1,1,8,2,0,0,0)) + chunk(b"IDAT",zlib.compress(b"\x00\x00\x00\xff")) + chunk(b"IEND",b"")

def fake_word(path, img, target="media/image1.png", external=False, duplicate=False):
    xml = ('<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" '
           'xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" '
           'xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><w:body>'
           '<a:blip r:embed="rId7"/>' + ('<a:blip r:embed="rId8"/>' if duplicate else '')
           + '</w:body></w:document>')
    rels = ('<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
            '<Relationship Id="rId7" Type="' + IMAGE_REL + '" Target="' + target + '"'
            + (' TargetMode="External"' if external else '') + '/></Relationships>')
    with ZipFile(path,"w") as z:
        z.writestr("word/document.xml",xml)
        z.writestr("word/_rels/document.xml.rels",rels)
        z.writestr("word/media/image1.png",img)

def tests():
    png = fake_png()
    check(check_png(png) == (1,1), "Positive image test failed")
    with tempfile.TemporaryDirectory() as temp:
        p = Path(temp)/"test.docx"
        fake_word(p,png)
        digest = sha256(p.read_bytes()).hexdigest()
        check(candidate(p,digest)[0] == png, "Positive extraction failed")
        def reject(label, wrong=None):
            try:
                candidate(p,wrong or sha256(p.read_bytes()).hexdigest())
            except (ValueError,KeyError):
                print("PASS negative:",label)
                return
            raise AssertionError("Accepted malicious candidate: " + label)
        reject("altered Word","0"*64)
        fake_word(p,png,external=True)
        reject("external relation")
        fake_word(p,png,target="../../secret.png")
        reject("path traversal")
        fake_word(p,b"<script>alert(1)</script>")
        reject("HTML disguised as PNG")
        fake_word(p,png,duplicate=True)
        reject("ambiguous image")
        corrupted = bytearray(png)
        corrupted[20] ^= 1
        fake_word(p,bytes(corrupted))
        reject("invalid PNG CRC")
        def chunk(tag, payload):
            return (struct.pack(">I", len(payload)) + tag + payload
                    + struct.pack(">I", zlib.crc32(tag + payload) & 0xffffffff))
        ihdr = png[8:33]
        fake_word(p, png[:33] + ihdr + png[33:])
        reject("duplicate IHDR despite valid CRC")
        fake_word(p, png[:33] + chunk(b"ABCD", b"invalid") + png[33:])
        reject("unknown critical PNG chunk")
        fake_word(p, png[:33] + chunk(b"IDAT", b"not-zlib") + chunk(b"IEND", b""))
        reject("corrupt IDAT with valid CRC")
        bad_header = chunk(b"IHDR", struct.pack(">IIBBBBB", 1, 1, 8, 2, 1, 0, 0))
        fake_word(p, png[:8] + bad_header + png[33:])
        reject("unsupported PNG compression method")
        fake_word(p, png + b"hidden-payload")
        reject("trailing payload after IEND")
        fake_word(p, png[:33] + chunk(b"IDAT", zlib.compress(b"\x00\x00\x00\xff"))
                  + chunk(b"tEXt", b"note") + chunk(b"IDAT", b"another") + chunk(b"IEND", b""))
        reject("discontiguous PNG IDAT")
    print("PASS candidate synthetic positive/negative tests (NOT logo approval)")

def audits(kind):
    data, ext, member, w, h = candidate(WORD,EXPECTED)
    spec = (ROOT/"docs/c01/C01_06_LOGO_CANDIDATO.md").read_text()
    if kind == "A":
        matrix = (ROOT/"docs/c01/C01_01_MATRIZ_REQUISITOS.md").read_text()
        master = (ROOT/"docs/MASTER_REFERENCE.md").read_text()
        check(EXPECTED in master and all(x in spec and x in matrix for x in
              ("P14","P25","T02-R01","T03-R01")), "Traceability incomplete")
    elif kind == "B":
        tests()
        check(member.startswith("word/media/") and "NO APROBADO" in spec,
              "Asset isolation/approval warning missing")
    elif kind == "C":
        check(w > 0 and h > 0 and "Chromium" in spec and "WCAG" in spec,
              "Visual gate requirements missing")
        check((ROOT/"frontend/src/app/pages/platform-home.component.html").is_file(),
              "Existing frontend absent")
    print("PASS preliminary audit",kind,"candidate_sha256",sha256(data).hexdigest())
    print("LIMIT: final UI testing, visual fidelity and owner approval NOT EXECUTED")

def main():
    ap=argparse.ArgumentParser()
    group=ap.add_mutually_exclusive_group(required=True)
    group.add_argument("--tests",action="store_true")
    group.add_argument("--inspect",action="store_true")
    group.add_argument("--extract-dir",type=Path)
    group.add_argument("--audit",choices=["A","B","C"])
    a=ap.parse_args()
    if a.tests:
        tests()
    elif a.audit:
        audits(a.audit)
    else:
        data,ext,member,w,h=candidate(WORD,EXPECTED)
        print("WORD SHA256",EXPECTED)
        print("CANDIDATE",member,"dimensions",w,h,"bytes",len(data),"SHA256",sha256(data).hexdigest())
        print("STATUS UNAPPROVED; NOT INSTALLED")
        if a.extract_dir is not None:
            a.extract_dir.mkdir(parents=True,exist_ok=True)
            target=a.extract_dir/("crecisol-candidate-NOT-APPROVED."+ext)
            target.write_bytes(data)
            check(target.read_bytes()==data,"Output bytes changed")
            print("EXACT candidate exported:",target)

if __name__=="__main__":
    try:
        main()
    except (ValueError,AssertionError,KeyError,FileNotFoundError,ET.ParseError,OSError) as error:
        print("FAIL C01-06:",error,file=sys.stderr)
        sys.exit(1)
