#!/usr/bin/env python3
"""C01-06 final A/B/C gate: source provenance, security and true Angular assets."""
import argparse
from hashlib import sha256
from pathlib import Path
from c01_06_official_assets import APPROVED_SHA, BOX, OUTPUT, FULL, EMBLEM, output_assets
from c01_06_logo_candidate import WORD, EXPECTED, tests, check

ROOT = Path(__file__).resolve().parents[1]

def read(path):
    return (ROOT/path).read_text()

def audit(kind):
    files = dict(output_assets())
    full = files[FULL]
    icon = files[EMBLEM]
    if kind == 'A':
        guide = read('docs/C01_PREPARACION.md')
        master = read('docs/MASTER_REFERENCE.md')
        note = read('docs/c01/C01_06_LOGO_CANDIDATO.md')
        check('C01-06' in guide and EXPECTED in master and APPROVED_SHA in note
              and 'ESE ES EL LOGO' in note and 'isotipo recortado' in note,
              'Approved asset, source, or owner decision absent')
        check('0/37' in guide and 'C01-07' in guide, 'Chapter state not traced')
    elif kind == 'B':
        tests()  # 12 adversarial rejects, existing checked source
        contract = read('frontend/src/app/branding/public-brand.ts')
        page = read('frontend/src/app/pages/platform-home.component.html')
        check('assetPattern' in contract and 'safeAsset' in contract,
              'Same-origin published asset validation absent')
        check('onHeaderAssetError' in page and 'onLogoError' in page
              and '/assets/crecisol-emblem.png' in page,
              'Header and hero missing-asset handling absent')
        check(sha256(full).hexdigest() == APPROVED_SHA, 'Wrong official logo bytes')
    else:
        check(BOX == (390,16,948,553), 'Emblem geometry changed')
        for name, data in files.items():
            check((OUTPUT/name).read_bytes() == data, 'Incorrect approved asset in public output')
            built = ROOT/'frontend/dist/crecisol-web/browser/assets'/name
            check(built.is_file() and built.read_bytes() == data,
                  'Generated approved asset absent or changed in production bundle')
        style = read('frontend/src/app/pages/platform-home.component.css')
        cases = read('frontend/tests/c01-06.official-integration.spec.ts')
        check('header-brand-icon' in style and 'object-fit: contain' in style,
              'Emblem/header style missing')
        check('desktop-1440' in cases and 'mobile-360' in cases
              and 'wcag22aa' in cases and 'HTTP 404' in cases, 'Final browser matrix incomplete')
    print('PASS C01-06 FINAL audit', kind, 'approved', APPROVED_SHA)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--audit', choices=['A','B','C'], required=True)
    audit(parser.parse_args().audit)

if __name__ == '__main__':
    main()
