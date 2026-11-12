#!/usr/bin/env python3
"""CRECISOL C04-06: normative A, branch/resource security B, same-SHA .NET10 C.

A: pinned unique Word/PDF, 72 C04 paragraphs and original C04-06 scope.
B: adversarial SOURCE mutations of guards + no early private API/adapters.
C: actual .NET 10 Release tests and A/B proof in the SAME CI SHA/run.
No real session auth, branch grant persistence, HTTP BOLA, atomic DB read,
cloud deployment, DNS or production safety is certified here.
"""
from __future__ import annotations

import os
import re
import sys

from c04_01_normative_inventory import EXPECTED, chapter4
from c04_01_preparation_audits import ROOT, canonical_active_word

PATHS = {
    "domain": ROOT / "backend/src/Crecisol.Domain/Tenancy/TenantBranchResourceScope.cs",
    "app": ROOT / "backend/src/Crecisol.Application/Tenancy/ResolveCurrentBranchResourceScope.cs",
    "test": ROOT / "backend/tests/Crecisol.BranchResource.ContractsTests/Program.cs",
    "spec": ROOT / "docs/c04/C04_06_ESPECIFICACION.md",
    "workflow": ROOT / ".github/workflows/c04-06-branch-resource-scope.yml",
}
PROOF = ROOT / ".c04-06-scope-proof"

MARKERS = {
    "domain": (
        "public sealed record TenantBranchId",
        "public sealed record TenantResourceId",
        "Guid.Empty",
        "public sealed record TenantRequiredAction",
        "FromServerPolicy(string? raw)",
        "parts.Length is < 2 or > 4",
        "part[0] is < 'a' or > 'z'",
        "TenantRequiredAction[redacted]",
        "public enum TrustedResourceState",
        "Suspended = 2",
        "Deleted = 3",
        "public sealed record TrustedBranchResourceSnapshot",
        "RestoreFromTrustedResourceReader(",
        "TrustedBranchResourceSnapshot.RequireUtc",
        "State == TrustedResourceState.Active && UpdatedAtUtc <= nowUtc",
        "public enum BranchGrantState",
        "Pending = 1",
        "Revoked = 4",
        "public enum BranchGrantScope",
        "OwnerOnly = 1",
        "BranchWide = 2",
        "public sealed record TrustedActorBranchGrantSnapshot",
        "RestoreFromTrustedGrantReader(",
        "state == BranchGrantState.Active && !grantedAtUtc.HasValue",
        "state == BranchGrantState.Pending && grantedAtUtc.HasValue",
        "ChangedAtUtc <= nowUtc && GrantedAtUtc.Value <= nowUtc",
        "nowUtc < ValidUntilUtc.Value",
        "public sealed record TenantBranchResourceScopeEvidence",
        "identity.ActorId != grant.ActorId",
        "identity.TenantId != resource.TenantId",
        "identity.TenantId != grant.TenantId",
        "resource.BranchId != grant.BranchId",
        "grant.RequiredAction != requiredAction",
        "!resource.IsActiveAtUtc(nowUtc)",
        "!grant.IsActiveAtUtc(nowUtc)",
        "grant.Scope == BranchGrantScope.OwnerOnly",
        "resource.OwnerActorId != identity.ActorId",
        "TenantBranchResourceScopeEvidence[redacted]",
    ),
    "app": (
        "interface ITrustedTenantScopedResourceReader",
        "FindByTenantAndResourceIdAsync(",
        "TenantId authorizedTenantId, TenantResourceId resourceId",
        "interface ITrustedActorBranchGrantReader",
        "FindExactGrantsAsync(",
        "TenantActorId authenticatedActorId, TenantId authorizedTenantId",
        "TenantBranchId resourceBranchId, TenantRequiredAction requiredAction",
        "ResolveCurrentTenantIdentityContext identityResolver",
        "public async Task<TenantBranchResourceScopeEvidence> ExecuteAsync(",
        "TenantResourceId resourceId,",
        "TenantRequiredAction requiredAction,",
        "ArgumentNullException.ThrowIfNull(resourceId)",
        "ArgumentNullException.ThrowIfNull(requiredAction)",
        "var identity = await identityResolver.ExecuteAsync(cancellationToken)",
        "identity.TenantId, resourceId, cancellationToken",
        "resource.TenantId != identity.TenantId",
        "resource.ResourceId != resourceId",
        "!resource.IsActiveAtUtc(startUtc)",
        "identity.ActorId, identity.TenantId, resource.BranchId,",
        "candidates is null || candidates.Count != 1 || candidates[0] is null",
        "grant.ActorId != identity.ActorId",
        "grant.TenantId != identity.TenantId",
        "grant.BranchId != resource.BranchId",
        "grant.RequiredAction != requiredAction",
        "grant.Scope == BranchGrantScope.OwnerOnly",
        "resource.OwnerActorId != identity.ActorId",
        "var freshResource = await resources.FindByTenantAndResourceIdAsync(",
        "if (freshResource != resource)",
        "var freshCandidates = await grants.FindExactGrantsAsync(",
        "freshCandidates.Count != 1",
        "freshCandidates[0] != grant",
        "var refreshedIdentity = await identityResolver.ExecuteAsync(",
        "if (refreshedIdentity != identity)",
        "finishUtc < startUtc",
        "!resource.IsActiveAtUtc(finishUtc)",
        "!grant.IsActiveAtUtc(finishUtc)",
        "TenantBranchResourceScopeEvidence.FromTrustedInternalSnapshots(",
    ),
    "test": (
        "Positive A owner-only evidence scoped to exact tenant/branch/resource",
        "Explicit branch-wide grant permits resource owned by B in same branch",
        "Positive tenant B has independent branch/owner/actor evidence",
        "Same tenant other branch requires its own exact grant",
        "A new action is allowed only with explicit matching permission record",
        "Cross-tenant B resource ID rejected by tenant A",
        "Another actor's grant cannot authorize A",
        "Another branch grant cannot authorize A branch",
        "Wrong branch grant cannot authorize resource in branch B",
        "A read grant cannot authorize update action",
        "OwnerOnly grant cannot authorize other owner's resource",
        "Duplicate grant is ambiguous even for same actor",
        "Grant ambiguity across branch/owner modes denied",
        "Suspended resource cannot be selected",
        "Revoked grant fails",
        "Resource owner changed mid-request fails",
        "Reassignment from branch A to B mid-request fails",
        "Grant revoked mid-request fails",
        "Grant revision changed mid-request fails",
        "Grant expires during asynchronous work",
        "C04-05 actor revision change during work fails",
        "Cancellation after grant recheck denies evidence",
        "Resource denial happens before grant lookup",
        "Grant lookup requires actor+tenant+resource branch+backend action",
        "NO production membership, branch authorization DB, real private HTTP BOLA",
    ),
    "spec": (
        EXPECTED,
        "C04-06 EN DESARROLLO",
        "C04-07 BLOQUEADA",
        "No heredar permisos de otra sucursal",
        "no cambios a",
        "NO garantiza atomicidad",
    ),
    "workflow": (
        "dotnet-version: '10.0.x'",
        "qa/c04_06_scope_audits.py A",
        "qa/c04_06_scope_audits.py B",
        "qa/c04_06_scope_audits.py C",
        "Crecisol.BranchResource.ContractsTests.csproj",
        "dotnet run --no-build",
        "persist-credentials: false",
        "contents: read",
    ),
}


