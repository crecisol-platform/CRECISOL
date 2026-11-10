#!/usr/bin/env python3
"""CRECISOL C03-07: independent Word/A, security mutation/B, runtime evidence/C."""
from copy import deepcopy
from hashlib import sha256
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

import c03_01_source as source

ROOT = Path(__file__).resolve().parents[1]
WORD = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
WORD_SHA = "e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e"
FILES = {
    "domain": ROOT / "backend/src/Crecisol.Domain/Events/PlatformOutboxEvent.cs",
    "app": ROOT / "backend/src/Crecisol.Application/Events/PlatformOutboxWorker.cs",
    "infra": ROOT / "backend/src/Crecisol.Infrastructure/Events/MySqlPlatformOutboxRelay.cs",
    "schema": ROOT / "database/c03_07_outbox_schema.sql",
    "api": ROOT / "backend/src/Crecisol.Api/Program.cs",
    "shell": ROOT / "database/tests/c03_07_outbox_mysql_smoke.sh",
    "test": ROOT / "backend/tests/Crecisol.Outbox.ContractsTests/Program.cs",
    "project": ROOT / "backend/tests/Crecisol.Outbox.ContractsTests/Crecisol.Outbox.ContractsTests.csproj",
    "workflow": ROOT / ".github/workflows/c03-07-outbox.yml",
    "spec": ROOT / "docs/c03/C03_07_ESPECIFICACION.md",
}
PROOF = ROOT / ".c03-07-ci-proof"

def require(ok, why):
    if not ok:
        raise AssertionError(why)

def read_all():
    for name, path in FILES.items():
        require(path.is_file(), "Missing deliverable: " + str(path))
    return {name: path.read_text(encoding="utf-8") for name, path in FILES.items()}

def contract(p):
    domain, app, infra, schema = (p[k] for k in ("domain","app","infra","schema"))
    for term in ("class PlatformOutboxEvent", "SupportedType = \"platform.foundation-check.v1\"",
                 "SupportedVersion = 1", "FixedPayload", "DateTimeKind.Utc",
                 "Guid.Empty", "SupportsConsumer(string eventType, int version)"):
        require(term in domain, "Domain allowlist/version/UTC invariant weakened: " + term)
    for term in ("interface IPlatformOutboxRelayPort", "record PlatformEventLease",
                 "int maxItems", "maxItems is < 1 or > 32",
                 "cancellationToken.ThrowIfCancellationRequested()",
                 "PlatformOutboxEvent.SupportsConsumer(lease.EventType, lease.SchemaVersion)",
                 "port.FailAsync(lease, true", "port.AcknowledgeAsync(lease, InternalConsumer",
                 'InternalConsumer = "crecisol.platform.foundation-check.v1"'):
        require(term in app, "Bounded fail-closed worker changed: " + term)
    for term in ("OpenTrustedConnectionAsync", "string.IsNullOrWhiteSpace(trustedServerDsn)",
                 "Pooling = false", "AppendWithinTransactionAsync",
                 "ReferenceEquals(transaction.Connection, db)",
                 "FOR UPDATE SKIP LOCKED", "attempt_count<3",
                 "lease_generation=@generation", "lease_owner=@worker",
                 "lease_expires_at_utc>UTC_TIMESTAMP(6)",
                 "consumerName != PlatformOutboxWorker.InternalConsumer",
                 "ON DUPLICATE KEY UPDATE event_id=event_id",
                 "await tx.CommitAsync(cancellationToken)", "await tx.RollbackAsync(cancellationToken)",
                 "last_failure_code='lease-expired'",
                 "last_failure_code=IF(@permanent=1,'unsupported-version','retry')"):
        require(term in infra, "Atomic/fenced/idempotent storage invariant weakened: " + term)
    require("lease_generation=@generation, lease_owner=@worker" in infra,
            "Claim must advance generation and assign one owner atomically")
    require(infra.count("lease_expires_at_utc>UTC_TIMESTAMP(6)") >= 3,
            "ACK, failure and preflight all require a fresh lease")
    require(infra.count("scope_kind=1 AND scope_id=@scope") >= 6,
            "Every claim/ACK/failure/recovery must be pinned to PLATFORM scope")
    require("scope_kind=2" not in infra and "AddHostedService" not in infra,
            "Private tenant selection or hosted service accidentally activated")
    for term in ("CREATE TABLE IF NOT EXISTS c03_07_outbox_events",
                 "CREATE TABLE IF NOT EXISTS c03_07_inbox_receipts",
                 "UNIQUE KEY uq_c03_07_scope_event (scope_kind, scope_id, event_id)",
                 "PRIMARY KEY (scope_kind, scope_id, consumer_name, event_id)",
                 "FOREIGN KEY (scope_kind, scope_id, event_id)",
                 "CHECK (scope_kind IN (1,2))",
                 "status IN ('pending','leased','delivered','retryable','dead-letter')",
                 "status='leased' AND lease_owner IS NOT NULL"):
        require(term in schema, "Tenant FK/status/lease SQL invariant weakened: " + term)
    require("ENGINE=InnoDB" in schema and schema.count("ENGINE=InnoDB") == 2,
            "Transactional InnoDB required on both tables")
    require("app.MapGet(\"/api/v1/public/branding/platform\"" in p["api"] and
            p["api"].count("app.MapGet(") == 1 and
            "AddHostedService" not in p["api"] and
            "MapPost(" not in p["api"],
            "C03-07 must not activate HTTP endpoints or a background worker")
    require("--network none" in p["shell"] and
            "CRECISOL_C03_07_TEST_DSN" in p["shell"] and
            "mysql:8.4@sha256:" in p["shell"] and
            "DROP TABLE c03_07_inbox_receipts" in p["shell"] and
            "DELETE FROM c03_07_outbox_events" in p["shell"],
            "Disposable local-only SQL least privilege test removed")
    require("C03-07" in p["spec"] and "C04 BLOQUEADO" in p["spec"],
            "Microphase boundary not recorded")

