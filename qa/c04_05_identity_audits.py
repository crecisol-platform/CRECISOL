#!/usr/bin/env python3
"""CRECISOL C04-05 gated security audits A/B/C.

A: canonical Word chapter04, original 12-phase plan, typed layering.
B: reject adversarial guard mutations, forbid client tenant_id and premature DI.
C: match A/B/.NET10 execution evidence to the same real CI SHA and run.

This does NOT test real crypto, HTTP authentication, session store, membership
database, resource authorization, production or cross-tenant HTTP BOLA.
"""
from __future__ import annotations

import os
import re
import sys

from c04_01_normative_inventory import EXPECTED, chapter4
from c04_01_preparation_audits import ROOT, canonical_active_word

PATHS = {
    "domain": ROOT / "backend/src/Crecisol.Domain/Tenancy/TenantIdentityContext.cs",
    "app": ROOT / "backend/src/Crecisol.Application/Tenancy/ResolveCurrentTenantIdentityContext.cs",
    "test": ROOT / "backend/tests/Crecisol.TenantIdentity.ContractsTests/Program.cs",
    "spec": ROOT / "docs/c04/C04_05_ESPECIFICACION.md",
    "workflow": ROOT / ".github/workflows/c04-05-tenant-identity.yml",
}
PROOF = ROOT / ".c04-05-identity-proof"

MARKERS = {
    "domain": (
        "public sealed record TenantActorId",
        "Guid.Empty",
        "TenantActorId[redacted]",
        "public enum ActorSessionState",
        "Suspended = 2",
        "Revoked = 3",
        "public sealed record TrustedActorSessionSnapshot",
        "RestoreFromTrustedIdentityProvider(",
        "authenticatedAtUtc > lastValidatedAtUtc",
        "lastValidatedAtUtc >= expiresAtUtc",
        "State == ActorSessionState.Active",
        "nowUtc < ExpiresAtUtc",
        "public enum TenantMembershipState",
        "Pending = 1",
        "Active = 2",
        "public sealed record TenantActorMembershipSnapshot",
        "RestoreFromTrustedMembershipCatalog(",
        "state == TenantMembershipState.Active && !grantedAtUtc.HasValue",
        "state == TenantMembershipState.Pending && grantedAtUtc.HasValue",
        "ChangedAtUtc <= nowUtc",
        "nowUtc < ValidUntilUtc.Value",
        "public sealed record TenantIdentityContextEvidence",
        "session.State != ActorSessionState.Active",
        "membership.State != TenantMembershipState.Active",
        "catalog.State != TenantLifecycleState.Active",
        "session.ActorId != membership.ActorId",
        "host.TenantId != membership.TenantId",
        "catalog.TenantId != host.TenantId",
        "catalog.AddressMode != host.AddressMode",
        "TenantIdentityContextEvidence[redacted]",
    ),
    "app": (
        "ITrustedCurrentActorSessionReader",
        "ITrustedCurrentDirectHostReader",
        "ITrustedActorTenantMembershipReader",
        "ResolveVerifiedTenantHost verifiedHostResolver",
        "ITrustedTenantCatalogReader tenants",
        "public async Task<TenantIdentityContextEvidence> ExecuteAsync(",
        "CancellationToken cancellationToken)",
        "sessions.ReadCurrentVerifiedSessionAsync(cancellationToken)",
        "if (session is null)",
        "if (!session.IsActiveAtUtc(initialNow))",
        "currentHost.ReadCurrentDirectHostAsync(cancellationToken)",
        "if (host is null)",
        "verifiedHostResolver.ResolveAsync(host, cancellationToken)",
        "membershipReader.FindExactMembershipsAsync(",
        "session.ActorId, hostHint.TenantId, cancellationToken",
        "matches is null || matches.Count != 1 || matches[0] is null",
        "membership.ActorId != session.ActorId",
        "membership.TenantId != hostHint.TenantId",
        "if (!membership.IsActiveAtUtc(initialNow))",
        "tenants.FindByIdAsync(hostHint.TenantId, cancellationToken)",
        "catalog.TenantId != hostHint.TenantId",
        "catalog.State != TenantLifecycleState.Active",
        "catalog.AddressMode != hostHint.AddressMode",
        "var recheckedHost = await verifiedHostResolver.ResolveAsync(",
        "if (recheckedHost != hostHint)",
        "if (!session.IsActiveAtUtc(finalNow) ||",
        "!membership.IsActiveAtUtc(finalNow)",
        "TenantIdentityContextEvidence.FromVerifiedInternalEvidence(",
    ),
    "test": (
        "Actor with A and B memberships receives ONLY the tenant from a verified host",
        "No membership in host A denied",
        "Membership A and B ambiguity rejected without first-match",
        "Membership for actor B cannot authorize actor A",
        "Membership for tenant B cannot authorize host A",
        "Expired session cannot select a tenant",
        "Revoked session cannot select a tenant",
        "Suspended membership cannot select tenant",
        "Tenant B returned for host A rejected",
        "Suspended tenant denied",
        "Address mode mismatch rejected",
        "Changed tenant binding between host checks rejected",
        "Host verification revision changed mid-flight rejected",
        "Cancellation after membership lookup denies",
        "Membership denial must prevent tenant catalog access",
        "Backend actor provider cannot accept caller-supplied actor GUID/claims",
        "Use case accepts ONLY cancellation: no tenant_id, host, claim, cookie or DSN",
        "Identity adapter, membership database, HTTP/DI, branch/resource rights",
    ),
    "spec": (
        EXPECTED,
        "C04-05 NO CERTIFICADA",
        "C04-06 BLOQUEADA",
        "No usar tenant_id como permiso",
        "no autenticación productiva",
    ),
    "workflow": (
        "dotnet-version: '10.0.x'",
        "qa/c04_05_identity_audits.py A",
        "qa/c04_05_identity_audits.py B",
        "qa/c04_05_identity_audits.py C",
        "Crecisol.TenantIdentity.ContractsTests.csproj",
        "dotnet run --no-build",
        "persist-credentials: false",
        "contents: read",
    ),
}


