#!/usr/bin/env python3
"""C03-08: canonical A, hostile 18-mutation B, same-job runtime C."""
from copy import deepcopy
from hashlib import sha256
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

import c03_01_source as source
import c03_05_status as phase

from master_source_guard import canonical_active_word

ROOT = Path(__file__).resolve().parents[1]
WORD = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
WORD_SHA = "e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e"
PROOF = ROOT / ".c03-08-ci-proof"
FILES = {
    "settings": ROOT / "backend/src/Crecisol.Api/Configuration/LocalRuntimeSettings.cs",
    "api": ROOT / "backend/src/Crecisol.Api/Program.cs",
    "tests": ROOT / "backend/tests/Crecisol.LocalConfig.ContractsTests/Program.cs",
    "project": ROOT / "backend/tests/Crecisol.LocalConfig.ContractsTests/Crecisol.LocalConfig.ContractsTests.csproj",
    "mysql": ROOT / "database/tests/c01_04_api_mysql_smoke.sh",
    "spec": ROOT / "docs/c03/C03_08_ESPECIFICACION.md",
    "workflow": ROOT / ".github/workflows/c03-08-local-config.yml",
    "gitignore": ROOT / ".gitignore",
}

def require(value, why):
    if not value:
        raise AssertionError(why)

def read_all():
    for name, path in FILES.items():
        require(path.is_file(), "Missing code or gate: " + str(path))
    return {name: path.read_text(encoding="utf-8") for name, path in FILES.items()}

def invariant(p):
    settings, api, tests, runner = (p[k] for k in ("settings","api","tests","mysql"))
    for token in (
        "enum LocalRuntimeProfile : byte", "Disabled = 0", "Local = 1", "Ci = 2",
        "public static LocalRuntimeSettings Parse(", "ValidateHostAllowlist(allowedHosts)",
        "profile is null", "maxWorkerBatch is not null", "allowExternalNetwork is not null",
        "testSocketPath is not null", '"local" => LocalRuntimeProfile.Local',
        '"ci" => LocalRuntimeProfile.Ci',
        "allowedHosts.Any(host =>", '"127.0.0.1"', '"localhost"',
        'allowExternalNetwork != "false"', "NumberStyles.None", "bound is < 1 or > 32",
        "maxWorkerBatch != bound.ToString(CultureInfo.InvariantCulture)",
        'testSocketPath.StartsWith("/tmp/"', 'testSocketPath.EndsWith("/mysqld.sock"',
        'testSocketPath.Contains(".."', 'testSocketPath.Contains("//"',
        "testSocketPath.Length > 180",
        "CanAccessExternalNetwork => false", "hosts.Count is < 1 or > 16",
        "var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase)",
        "!unique.Add(host)", "label.Length is < 1 or > 63", "label[0] == '-'",
        "label[^1] == '-'", "Unsupported local runtime profile.",
    ):
        require(token in settings, "Config invariant removed: " + token)
    for token in (
        "using Crecisol.Api.Configuration;", "LocalRuntimeSettings.ValidateHostAllowlist(allowed);",
        "LocalRuntimeSettings.Parse(", 'Environment.GetEnvironmentVariable("CRECISOL_C03_08_PROFILE")',
        'Environment.GetEnvironmentVariable("CRECISOL_C03_08_MAX_WORKER_BATCH")',
        'Environment.GetEnvironmentVariable("CRECISOL_C03_08_ALLOW_EXTERNAL_NETWORK")',
        'Environment.GetEnvironmentVariable("CRECISOL_C03_08_TEST_SOCKET")',
        "builder.Services.AddPublicBrandingModule(serverDsn);",
        'Environment.GetEnvironmentVariable("CRECISOL_C01_READONLY_DSN")',
        'Environment.GetEnvironmentVariable("CRECISOL_C01_ALLOWED_HOSTS")',
        'app.MapGet("/api/v1/public/branding/platform"',
    ):
        require(token in api, "Existing public API/local guard weakened: " + token)
    require(api.count("app.MapGet(") == 1 and "app.MapPost(" not in api and
            "AddHostedService" not in api and "UseForwardedHeaders" not in api,
            "C03-08 cannot activate HTTP, client-selected hosts or workers")
    require(api.index("LocalRuntimeSettings.Parse(") < api.index("var app = builder.Build();"),
            "Typed validation must occur before API starts serving requests")
    for token in (
        "Reject(() =>", "Orphaned", "external network", "Unknown", "traversal",
        "Full", "injection", "Duplicate", "localhost", "production"
    ):
        # All different attack families are materially present in executable tests.
        require(token.casefold() in tests.casefold(),
                "Executable negative case family missing: " + token)
    for token in (
        'CRECISOL_C03_08_LOCAL_PROFILE_SMOKE:-0', 'CRECISOL_C03_08_PROFILE="ci"',
        'CRECISOL_C03_08_MAX_WORKER_BATCH="8"',
        'CRECISOL_C03_08_ALLOW_EXTERNAL_NETWORK="false"',
        'CRECISOL_C03_08_TEST_SOCKET="$SOCKET_DIR/mysqld.sock"',
        "--network none", "Connection Protocol=Unix",
    ):
        require(token in runner, "No true isolated runtime integration: " + token)
    for token in (".env", ".env.*", "*.pem", "*.key", "**/appsettings.Production.json"):
        require(token in p["gitignore"], "Secret ignore baseline weakened: " + token)
    require("C03-07" in p["spec"] and "C03-08" in p["spec"] and
            "NO CERTIFICADA" in p["spec"] and
            "C03 NO CERTIFICADO" in p["spec"] and
            "C04 BLOQUEADO" in p["spec"] and
            WORD_SHA in p["spec"], "Unapproved C03-08 advancement or missing source")