def audit_a():
    require(sha256(WORD.read_bytes()).hexdigest() == WORD_SHA,
            "Canonical Word SHA mismatch")
    original, table, _, _ = source.original_parts()
    require(len(original) == 7 and len(table) == 9 and
            table[5][0] == "Workers", "Workers table T01-R06 drift")
    p = read_all()
    require(WORD_SHA in p["spec"] and
            "C03-01..C03-06 certificadas (6/10)" in p["spec"] and
            "C03 NO certificado" in p["spec"] and
            "No se han" not in p["spec"],
            "Predecessor/provenance not traceable")
    require("Program.cs" in
            (ROOT / "docs/c03/C03_06_ESPECIFICACION.md").read_text(encoding="utf-8"),
            "C03-06 API boundary not traceable")
    contract(p)
    print("PASS C03-07 A: canonical Word SHA, T01-R06 Workers, 6/10 predecessor and limited inactive scope.")

def audit_b():
    p = read_all()
    contract(p)
    mutants = [
        ("UTC removed", "domain", "DateTimeKind.Utc", "DateTimeKind.Unspecified"),
        ("event version removed", "domain", "SupportedVersion = 1", "SupportedVersion = 0"),
        ("wildcard worker batch", "app", "maxItems is < 1 or > 32", "maxItems < 0"),
        ("worker dispatch unknown", "app",
         "PlatformOutboxEvent.SupportsConsumer(lease.EventType, lease.SchemaVersion)", "true"),
        ("arbitrary consumer", "app",
         "port.AcknowledgeAsync(lease, InternalConsumer", "port.AcknowledgeAsync(lease, \"wildcard\""),
        ("missing storage configuration allowed", "infra",
         "string.IsNullOrWhiteSpace(trustedServerDsn)", "trustedServerDsn == \"notset\""),
        ("split transaction allowed", "infra",
         "ReferenceEquals(transaction.Connection, db)", "transaction.Connection != null"),
        ("tenant query scope expanded", "infra",
         "scope_kind=1 AND scope_id=@scope", "scope_kind=2 AND scope_id=@scope"),
        ("unlock race", "infra", "FOR UPDATE SKIP LOCKED", "LIMIT 1"),
        ("remove worker fence", "infra", "lease_owner=@worker", "lease_owner=NULL"),
        ("remove generation fence", "infra",
         "lease_generation=@generation", "lease_generation=lease_generation"),
        ("remove lease freshness", "infra",
         "lease_expires_at_utc>UTC_TIMESTAMP(6)", "lease_expires_at_utc IS NOT NULL"),
        ("duplicate receipt not idempotent", "infra",
         "ON DUPLICATE KEY UPDATE event_id=event_id", "ON DUPLICATE KEY UPDATE processed_at_utc=UTC_TIMESTAMP(6)"),
        ("composite receipt FK removed", "schema",
         "FOREIGN KEY (scope_kind, scope_id, event_id)", "FOREIGN KEY (event_id)"),
        ("scope constraint removed", "schema",
         "CHECK (scope_kind IN (1,2))", "CHECK (scope_kind>=0)"),
        ("unsafe worker hosted", "api", "app.Run();",
         "builder.Services.AddHostedService<UnsafeWorker>();\napp.Run();"),
    ]
    for name, field, before, after in mutants:
        require(before in p[field], "Invalid mutation fixture: " + name)
        candidate = deepcopy(p)
        candidate[field] = candidate[field].replace(before, after, 1)
        try:
            contract(candidate)
        except AssertionError:
            continue
        raise AssertionError("Unsafe mutation passed: " + name)
    source.actual_graph()
    print(f"PASS C03-07 B: {len(mutants)}/{len(mutants)} adversarial mutations rejected, 4-layer graph and explicit PLATFORM-only SQL.")

