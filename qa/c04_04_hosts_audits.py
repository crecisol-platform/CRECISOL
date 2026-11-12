#!/usr/bin/env python3
"""CRECISOL C04-04: host trust boundary evidence A/B/C.

A: pinned C04 master, 72 paragraphs, domain lifecycle, isolated architecture.
B: independent marker mutations + direct Host / Forwarded / duplicate guards.
C: real .NET10 tests on same CI SHA/run, clean dependencies and no active routes.
This NEVER certifies live DNS ownership, a proxy deployment or private auth.
"""
from __future__ import annotations

import os
import re
import sys

from c04_01_normative_inventory import EXPECTED, chapter4
from c04_01_preparation_audits import ROOT, canonical_active_word

PATHS = {
    "domain": ROOT / "backend/src/Crecisol.Domain/Tenancy/VerifiedTenantHost.cs",
    "app": ROOT / "backend/src/Crecisol.Application/Tenancy/ResolveVerifiedTenantHost.cs",
    "api": ROOT / "backend/src/Crecisol.Api/Configuration/StrictVerifiedHostTransport.cs",
    "test": ROOT / "backend/tests/Crecisol.VerifiedHosts.ContractsTests/Program.cs",
    "spec": ROOT / "docs/c04/C04_04_ESPECIFICACION.md",
    "workflow": ROOT / ".github/workflows/c04-04-verified-hosts.yml",
}
PROOF = ROOT / ".c04-04-host-proof"

MARKERS = {
    "domain": (
        "CanonicalTenantHost",
        "IPAddress.TryParse(rawHost, out _)",
        "rawHost.Any(c => c < 33 || c > 126)",
        "rawHost.Split('.')",
        "label[0] == '-'",
        "label[^1] == '-'",
        "rawHost.ToLowerInvariant()",
        "PendingVerification = 1",
        "Verified = 2",
        "Suspended = 3",
        "Revoked = 4",
        "TenantAddressMode.Subdomain or TenantAddressMode.CustomDomain",
        "if (!Enum.IsDefined(state) || revision <= 0)",
        "changedAtUtc.Offset != TimeSpan.Zero",
        "validUntilUtc.Value.Offset != TimeSpan.Zero",
        "state == HostVerificationState.Verified",
        "state == HostVerificationState.PendingVerification",
        "ChangedAtUtc <= nowUtc",
        "nowUtc < ValidUntilUtc.Value",
        "VerifiedTenantHostHint[redacted]",
    ),
    "app": (
        "ITrustedVerifiedHostCatalog",
        "ITrustedHostUtcClock",
        "ArgumentNullException.ThrowIfNull(host)",
        "cancellationToken.ThrowIfCancellationRequested();",
        "catalog.FindExactBindingsAsync(host, cancellationToken)",
        "matches is null || matches.Count != 1 || matches[0] is null",
        "if (binding.Host != host)",
        "var nowUtc = clock.GetUtcNow();",
        "if (!binding.IsVerifiedAt(nowUtc))",
        "throw new UnauthorizedAccessException",
        "new VerifiedTenantHostHint(",
    ),
    "api": (
        "request.Headers.Keys.Any",
        'header.Equals("Forwarded", StringComparison.OrdinalIgnoreCase)',
        'header.StartsWith("X-Forwarded-", StringComparison.OrdinalIgnoreCase)',
        'header.Equals("X-Original-Host", StringComparison.OrdinalIgnoreCase)',
        'header.Equals("X-Original-URL", StringComparison.OrdinalIgnoreCase)',
        "var values = request.Headers.Host;",
        "values.Count != 1",
        "CanonicalTenantHost.Parse(values[0])",
        "catch (InvalidOperationException)",
    ),
    "test": (
        "Two tenants own same host: ambiguity denied",
        "Same-tenant duplicate host ownership also denied",
        "Expired verified host cannot resolve",
        "Revoked host cannot resolve",
        "Future verified host cannot resolve",
        "Host A lookup never accepts catalog entry for host B",
        "Cancelled request never calls catalogue",
        "Duplicate Host headers denied even if first is allowed",
        "Forged proxy and rewrite header rejected:",
        "PathSlug cannot resolve tenant from a host alone",
        "Backend clock cannot run in non-UTC mode",
        "NO real DNS ownership verification",
    ),
    "spec": (
        EXPECTED,
        "C04-04 NO CERTIFICADA",
        "C04-05 BLOQUEADA",
        "No confiar en Forwarded no verificado",
        "no valida DNS en vivo",
    ),
    "workflow": (
        "dotnet-version: '10.0.x'",
        "qa/c04_04_hosts_audits.py A",
        "qa/c04_04_hosts_audits.py B",
        "qa/c04_04_hosts_audits.py C",
        "Crecisol.VerifiedHosts.ContractsTests.csproj",
        "dotnet run --no-build",
        "persist-credentials: false",
        "contents: read",
    ),
}


