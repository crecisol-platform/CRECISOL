#!/usr/bin/env python3
"""C01-08 integral chapter 01 gates A/B/C, technical only.

Read-only verification requiring proof produced after actual runner commands;
never grants owner approval or certifies a chapter.
"""
from hashlib import sha256
from pathlib import Path
import sys

import c01_verify_source as source_verifier

ROOT = Path(__file__).resolve().parents[1]
PROOF = ROOT / '.c01-08-ci-proof'
WORD_HASH = 'e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e'
LOGO_HASH = '9e83d1d0a891565cf80a21a7e90d7afcaa24c9bb63e7d2a589790127dadd549f'


def read(path):
    return (ROOT / path).read_text(encoding='utf-8')


def require(condition, message):
    if not condition:
        raise SystemExit('FAIL C01-08 ' + message)


def verified(name):
    location = PROOF / name
    require(location.is_file() and location.read_text(encoding='utf-8').strip() ==
            'PASS: ' + name, 'runner proof absent: ' + name)


def audit_a():
    source_verifier.main()  # validates actual Word bytes, 34 paragraphs and 21 rows
    matrix = read('docs/c01/C01_01_MATRIZ_REQUISITOS.md')
    plan = read('docs/C01_PREPARACION.md')
    acta = read('docs/c01/C01_08_AUDITORIA_Y_ACTA.md')
    require(all(x in matrix for x in ('P01', 'P04', 'P06', 'P07', 'P14',
            'P25', 'P28', 'P34', 'T03-R02', 'T03-R04')),
            'source requirements not traced')
    for n in range(1, 8):
        old = read(f'docs/c01/C01_{n:02d}_ACTA_MICROFASE.md')
        require(f'C01-{n:02d}' in old and
                any(x in old for x in ('PASS', 'CERRAD', 'Cerrad')),
                f'prior microphase C01-{n:02d} has no closure evidence')
    require('C01-08' in plan and '7/8' in plan and '0/37' in plan,
            'C01 plan or next chapter gate inconsistent')
    require('NO CERTIFICADO' in acta and 'PENDIENTE' in acta and
            'Aceptación humana expresa' in acta,
            'owner acceptance requirement missing')
    require(WORD_HASH in read('docs/MASTER_REFERENCE.md'),
            'canonical Word SHA missing')
    for name in ('word', 'api', 'domain', 'application'):
        verified(name)


def audit_b():
    html = read('frontend/src/app/pages/platform-home.component.html')
    typescript = read('frontend/src/app/pages/platform-home.component.ts')
    index = read('frontend/src/index.html')
    parser = read('frontend/src/app/branding/public-brand.ts')
    backend = read('backend/src/Crecisol.Api/Program.cs')
    wf = read('.github/workflows/c01-08-integral.yml')
    require('[src]="asset.url"' in html and 'model.brand.logoUrl' in html
            and 'model.brand.name' in html and
            '/assets/crecisol-emblem.png' not in html,
            'header/hero identity still hardcoded')
    require('headerAssetFailedKey' in typescript and
            'brand.presentation?.faviconUrl' in typescript and
            'data-crecisol-managed' in typescript,
            'revision-aware image failure or head metadata missing')
    require('/assets/crecisol-emblem.png' not in index,
            'a static icon survives publication change')
    require('safeAsset' in parser and 'assetPattern' in parser and
            'X-Tenant-Id' in backend and 'X-Forwarded-Host' in backend,
            'frontend/backend input boundaries missing')
    require('permissions:\n  contents: read' in wf,
            'CI token not read-only')
    for name in ('browser-revision', 'live-stack', 'legacy-audits'):
        verified(name)


def audit_c():
    folder = ROOT / 'frontend/dist/crecisol-web/browser/assets'
    require((folder/'crecisol-emblem.png').is_file(),
            'approved emblem not built')
    original = folder/'crecisol-oficial.png'
    require(original.is_file() and sha256(original.read_bytes()).hexdigest() == LOGO_HASH,
            'approved main logo not built byte-exact')
    contract = read('docs/c01/C01_08_PARAMETRIZACION_TRANSVERSAL.md')
    regression = read('frontend/tests/c01-08.brand-revision.spec.ts')
    for index in range(1, 11):
        require(f'PAR-{index:02d}' in contract,
                f'future editable-brand verification PAR-{index:02d} not traced')
    require('a newly fetched published revision changes' in regression and
            'wcag22aa' in regression and 'untrusted.invalid' in regression,
            'versioned UI negative/WCAG tests are absent')
    for name in ('bundle', 'browser-revision', 'live-stack',
                 'domain', 'application', 'legacy-audits'):
        verified(name)


def main():
    mode = sys.argv[1] if len(sys.argv) > 1 else None
    require(mode in ('A', 'B', 'C'), 'invalid audit mode')
    {'A': audit_a, 'B': audit_b, 'C': audit_c}[mode]()
    print('PASS C01-08 INTEGRAL audit', mode,
          '(technical evidence only; owner acceptance PENDING)')


if __name__ == '__main__':
    main()