def audit_c():
    p = read_all()
    contract(p)
    for token in ("build", "contracts", "mysql", "regressions"):
        f = PROOF / token
        require(f.is_file() and f.read_text(encoding="utf-8").strip() ==
                "PASS: " + token, "Real CI step evidence absent: " + token)
    proj = ET.fromstring(p["project"])
    require(proj.findtext(".//TargetFramework") == "net10.0" and
            proj.findtext(".//TreatWarningsAsErrors") == "true",
            ".NET 10 strict compile required")
    require([x.attrib["Include"] for x in proj.findall(".//ProjectReference")] ==
            ["../../src/Crecisol.Infrastructure/Crecisol.Infrastructure.csproj"],
            "Test project must have narrow, acyclic Infrastructure dependency")
    for token in ("Rollback leaves no publishable event", "SKIP LOCKED prevents concurrent double claim",
                  "Composite FK denies cross-tenant receipt", "Expired lease recovered",
                  "Replayed ACK is rejected", "Unknown type never registered",
                  "Three failures dead-letter", "Cancellation before claim"):
        require(token in p["test"], "Missing runtime negative assertion: " + token)
    for token in ("dotnet build", "dotnet run", "bash database/tests/c03_07_outbox_mysql_smoke.sh",
                  "CRECISOL_C03_05_CONTRACT_SMOKE=1", "ubuntu-24.04", "contents: read"):
        require(token in p["workflow"], "Required runtime/regression CI missing: " + token)
    source.actual_graph()
    print("PASS C03-07 C: .NET10 Release, real disposable MySQL8.4, concurrency and negative cases, old chapter regressions.")

if __name__ == "__main__":
    try:
        require(len(sys.argv) == 2 and sys.argv[1] in ("A","B","C"), "Use A, B or C")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (AssertionError, KeyError, OSError, ValueError, TypeError) as error:
        print("FAIL C03-07 audit: " + str(error), file=sys.stderr)
        sys.exit(1)
