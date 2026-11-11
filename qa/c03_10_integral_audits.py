#!/usr/bin/env python3
"""C03-10 independent A/B/C chapter gate. Never fabricates runtime evidence."""
from pathlib import Path
import os
import re
import subprocess
import sys

from master_source_guard import canonical_active_word, CURRENT_WORD_SHA, CURRENT_PDF_SHA
import c03_01_source as deps

ROOT = Path(__file__).resolve().parents[1]
PROOF = ROOT / ".c03-10-ci-proof"
STEPS = ("dotnet", "angular", "chromium", "mysql", "regressions")


def need(condition, message):
    if not condition:
        raise AssertionError(message)


def read(path):
    location = ROOT / path
    need(location.is_file(), "Missing required source: " + path)
    return location.read_text(encoding="utf-8")


def audit_a():
    need(canonical_active_word(), "Master Word/PDF verification failed")
    reference = read("docs/MASTER_REFERENCE.md")
    need(CURRENT_WORD_SHA in reference and CURRENT_PDF_SHA in reference,
         "Current canonical Word/PDF pins missing")
    prior = read("docs/c03/C03_09_CERTIFICACION_AUTONOMA.md")
    need("C03-09 CERTIFICADA EN SU ALCANCE" in prior,
         "C03-09 predecessor not formally certified")
    spec = read("docs/c03/C03_10_ESPECIFICACION.md")
    need(all(token in spec for token in
             ("C03-10", "NO CERTIFICADA", "C04", "A —", "B —", "C —",
              CURRENT_WORD_SHA, "Rollback" if "Rollback" in spec else "Revertir")),
         "C03-10 specification or rollback/limits missing")
    plan = read("docs/PLAN_37_CAPITULOS.md")
    future = read("docs/planificacion-futura/C03_MICROFASES_PROPUESTAS.md")
    need("| C03-10 | Auditorías integrales" in future,
         "C03-10 microphase definition changed")
    row = next((s for s in plan.splitlines() if s.startswith("C03 | ")), "")
    c04 = next((s for s in plan.splitlines() if s.startswith("C04 | ")), "")
    need(row and "C03 NO CERTIFICADO" in row and
         c04 == "C04 | Multitenancy, dominios y aprovisionamiento | PENDIENTE",
         "Premature chapter certification or C04 promotion")
    for number in range(1, 10):
        need((ROOT / f"docs/c03/C03_{number:02}_ESPECIFICACION.md").is_file(),
             "Missing sequential C03 stage: " + str(number))
    deps.actual_graph()
    print("PASS C03-10 A: exact Word/PDF, 9 predecessors, scope, graph and C04 lock")


def invariant(api, outbox, workflow):
    need(api.count('app.MapGet(') == 1 and 'app.MapPost(' not in api
         and 'AddHostedService' not in api and 'UseForwardedHeaders' not in api,
         "Unexpected private endpoint, worker or forwarded-host bypass")
    need("--network none" in outbox and "mysql:8.4@sha256:" in outbox,
         "MySQL test container loses network isolation or image digest")
    need("permissions:\n  contents: read" in workflow
         and "pull_request_target:" not in workflow
         and "contents: write" not in workflow
         and "deploy" not in workflow.lower(),
         "Workflow permissions, trigger or deployment boundary weakened")


def audit_b():
    api = read("backend/src/Crecisol.Api/Program.cs")
    outbox = read("database/tests/c03_07_outbox_mysql_smoke.sh")
    workflow = read(".github/workflows/c03-10-integral.yml")
    invariant(api, outbox, workflow)
    mutations = (
        ("API POST", api + '\napp.MapPost("bad", () => 1);', outbox, workflow),
        ("hosted worker", api + "\nAddHostedService", outbox, workflow),
        ("remove public route", api.replace('app.MapGet(', 'app.MapMissing(', 1), outbox, workflow),
        ("enable DB network", api, outbox.replace("--network none", "--network host", 1), workflow),
        ("unpinned MySQL", api, outbox.replace("mysql:8.4@sha256:", "mysql:8.4@", 1), workflow),
        ("write token", api, outbox, workflow.replace("contents: read", "contents: write", 1)),
        ("unsafe PR trigger", api, outbox, workflow + "\npull_request_target:"),
        ("workflow deployment", api, outbox, workflow + "\ndeploy production"),
    )
    for label, a, b, c in mutations:
        try:
            invariant(a, b, c)
        except AssertionError:
            continue
        raise AssertionError("Unsafe mutation escaped: " + label)
    ignored = read(".gitignore")
    need(".env" in ignored and "*.pem" in ignored and "*.key" in ignored,
         "Secret ignore baseline incomplete")
    print(f"PASS C03-10 B: scope, secrets, permissions and {len(mutations)} adversarial mutations rejected")


def version(command):
    done = subprocess.run(command, capture_output=True, text=True,
                          check=False, timeout=15)
    need(done.returncode == 0, "Tool version command failed: " + command[0])
    return done.stdout.strip()


def audit_c():
    need(os.getenv("GITHUB_ACTIONS") == "true",
         "C audit requires actual GitHub Actions runner")
    sha, run = os.getenv("GITHUB_SHA", ""), os.getenv("GITHUB_RUN_ID", "")
    need(re.fullmatch("[0-9a-f]{40}", sha) is not None and
         re.fullmatch("[0-9]+", run) is not None,
         "No verified Actions commit and run identity")
    need(re.match(r"^v24\.\d+\.\d+$", version(["node", "--version"])) is not None,
         "C03 runtime did not execute Node 24")
    need(re.search(r"^10\.0\.\d+\s+\[", version(["dotnet", "--list-sdks"]), re.M) is not None,
         "C03 runtime did not execute .NET 10")
    for step in STEPS:
        path = PROOF / step
        need(path.is_file() and path.read_text(encoding="utf-8").strip()
             == f"PASS {step} sha={sha} run={run}",
             "Missing same-run executed evidence: " + step)
    deps.actual_graph()
    print("PASS C03-10 C: real same-run .NET10, Node24/Angular22, Chromium, MySQL8.4, regression and graph")
    print("NOTE: C03-09 local benchmarks remain historical; no production p95 is claimed")


def main():
    need(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
         "Usage: c03_10_integral_audits.py A|B|C")
    {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, OSError, ValueError, KeyError, subprocess.TimeoutExpired) as error:
        print("FAIL C03-10:", error, file=sys.stderr)
        sys.exit(1)
