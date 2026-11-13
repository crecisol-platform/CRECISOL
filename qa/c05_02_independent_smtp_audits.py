#!/usr/bin/env python3
"""C05-02 independent synthetic SMTP receiver: A/B/C with exact runner SHA."""
from __future__ import annotations
import json
import os
from pathlib import Path
import re
import subprocess
import sys
from c05_01_preparation_audits import ROOT
from c05_02_proof_outbox_audits import audit_a as predecessor_a

REPO = "crecisol-platform/CRECISOL"
FEATURE = "feature/c05-02-independent-smtp-recipient-20261113"
BASE = "407d233b3c490d0c2c4e8e2c8611e63b058be558"
EVIDENCE = ROOT / ".c05-02-independent-smtp"
FILES = {
    "sender": "backend/src/Crecisol.Infrastructure/Identity/LoopbackOnlySmtpProofSink.cs",
    "receiver": "backend/tests/Crecisol.LoopbackSmtpReceiver/Program.cs",
    "receiver_project": "backend/tests/Crecisol.LoopbackSmtpReceiver/Crecisol.LoopbackSmtpReceiver.csproj",
    "test": "backend/tests/Crecisol.IndependentMailbox.MySqlTests/Program.cs",
    "test_project": "backend/tests/Crecisol.IndependentMailbox.MySqlTests/Crecisol.IndependentMailbox.MySqlTests.csproj",
    "mysql": "database/tests/c05_02_independent_receiver_mysql_smoke.sh",
    "doc": "docs/c05/INTEGRACION_RECEPTOR_SMTP_INDEPENDIENTE_C05_02.md",
    "audit": "qa/c05_02_independent_smtp_audits.py",
    "ci": ".github/workflows/c05-02-independent-smtp.yml",
}
ALLOWED = set(FILES.values())
GUARDS = {
    "sender": (
        "class LoopbackOnlySmtpProofSink",
        "ITrustedLocalProofMailboxSink",
        "receiverPort is < 1024 or > 65535",
        "capability.Length != 32",
        "CanonicalMailboxOrNull(",
        "storedCanonicalMailbox, StringComparison.Ordinal",
        '"@example.test"',
        "new SmtpClient(IPAddress.Loopback.ToString(), port)",
        "DeliveryMethod = SmtpDeliveryMethod.Network",
        "EnableSsl = false",
        "UseDefaultCredentials = false",
        "CancellationToken cancellationToken",
        "await sender.SendMailAsync(message, cancellationToken)",
        "cancellationToken.ThrowIfCancellationRequested()",
        "LoopbackOnlySmtpProofSink[redacted]",
    ),
    "receiver": (
        "TcpListener(IPAddress.Loopback, 0)",
        "Console.Out.WriteLine($\"READY {port}\")",
        "ReceiveTimeout = 5000",
        "SendTimeout = 5000",
        "line.Length > 512",
        "commands < 25",
        "MAIL FROM:<no-reply@crecisol.example.test>",
        "recipient is not null",
        "candidate.EndsWith(\"@example.test\", StringComparison.Ordinal)",
        "candidate.Contains(' ')",
        "candidate.Count(ch => ch == '@') != 1",
        "total < 4096",
        'Regex.IsMatch(data, @"\\A[A-F0-9]{64}\\z"',
        "Console.Out.WriteLine($\"MAIL {recipient} {hex}\")",
        "catch (Exception)",
        "No secret is written to disk",
    ),
    "test": (
        'args[0] != "--integration"',
        "MySqlDisposableProofDeliveryOutbox",
        "MySqlDisposableNeutralRegistrationRecorder",
        "MySqlDisposableEmailPossessionAdapter",
        "LoopbackOnlySmtpProofSink",
        "RedirectStandardOutput = true",
        "RedirectStandardError = true",
        "receiver.StandardOutput.ReadLineAsync()",
        "ProcessOnceAsync(tenantA,a1",
        "TryVerifyOnceAsync(",
        "Possessing only the address does not verify the channel",
        "Same code cannot verify the wrong tenant",
        "An already-used challenge cannot be replayed",
        "A SENT outbox cannot dispatch a replay",
        "Header injection cannot change SMTP envelope",
        "External mailbox is denied before opening TCP",
        "Production SMTP port 25 is rejected",
        "Unavailable independent SMTP receiver fails delivery closed",
        "CryptographicOperations.ZeroMemory(aCode)",
        "CryptographicOperations.ZeroMemory(bCode)",
        "CryptographicOperations.ZeroMemory(master)",
        "Neither SMTP receipt nor proof verification creates identity",
        "Delivery outbox has no plaintext recipient or bearer column",
    ),
    "mysql": (
        'mysql:8.4@sha256:',
        "--network bridge -p '127.0.0.1::3306'",
        "openssl rand -hex 32",
        "c05_02_delivery_outbox_atomic_fixture.sql",
        "GRANT EXECUTE ON PROCEDURE",
        "CRECISOL_C05_02_OUTBOX_WORKER_DSN",
        "Crecisol.IndependentMailbox.MySqlTests",
        "docker rm -f",
    ),
    "doc": (
        "C05-02 NO CERTIFICADA",
        "C05-03 bloqueada",
        "No demuestra entrega a inbox real",
        "SmtpClient",
        "receptor en otro proceso",
        "127.0.0.1",
        "Rollback:",
        BASE,
    ),
    "ci": (
        "contents: read",
        "persist-credentials: false",
        "dotnet-version: '10.0.x'",
        "c05_02_independent_smtp_audits.py A",
        "c05_02_independent_smtp_audits.py B",
        "c05_02_independent_smtp_audits.py C",
        "c05_02_independent_receiver_mysql_smoke.sh",
    ),
    "receiver_project": (
        "<TargetFramework>net10.0</TargetFramework>",
        "<TreatWarningsAsErrors>true</TreatWarningsAsErrors>",
    ),
    "test_project": (
        "<TargetFramework>net10.0</TargetFramework>",
        "<TreatWarningsAsErrors>true</TreatWarningsAsErrors>",
        "Crecisol.Infrastructure.csproj",
    ),
}

