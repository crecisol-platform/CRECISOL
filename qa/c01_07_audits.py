#!/usr/bin/env python3
"""C01-07 independent audits after REAL runner checks, never self-attested PASS."""
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PROOF = ROOT / '.c01-07-ci-proof'

def source(path):
    return (ROOT / path).read_text(encoding='utf-8')

def require(ok, message):
    if not ok:
        raise SystemExit('FAIL C01-07 ' + message)

def completed(name):
    result = PROOF / name
    require(result.is_file() and result.read_text().strip() == 'PASS: ' + name,
            'missing actual runner success: ' + name)

def audit_a():
    master = source('docs/MASTER_REFERENCE.md')
    matrix = source('docs/c01/C01_01_MATRIZ_REQUISITOS.md')
    plan = source('docs/C01_PREPARACION.md')
    contract = source('docs/c01/C01_07_REGRESION_Y_SEGURIDAD.md')
    require('e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e' in master,
            'master identity missing')
    require(all(item in matrix for item in ('P04', 'P06', 'P07', 'P25', 'T03-R02')),
            'canonical traceability missing')
    require(all(item in contract for item in ('C01-07', 'MySQL 8.4', 'Angular 22', 'ASP.NET Core 10')),
            'technical contract missing')
    require('C01-06' in plan and '0/37' in plan, 'prior closure or chapter gate missing')
    for item in ('api-baseline', 'api-absent', 'api-invalid', 'api-restored',
                 'browser-published', 'browser-absent', 'browser-invalid', 'browser-updated'):
        completed(item)

def audit_b():
    api = source('backend/src/Crecisol.Api/Program.cs')
    reader = source('backend/src/Crecisol.Infrastructure/Branding/MySqlPublishedPlatformBrandReader.cs')
    smoke = source('qa/c01_04_http_smoke.py')
    harness = source('qa/c01_07_real_stack.sh')
    tests = source('frontend/tests/c01-07.real-stack.spec.ts')
    require('CRECISOL_C01_ALLOWED_HOSTS' in api and 'X-Forwarded-Host' in api
            and 'X-Tenant-Id' in api and 'Authorization' in api,
            'host and client context fail-closed missing')
    require('WHERE p.scope_kind = 1' in reader and 'brand_publications' in reader,
            'platform-only SQL predicate missing')
    require('PRIVATE_TENANT_B' in tests and 'UNPUBLISHED_DRAFT' in tests,
            'private tenant/draft negative browser checks missing')
    require('X-Forwarded-Host' in smoke and 'X-Tenant-Id' in smoke
            and 'admin branding route not exposed' in smoke,
            'HTTP authorization-negative regression missing')
    require('--network none' in harness and 'GRANT SELECT' in harness
            and 'UPDATE brand_revisions' in harness,
            'isolated SQL grant check missing')
    require('page.route(' not in '\n'.join(line for line in tests.splitlines()
            if not line.lstrip().startswith(('*', '//', '/*'))),
            'real stack test must not mock API')
    for item in ('read-only', 'browser-invalid', 'browser-absent', 'api-invalid', 'api-absent'):
        completed(item)

def audit_c():
    app = source('frontend/playwright.c01-07.config.ts')
    tests = source('frontend/tests/c01-07.real-stack.spec.ts')
    workflow = source('.github/workflows/c01-07-regression.yml')
    require('proxy-config' in app and 'reuseExistingServer: false' in app,
            'same-origin real proxy or test server isolation missing')
    require('wcag22aa' in tests and 'width: 320' in tests and 'scrollWidth' in tests,
            'responsive/WCAG evidence missing')
    require('npm run build' in workflow and 'npm run check:types' in workflow
            and 'npm ci' in workflow and 'dotnet build' in workflow,
            'real toolchain gate missing')
    require('permissions:\n  contents: read' in workflow,
            'Actions token must be read-only')
    require((ROOT/'frontend/dist/crecisol-web/browser/assets/crecisol-oficial.png').is_file(),
            'official image missing from built bundle')
    completed('browser-published')
    completed('browser-updated')
    completed('browser-legacy')
    completed('api-restored')

if __name__ == '__main__':
    name = sys.argv[1] if len(sys.argv) > 1 else None
    require(name in ('A', 'B', 'C'), 'invalid audit name')
    {'A': audit_a, 'B': audit_b, 'C': audit_c}[name]()
    print('PASS C01-07 FINAL audit ' + name + ' on real disposable stack')