def require(condition: bool, why: str) -> None:
    if not condition:
        raise AssertionError(why)


def load() -> dict[str, str]:
    return {kind: path.read_text(encoding="utf-8") for kind, path in PATHS.items()}


def guard(files: dict[str, str]) -> None:
    for kind, markers in MARKERS.items():
        for marker in markers:
            require(marker in files[kind], f"{kind}: missing boundary: {marker}")

    domain, app, api = files["domain"], files["app"], files["api"]
    require(domain.count("public sealed record") == 3,
            "Host, binding and hint must remain three typed immutable records")
    require(domain.count("public enum HostVerificationState") == 1,
            "One bounded verification lifecycle only")
    require("MySqlConnection" not in domain and "MySqlConnection" not in app
            and "ConnectionStringBuilder" not in domain
            and "ConnectionStringBuilder" not in app,
            "No database placement or credential construction in host contracts")
    require("using Microsoft" not in domain and "using Microsoft" not in app,
            "Domain/Application must be independent of HTTP/hosting")
    require(app.count("catalog.FindExactBindingsAsync(host, cancellationToken)") == 1,
            "One exact protected lookup is mandatory")
    require(app.index("catalog.FindExactBindingsAsync") < app.index("matches.Count != 1"),
            "Ambiguous results must be rejected after actual lookup")
    require(app.index("matches.Count != 1") < app.index("new VerifiedTenantHostHint("),
            "Hint cannot be returned before ambiguity rejection")
    require(app.index("binding.IsVerifiedAt(nowUtc)") < app.index("new VerifiedTenantHostHint("),
            "Unverified/expired results must never escape")
    require(api.count("var values = request.Headers.Host;") == 1,
            "Only direct Host header can be used")
    require("request.Headers.Host" in api and "request.Host.Host" not in api,
            "Do not use a derived/inferred proxy host")
    require("X-Forwarded-Host" not in (ROOT / "backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8").replace(
        'context.Request.Headers.ContainsKey("X-Forwarded-Host")', ""),
        "Do not add new proxy or private route wiring in Program.cs")
    require("StrictVerifiedHostTransport" not in
            (ROOT / "backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8"),
            "Isolated transport must not be registered before C04-05/06")
    require("ResolveVerifiedTenantHost" not in
            (ROOT / "backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8"),
            "No private host resolver / DI exposed through current API")
    require("Forwarded" not in domain and "HttpRequest" not in app,
            "Transport must not reach Domain or Application")
    require("Test" in files["test"] and "FakeCatalog" in files["test"],
            "Mock catalogue must be explicitly disclosed in tests")
    require("create-pull-request" not in files["workflow"] and
            "docker run" not in files["workflow"] and
            "deploy" not in files["workflow"].lower(),
            "Host workflow must not change network/infrastructure")