def require(value: bool, reason: str) -> None:
    if not value:
        raise AssertionError(reason)


def load() -> dict[str, str]:
    return {key: path.read_text(encoding="utf-8") for key, path in PATHS.items()}


def guard(data: dict[str, str]) -> None:
    for key, markers in MARKERS.items():
        for marker in markers:
            require(marker in data[key], f"{key}: missing security invariant: {marker}")

    domain, app, tests, workflow = (
        data["domain"], data["app"], data["test"], data["workflow"])
    require(domain.count("public sealed record") == 4,
            "Exactly actor ID, session, membership and context evidence records required")
    require("MySqlConnection" not in domain and "MySqlConnection" not in app and
            "ConnectionStringBuilder" not in domain and
            "ConnectionStringBuilder" not in app,
            "Identity context cannot open a database or select DSN")
    require("using Microsoft" not in domain and "using Microsoft" not in app and
            "HttpContext" not in app and "HttpRequest" not in app,
            "Domain/Application must not depend on HTTP framework")
    require("QueryString" not in app and "X-Tenant-Id" not in app.split("public sealed class")[1],
            "No tenant selector can enter authenticated backend use case")
    require(app.count("sessions.ReadCurrentVerifiedSessionAsync(cancellationToken)") == 1,
            "Exactly one trusted authenticated actor lookup required")
    require(app.count("membershipReader.FindExactMembershipsAsync(") == 1,
            "One exact membership lookup required")
    require(app.count("verifiedHostResolver.ResolveAsync(host, cancellationToken)") == 2,
            "Host must be verified twice around membership+tenant lookups")
    require(app.index("sessions.ReadCurrentVerifiedSessionAsync") <
            app.index("currentHost.ReadCurrentDirectHostAsync") <
            app.index("membershipReader.FindExactMembershipsAsync") <
            app.index("tenants.FindByIdAsync") <
            app.index("var recheckedHost ="),
            "Authentication/host/membership/catalog order must not be bypassed")
    require(app.index("matches.Count != 1") <
            app.index("tenants.FindByIdAsync"), "Reject ambiguous grants before tenant catalog")
    require(app.index("if (recheckedHost != hostHint)") <
            app.index("TenantIdentityContextEvidence.FromVerifiedInternalEvidence("),
            "Rebinding must reject before emitting context")
    require("TestActorReader" in tests and "TestMembershipReader" in tests and
            "TestHostCatalog" in tests,
            "Test doubles must be explicit, not masquerade as production auth")
    require("actions/checkout@" in workflow and "actions/setup-dotnet@" in workflow and
            "deploy" not in workflow.lower() and "docker run" not in workflow,
            "CI must not deploy/provision/perform external DNS or infrastructure actions")
    api = (ROOT / "backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
    for name in ("ResolveCurrentTenantIdentityContext",
                 "ITrustedCurrentActorSessionReader",
                 "ITrustedActorTenantMembershipReader",
                 "TenantIdentityContextEvidence"):
        require(name not in api, "Private tenant identity route/DI introduced: " + name)
    for source in (ROOT / "backend/src/Crecisol.Infrastructure").rglob("*.cs"):
        text = source.read_text(encoding="utf-8")
        require("ITrustedCurrentActorSessionReader" not in text and
                "ITrustedActorTenantMembershipReader" not in text,
                "Unreviewed production authentication/membership adapter enabled")


def audit_a() -> None:
    require(canonical_active_word(), "Canonical Word and PDF digests changed")
    paragraphs = chapter4()
    require(len(paragraphs) == 72, "C04 canonical paragraph count changed")
    source = "\n".join(p for _, p in paragraphs)
    for item in ("SHARED A/B y DEDICATED C",
                 "nunca confiar en tenant_id aportado por navegador",
                 "FK compuestas con tenant"):
        require(item in source, "Missing authority in canonical C04: " + item)
    plan = (ROOT / "docs/planificacion-futura/C04_MICROFASES_PROPUESTAS.md").read_text(
        encoding="utf-8")
    require("| C04-05 | Contexto tenant por identidad |" in plan,
            "Original C04-05 phase name changed")
    require("No usar tenant_id como permiso" in plan,
            "Original C04-05 negative requirement missing")
    guard(load())
    print("PASS C04-05 A: pinned Word/PDF and 72 C04 paragraphs, "
          "signed-actor port, verified host, exact membership and active tenant, "
          "no API/production authentication")


def audit_b() -> None:
    data = load()
    guard(data)
    rejected = 0
    for key, markers in MARKERS.items():
        for marker in markers:
            mutated = dict(data)
            mutated[key] = mutated[key].replace(marker, "[REMOVED SECURITY BOUNDARY]")
            try:
                guard(mutated)
            except AssertionError:
                rejected += 1
            else:
                raise AssertionError("A security-guard mutation was accepted: " +
                                     key + ": " + marker)
    print(f"PASS C04-05 B: {rejected} adversarial single-guard mutations rejected; "
          "no tenant_id permission, duplicate A/B grants or new private API")


def audit_c() -> None:
    require(os.environ.get("GITHUB_ACTIONS") == "true",
            "C requires real GitHub Actions execution")
    sha = os.environ.get("GITHUB_SHA", "")
    run = os.environ.get("GITHUB_RUN_ID", "")
    require(bool(re.fullmatch("[0-9a-f]{40}", sha) and re.fullmatch("[0-9]+", run)),
            "Missing SHA or run ID")
    for stage in ("A", "B", "dotnet"):
        file = PROOF / stage
        require(file.is_file() and file.read_text(encoding="utf-8").strip() ==
                f"PASS {stage} sha={sha} run={run}",
                "Stale/missing same-run evidence: " + stage)
    for path in (
        ROOT / "backend/src/Crecisol.Domain/Crecisol.Domain.csproj",
        ROOT / "backend/src/Crecisol.Application/Crecisol.Application.csproj",
        ROOT / "backend/tests/Crecisol.TenantIdentity.ContractsTests/Crecisol.TenantIdentity.ContractsTests.csproj",
    ):
        require("<TargetFramework>net10.0</TargetFramework>" in
                path.read_text(encoding="utf-8"),
                "C04-05 requires real .NET10: " + str(path))
    app_project = (ROOT / "backend/src/Crecisol.Application/Crecisol.Application.csproj").read_text(
        encoding="utf-8")
    require("Crecisol.Domain.csproj" in app_project and
            "Crecisol.Infrastructure.csproj" not in app_project,
            "Clean Architecture Domain->Application must remain independent")
    guard(load())
    print("PASS C04-05 C: .NET10 Release tests and all three audits bound to same SHA/run; "
          "no new DI, HTTP, identity adapter, membership database or resource permissions")


def main() -> None:
    require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
            "Usage: qa/c04_05_identity_audits.py A|B|C")
    {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, OSError, ValueError) as error:
        print("FAIL C04-05:", error, file=sys.stderr)
        sys.exit(1)