def need(condition: bool, reason: str) -> None:
    if not condition:
        raise AssertionError(reason)

def sources() -> dict[str,str]:
    return {k:(ROOT / v).read_text(encoding="utf-8") for k,v in FILES.items()}

def invariants(data: dict[str,str]) -> None:
    for kind, markers in GUARDS.items():
        for marker in markers:
            need(marker in data[kind], "Missing C05-02 " + kind + " security guard: " + marker)
    need("MapPost(" not in data["sender"] and
         "HttpContext" not in data["sender"] and
         "IConfiguration" not in data["sender"] and
         "InsertMembership" not in data["sender"] and
         "AccountExists" not in data["sender"],
         "Test SMTP sender may not authorize or expose an API")
    need("IPAddress.Any" not in data["receiver"] and
         "IPAddress.IPv6Any" not in data["receiver"] and
         "File.WriteAllText" not in data["receiver"] and
         "File.AppendAllText" not in data["receiver"],
         "Independent test receiver must remain bound to loopback and memory")
    need("GRANT ALL" not in data["mysql"] and
         "GRANT UPDATE ON \`$DB\`.c05_02_delivery_outbox TO 'c05_02_outbox_worker'" not in data["mysql"],
         "Worker SQL role may not mutate protected tables directly")
    need("workflow_dispatch:" not in data["ci"] and
         "id-token: write" not in data["ci"] and
         "deploy" not in data["ci"].lower(),
         "CI may not deploy or request write token")
    need("C05-02 NO CERTIFICADA" in data["doc"] and
         "No demuestra entrega a inbox real" in data["doc"],
         "Must disclose external SMTP recipient limitation")
    api = (ROOT / "backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
    routes = re.findall(r'app\.Map(?:Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"',api)
    need(routes == ["/api/v1/public/branding/platform"],
         "Must not activate production registration or email endpoint")

def audit_a() -> None:
    predecessor_a()
    invariants(sources())
    print("PASS independent SMTP A: canonical sources and strict local process boundary")

def audit_b() -> None:
    original = sources()
    invariants(original)
    rejected = 0
    for kind, markers in GUARDS.items():
        for marker in markers:
            mutant = dict(original)
            mutant[kind] = mutant[kind].replace(marker, "[REMOVED_GUARD]")
            need(mutant[kind] != original[kind], "Ineffective mutation: "+marker)
            try:
                invariants(mutant)
            except AssertionError:
                rejected += 1
            else:
                raise AssertionError("Accepted prohibited mutation: "+marker)
    need(rejected >= 60, "Insufficient independent SMTP negative mutation guards")
    print(f"PASS independent SMTP B: {rejected} negative guard mutations rejected")
    print("LIMIT: real socket/SQL assertions are separately executed, not proved by mutation")

def git(*arguments: str) -> str:
    return subprocess.check_output(
        ["git", *arguments], cwd=ROOT, text=True,
        stderr=subprocess.DEVNULL).strip()

def audit_c() -> None:
    need(os.environ.get("GITHUB_ACTIONS") == "true" and
         os.environ.get("GITHUB_REPOSITORY") == REPO,
         "Only a real CRECISOL CI run can attest SHA")
    sha, run = os.environ.get("GITHUB_SHA",""),os.environ.get("GITHUB_RUN_ID","")
    need(re.fullmatch(r"[a-f0-9]{40}",sha) is not None and
         re.fullmatch(r"\d+",run) is not None and
         git("rev-parse","HEAD")==sha,
         "Exact same-run SHA required")
    for stage in ("A","B","dotnet","mysql"):
        p = EVIDENCE / stage
        need(p.is_file() and p.read_text(encoding="utf-8").strip() ==
             f"PASS {stage} sha={sha} run={run}",
             "Stage absent or stale: "+stage)
    event = os.environ.get("GITHUB_EVENT_NAME")
    if event == "pull_request":
        path = Path(os.environ.get("GITHUB_EVENT_PATH",""))
        need(path.is_file(), "PR event payload missing")
        pr=json.loads(path.read_text(encoding="utf-8"))["pull_request"]
        base,head=pr["base"],pr["head"]
        need(base["ref"]=="dev" and head["ref"]==FEATURE and
             base["repo"]["full_name"]==REPO and
             head["repo"]["full_name"]==REPO,
             "Unauthorized feature branch, fork or base")
        need(git("rev-list","--parents","-n","1","HEAD").split() ==
             [sha,base["sha"],head["sha"]],
             "CI synthetic merge does not match PR parents")
        changed=git("diff","--name-only",base["sha"],head["sha"]).splitlines()
    else:
        need(event=="push" and os.environ.get("GITHUB_REF")=="refs/heads/dev",
             "Only dev POSTMERGE can pass")
        changed=git("show","--format=","--name-only",sha).splitlines()
    need(bool(changed) and set(changed).issubset(ALLOWED) and
         FILES["sender"] in changed and FILES["receiver"] in changed and
         FILES["test"] in changed,
         "Out-of-scope or incomplete PR: "+repr(changed))
    invariants(sources())
    print(f"PASS independent SMTP C: A/B/.NET10/MySQL8.4 same SHA={sha} run={run}")
    print("NOT CERTIFIED: external inbox, identity, invited actor or membership/session")

if __name__ == "__main__":
    try:
        need(len(sys.argv)==2 and sys.argv[1] in ("A","B","C"),
             "Usage: c05_02_independent_smtp_audits.py A|B|C")
        {"A":audit_a,"B":audit_b,"C":audit_c}[sys.argv[1]]()
    except (AssertionError,OSError,ValueError,KeyError,
            subprocess.SubprocessError) as exc:
        print("FAIL independent SMTP audit: "+str(exc),file=sys.stderr)
        sys.exit(1)