def audit_a() -> None:
    require(canonical_active_word(), "Canonical Word/PDF SHA mismatch")
    rows = chapter4()
    require(len(rows) == 72, "Original C04 72 paragraphs changed")
    text = "\n".join(s for _, s in rows)
    for marker in ("SHARED A/B y DEDICATED C",
                   "nunca confiar en tenant_id aportado por navegador",
                   "FK compuestas con tenant"):
        require(marker in text, "Canonical C04 authority missing: " + marker)
    plan = (ROOT / "docs/planificacion-futura/C04_MICROFASES_PROPUESTAS.md").read_text(encoding="utf-8")
    require("| C04-04 | Resolución de hosts verificados |" in plan,
            "C04-04 scope mutated in original plan")
    require("No confiar en Forwarded no verificado" in plan,
            "C04-04 negative gate removed from plan")
    guard(load())
    print("PASS C04-04 A: pinned master C04 72 paragraphs, exact host"
          " lifecycle and fail-closed backend hint with no production endpoints")


def audit_b() -> None:
    files = load()
    guard(files)
    count = 0
    for kind, markers in MARKERS.items():
        for marker in markers:
            mutant = dict(files)
            mutant[kind] = mutant[kind].replace(marker, "[MUTATED TRUST BOUNDARY]")
            try:
                guard(mutant)
            except AssertionError:
                count += 1
            else:
                raise AssertionError("Guard accepted mutation: " + kind + ": " + marker)
    require(not any("ITrustedVerifiedHostCatalog" in f.read_text(encoding="utf-8")
                    for f in (ROOT / "backend/src/Crecisol.Infrastructure").rglob("*.cs")),
            "Unreviewed live host catalogue adapter created in Infrastructure")
    print(f"PASS C04-04 B: {count} adversarial source guard mutations rejected; "
          "direct Host only, no X-Forwarded-*, no ambiguous/cross-tenant hints")


def audit_c() -> None:
    require(os.environ.get("GITHUB_ACTIONS") == "true",
            "Audit C requires a real GitHub Actions runner")
    sha = os.environ.get("GITHUB_SHA", "")
    run = os.environ.get("GITHUB_RUN_ID", "")
    require(bool(re.fullmatch(r"[0-9a-f]{40}", sha) and re.fullmatch(r"[0-9]+", run)),
            "SHA or run ID invalid")
    for kind in ("A", "B", "dotnet"):
        p = PROOF / kind
        require(p.is_file() and p.read_text(encoding="utf-8").strip() ==
                f"PASS {kind} sha={sha} run={run}",
                "Missing/stale same-run evidence: " + kind)
    for path in (
        ROOT / "backend/src/Crecisol.Domain/Crecisol.Domain.csproj",
        ROOT / "backend/src/Crecisol.Application/Crecisol.Application.csproj",
        ROOT / "backend/src/Crecisol.Api/Crecisol.Api.csproj",
        ROOT / "backend/tests/Crecisol.VerifiedHosts.ContractsTests/Crecisol.VerifiedHosts.ContractsTests.csproj",
    ):
        require("<TargetFramework>net10.0</TargetFramework>" in path.read_text(encoding="utf-8"),
                "Invalid .NET10 project graph: " + str(path))
    require("Crecisol.Domain.csproj" in
            (ROOT / "backend/src/Crecisol.Application/Crecisol.Application.csproj").read_text(encoding="utf-8"),
            "Application must depend on Domain")
    require("Crecisol.Api.csproj" in
            (ROOT / "backend/tests/Crecisol.VerifiedHosts.ContractsTests/Crecisol.VerifiedHosts.ContractsTests.csproj").read_text(encoding="utf-8"),
            "Tests must exercise REAL ASP.NET Core HTTP request adapter")
    guard(load())
    print("PASS C04-04 C: same SHA-run A/B/.NET10, ASP.NET request"
          " negative tests, layered architecture; DNS live/private auth NOT certified")


def main() -> None:
    require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
            "Usage: qa/c04_04_hosts_audits.py A|B|C")
    {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, OSError, ValueError) as exc:
        print("FAIL C04-04:", exc, file=sys.stderr)
        sys.exit(1)