def require(condition: bool, reason: str) -> None:
    if not condition:
        raise AssertionError(reason)


def load() -> dict[str, str]:
    return {kind: path.read_text(encoding="utf-8") for kind, path in PATHS.items()}


def guard(data: dict[str, str]) -> None:
    for kind, markers in MARKERS.items():
        for marker in markers:
            require(marker in data[kind], f"{kind}: missing scoped trust check: {marker}")

    domain, app, tests, workflow = (
        data["domain"], data["app"], data["test"], data["workflow"])
    require(domain.count("public sealed record") == 6,
            "Six precise typed contracts required, no catchall cross-tenant context")
    require("MySqlConnection" not in domain and "MySqlConnection" not in app and
            "ConnectionStringBuilder" not in domain and
            "ConnectionStringBuilder" not in app,
            "Branch scope cannot construct DB connection/DSN")
    require("using Microsoft" not in domain and "using Microsoft" not in app and
            "HttpContext" not in app and "HttpRequest" not in app,
            "Domain/Application must not depend on HTTP framework")
    require(app.count("identityResolver.ExecuteAsync(cancellationToken)") == 2,
            "C04-05 identity context must be rechecked during authorization")
    require(app.count("resources.FindByTenantAndResourceIdAsync(") == 2,
            "Scoped resource metadata must be re-read before returning evidence")
    require(app.count("grants.FindExactGrantsAsync(") == 2,
            "Exact branch/actor/action grant must be re-read for concurrent revocation")
    require(app.index("var identity = await identityResolver.ExecuteAsync(") <
            app.index("var resource = await resources.FindByTenantAndResourceIdAsync(") <
            app.index("var candidates = await grants.FindExactGrantsAsync(") <
            app.index("var freshResource = await resources.FindByTenantAndResourceIdAsync(") <
            app.index("var freshCandidates = await grants.FindExactGrantsAsync(") <
            app.index("var refreshedIdentity = await identityResolver.ExecuteAsync(") <
            app.index("var finishUtc = clock.GetUtcNow();") <
            app.index("return TenantBranchResourceScopeEvidence.FromTrustedInternalSnapshots("),
            "Identity->resource->grant->resource->grant->identity->UTC order required")
    require(app.index("candidates.Count != 1") <
            app.index("var freshResource ="),
            "Any absent/duplicate grant must reject before recheck")
    require(app.index("if (refreshedIdentity != identity)") <
            app.index("return TenantBranchResourceScopeEvidence.FromTrustedInternalSnapshots("),
            "Changing authenticated identity/tenant cannot return evidence")
    require("FakeResourceReader" in tests and "FakeGrantReader" in tests and
            "FakeVerifiedHostCatalog" in tests,
            "Tests must state their synthetic server-side nature")
    require("deploy" not in workflow.lower() and
            "docker run" not in workflow and
            "curl " not in workflow and
            "actions/checkout@" in workflow and
            "actions/setup-dotnet@" in workflow,
            "CI read-only: no deploy, remote network or secret mutation")
    api = (ROOT / "backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
    for marker in ("ResolveCurrentBranchResourceScope",
                   "ITrustedActorBranchGrantReader",
                   "ITrustedTenantScopedResourceReader",
                   "TenantBranchResourceScopeEvidence"):
        require(marker not in api, "Premature private endpoint/DI in Program.cs: " + marker)
    for source in (ROOT / "backend/src/Crecisol.Infrastructure").rglob("*.cs"):
        body = source.read_text(encoding="utf-8")
        require("ITrustedActorBranchGrantReader" not in body and
                "ITrustedTenantScopedResourceReader" not in body,
                "Unexpected production resource/grant adapter introduced")


def audit_a() -> None:
    require(canonical_active_word(), "Canonical Word/PDF SHA mismatch")
    paragraphs = chapter4()
    require(len(paragraphs) == 72, "Original C04 72 paragraphs changed")
    text = "\n".join(s for _, s in paragraphs)
    for phrase in ("SHARED A/B y DEDICATED C",
                   "nunca confiar en tenant_id aportado por navegador",
                   "FK compuestas con tenant"):
        require(phrase in text, "Canonical C04 authority missing: " + phrase)
    plan = (ROOT / "docs/planificacion-futura/C04_MICROFASES_PROPUESTAS.md").read_text(
        encoding="utf-8")
    require("| C04-06 | Contexto de sucursal y recurso |" in plan,
            "Canonical C04-06 label changed")
    require("Scopes por branch/owner en backend" in plan and
            "No heredar permisos de otra sucursal" in plan,
            "C04-06 positive/negative contract was changed")
    guard(load())
    print("PASS C04-06 A: unchanged master Word/PDF and 72 C04 paragraphs, "
          "typed tenant/branch/resource/owner/action, no new private HTTP/DI")


def audit_b() -> None:
    data = load()
    guard(data)
    rejected = 0
    for kind, markers in MARKERS.items():
        for marker in markers:
            mutant = dict(data)
            mutant[kind] = mutant[kind].replace(marker, "[REMOVED_BRANCH_TRUST_GUARD]")
            try:
                guard(mutant)
            except AssertionError:
                rejected += 1
            else:
                raise AssertionError("Guard mutation went undetected: " + kind +
                                     ": " + marker)
    print(f"PASS C04-06 B: {rejected} branch/owner/action trust-guard "
          "mutations rejected, exact single grant and no cross-branch inheritance")


def audit_c() -> None:
    require(os.environ.get("GITHUB_ACTIONS") == "true",
            "Audit C requires actual GitHub Actions runner")
    sha = os.environ.get("GITHUB_SHA", "")
    run = os.environ.get("GITHUB_RUN_ID", "")
    require(bool(re.fullmatch(r"[0-9a-f]{40}", sha) and
                 re.fullmatch(r"[0-9]+", run)), "Missing valid source SHA/run")
    for stage in ("A", "B", "dotnet"):
        proof = PROOF / stage
        require(proof.is_file() and proof.read_text(encoding="utf-8").strip() ==
                f"PASS {stage} sha={sha} run={run}",
                "Missing/stale same-SHA/run proof: " + stage)
    for path in (
        ROOT / "backend/src/Crecisol.Domain/Crecisol.Domain.csproj",
        ROOT / "backend/src/Crecisol.Application/Crecisol.Application.csproj",
        ROOT / "backend/tests/Crecisol.BranchResource.ContractsTests/Crecisol.BranchResource.ContractsTests.csproj",
    ):
        require("<TargetFramework>net10.0</TargetFramework>" in
                path.read_text(encoding="utf-8"),
                "Not a real .NET10 project graph: " + str(path))
    app_project = (ROOT / "backend/src/Crecisol.Application/Crecisol.Application.csproj").read_text(
        encoding="utf-8")
    require("Crecisol.Domain.csproj" in app_project and
            "Crecisol.Infrastructure.csproj" not in app_project,
            "Domain/Application Clean Architecture was bypassed")
    test_project = (ROOT / "backend/tests/Crecisol.BranchResource.ContractsTests/Crecisol.BranchResource.ContractsTests.csproj").read_text(
        encoding="utf-8")
    require("Crecisol.Application.csproj" in test_project,
            "C04-06 tests must compile actual Application project")
    guard(load())
    print("PASS C04-06 C: all A/B/.NET10 checks share GitHub SHA/run, "
          "no private endpoints/production auth or atomic DB access claimed")


def main() -> None:
    require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
            "Usage: qa/c04_06_scope_audits.py A|B|C")
    {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, OSError, ValueError) as error:
        print("FAIL C04-06:", error, file=sys.stderr)
        sys.exit(1)