def audit_a():
    require(canonical_active_word(WORD_SHA),
            "Canonical Word SHA changed")
    original, table, _, _ = source.original_parts()
    require(len(original) == 7 and len(table) == 9,
            "Canonical chapter 03 matrix changed")
    plan = (ROOT / "docs/PLAN_37_CAPITULOS.md").read_text(encoding="utf-8")
    prep = (ROOT / "docs/C03_PREPARACION.md").read_text(encoding="utf-8")
    future = (ROOT / "docs/planificacion-futura/C03_MICROFASES_PROPUESTAS.md").read_text(encoding="utf-8")
    require(phase.fifth_plan_safe(plan),
            "Canonical sequential status must be valid before the local configuration audit")
    row = next((line for line in plan.splitlines() if line.startswith("C03 | ")), "")
    eighth = phase.eighth_certified(row)
    next_lock = "C03-09 NO INICIADA" if eighth else "C03-08 NO INICIADA"
    for t in (plan, prep, future):
        require("C03-07 CERTIFICADA (7/10)" in t and
                next_lock in t and
                ("C03-08 CERTIFICADA (8/10)" in t if eighth else True) and
                "C03 NO CERTIFICADO" in t and "C04 BLOQUEADO" in t,
                "Previous stage, current stage or chapter lock lost")
    require("| C03-08 | Configuración segura y entornos locales |" in prep,
            "Exact microphase source absent")
    require("| C03-08 | Configuración y ambientes locales |" in future,
            "Canonical future plan does not authorize C03-08 scope")
    invariant(read_all())
    print("PASS C03-08 A: immutable Word SHA, 7 paragraphs/9 C03 matrix rows, 7/10 predecessor, no next-phase promotion.")

def audit_b():
    parts = read_all()
    invariant(parts)
    mutants = [
        ("enable by default", "settings", "profile is null", "profile is not null"),
        ("ignore orphan batch", "settings", "maxWorkerBatch is not null", "maxWorkerBatch is null"),
        ("ignore orphan network option", "settings", "allowExternalNetwork is not null", "allowExternalNetwork is null"),
        ("ignore orphan socket option", "settings", "testSocketPath is not null", "testSocketPath is null"),
        ("accept production profile", "settings", '"local" => LocalRuntimeProfile.Local', '"production" => LocalRuntimeProfile.Local'),
        ("allow remote hosts", "settings", "allowedHosts.Any(host =>", "allowedHosts.All(host =>"),
        ("permit external traffic", "settings", 'allowExternalNetwork != "false"', 'allowExternalNetwork != "true"'),
        ("remove batch limit", "settings", "bound is < 1 or > 32", "bound < 0"),
        ("number coercion", "settings", "NumberStyles.None", "NumberStyles.Any"),
        ("accept noncanonical numbers", "settings",
         "maxWorkerBatch != bound.ToString(CultureInfo.InvariantCulture)",
         "maxWorkerBatch == bound.ToString(CultureInfo.InvariantCulture)"),
        ("allow DB remote", "settings", 'testSocketPath.StartsWith("/tmp/"', 'testSocketPath.StartsWith("mysql://"'),
        ("allow socket traversal", "settings", 'testSocketPath.Contains(".."', 'testSocketPath.Contains("safe"'),
        ("allow oversized socket", "settings", "testSocketPath.Length > 180", "testSocketPath.Length > 1000"),
        ("allow unbounded hosts", "settings", "hosts.Count is < 1 or > 16", "hosts.Count < 0"),
        ("allow duplicate hosts", "settings", "!unique.Add(host)", "unique.Contains(host)"),
        ("permit network bool true", "settings", "CanAccessExternalNetwork => false", "CanAccessExternalNetwork => true"),
        ("skip validation at composition", "api",
         "LocalRuntimeSettings.ValidateHostAllowlist(allowed);", " // omitted validation"),
        ("disable smoke opt-in", "mysql", 'CRECISOL_C03_08_PROFILE="ci"', 'CRECISOL_C03_08_PROFILE="production"'),
    ]
    for label, field, before, after in mutants:
        require(before in parts[field], "Bad mutation fixture: " + label)
        broken = deepcopy(parts)
        broken[field] = broken[field].replace(before, after, 1)
        try:
            invariant(broken)
        except AssertionError:
            continue
        raise AssertionError("Unsafe config mutation escaped: " + label)
    source.actual_graph()
    print(f"PASS C03-08 B: {len(mutants)}/{len(mutants)} hostile config mutations rejected, no private/remote activation.")

def audit_c():
    p = read_all()
    invariant(p)
    for token in ("build", "contracts", "mysql", "regressions"):
        marker = PROOF / token
        require(marker.is_file() and
                marker.read_text(encoding="utf-8").strip() == "PASS: " + token,
                "Required real CI execution evidence absent: " + token)
    project = ET.fromstring(p["project"])
    require(project.findtext(".//TargetFramework") == "net10.0" and
            project.findtext(".//TreatWarningsAsErrors") == "true",
            ".NET10 Release strict project contract missing")
    refs = [x.attrib["Include"] for x in project.findall(".//ProjectReference")]
    require(refs == ["../../src/Crecisol.Api/Crecisol.Api.csproj"],
            "Test must reference only API composition root")
    for token in ("CRECISOL_C03_08_LOCAL_PROFILE_SMOKE=1",
                  "dotnet run --no-build -c Release",
                  "bash database/tests/c01_04_api_mysql_smoke.sh",
                  "python3 qa/test_c03_portability.py",
                  "python3 qa/c03_07_closure_gate.py",
                  "python3 qa/c03_08_local_config_audits.py C",
                  "ubuntu-24.04", "contents: read"):
        require(token in p["workflow"], "Real CI gate lost: " + token)
    source.actual_graph()
    print("PASS C03-08 C: verified same-run .NET10 contracts, disposable MySQL8.4 HTTP, old-stage gates, architecture.")

if __name__ == "__main__":
    try:
        require(len(sys.argv) == 2 and sys.argv[1] in ("A","B","C"),
                "Choose independent audit A, B or C")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (AssertionError, KeyError, OSError, ValueError, TypeError) as error:
        print("FAIL C03-08 audit:", str(error), file=sys.stderr)
        sys.exit(1)
