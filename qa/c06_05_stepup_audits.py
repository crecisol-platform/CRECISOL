#!/usr/bin/env python3
"""C06-05: independent candidate audit, not an acta or cryptographic test."""
from pathlib import Path
import json
import os
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
BASE = "9bcd3fca482ef49df12b602e3641b1fc08a1994f"
DOMAIN = "backend/src/Crecisol.Domain/Authorization/C06SensitiveOperationPolicy.cs"
AUTH = "backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Authorized.cs"
PLATFORM = "backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Platform.cs"
PATHS = [
  "qa/c06_02_role_model_audit.py",
  "backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Authorized.cs",
  "backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Platform.cs",
  "backend/src/Crecisol.Domain/Authorization/C06SensitiveOperationPolicy.cs",
  "backend/tests/Crecisol.C06StepUp.ContractsTests/Program.cs",
  "backend/tests/Crecisol.C06StepUp.ContractsTests/Crecisol.C06StepUp.ContractsTests.csproj",
  "docs/c06/C06_05_ESPECIFICACION.md",
  "qa/c06_05_stepup_audits.py",
  "qa/c05_operational_flow_e2e.mjs",
  ".github/workflows/c06-05-sensitive-step-up.yml",
  "qa/c06_03_context_audits.py",
  "qa/c06_04_abac_audits.py"
]
TENANT = set(["passkeys/register-options","passkeys/revoke","invitations/issue","sessions/revoke-all","pairings/approve","factor/replace","password/change","mailbox/request","mailbox/complete","memberships/decide"])
PLATFORM_ACTIONS = set(["platform/sessions/revoke-all","platform/cases/find","platform/password/change","platform/factor/replace","platform/mailbox/request","platform/mailbox/complete","platform/review","platform/release-share","platform/session-policy/update"])
REASONS = set(["memberships/decide","platform/cases/find","platform/review"])

def need(condition, message):
    if not condition:
        raise AssertionError("C06-05 NO-GO: " + message)

def read(path):
    return (ROOT / path).read_text(encoding="utf-8")

def scope():
    event = os.environ.get("GITHUB_EVENT_NAME")
    if event == "pull_request":
        need(os.environ.get("GITHUB_BASE_REF") == "dev", "PR outside dev")
        base = ["origin/dev...HEAD"]
    elif event == "push":
        need(os.environ.get("GITHUB_REF") == "refs/heads/dev", "push outside dev")
        base = ["HEAD^", "HEAD"]
    else:
        base = [BASE, "HEAD"]
    changed = subprocess.check_output(["git", "diff", "--name-only", *base], cwd=ROOT, text=True).splitlines()
    # Explicit sequential successor: preserve every other historical audit.
    if "docs/c06/C06_09_ESPECIFICACION.md" in changed:
        from c06_09_delivery_scope import require_exact
        require_exact(changed)
        return changed
    if "docs/c06/C06_08_ESPECIFICACION.md" in changed:
        from c06_08_query_scope import require_exact
        require_exact(changed)
        return changed
    if "docs/c06/C06_07_ESPECIFICACION.md" in changed:
        from c06_07_privacy_scope import require_exact
        require_exact(changed)
        return changed
    if "docs/c06/C06_06_ESPECIFICACION.md" in changed:
        from c06_06_exceptional_scope import require_exact
        require_exact(changed)
        return changed
    need(set(changed) == set(PATHS), "unexpected or missing files: " + repr(changed))
    need(not any(p.startswith(("frontend/", "database/", "backend/src/Crecisol.Api/", "backend/src/Crecisol.CredentialSecurity/")) for p in changed), "unexpected UI, DB, API or crypto change")

def catalog():
    d = json.loads(read("docs/c06/C06_01_CATALOGO_ACCIONES.json"))
    need(len(d["api_actions"]) == 80 and len(d["normative_actions"]) == 28, "catalog drift")
    need(all(a["grantable"] is False for a in d["normative_actions"]), "future grants enabled")
    operations = {a["operation_code"]: a for a in d["api_actions"]}
    for op in TENANT | PLATFORM_ACTIONS:
        row = operations.get(op)
        need(row and row["realm"] == ("tenant" if op in TENANT else "platform") and "code" in row["required_fields"], "not an existing factor route: " + op)
        need(("reason" in row["required_fields"]) == (op in REASONS), "purpose contract mismatch: " + op)
    domain = read(DOMAIN)
    left = domain.split("C06StepUpRealm.Tenant => operation is", 1)[1].split("C06StepUpRealm.Platform => operation is", 1)[0]
    right = domain.split("C06StepUpRealm.Platform => operation is", 1)[1].split("_ => false", 1)[0]
    need(set(re.findall(r'"([^"]+)"', left)) == TENANT, "tenant closed list drift")
    need(set(re.findall(r'"([^"]+)"', right)) == PLATFORM_ACTIONS, "platform closed list drift")

def security():
    auth, platform, domain = read(AUTH), read(PLATFORM), read(DOMAIN)
    for term in ("ResolveC06TenantContextFromLockedRows(", "C06StepUpInputValid(C06StepUpRealm.Tenant, r)",
                 "factor_kind=1 AND factor_state=1 AND revoked_at_utc IS NULL FOR UPDATE",
                 'await Reserve(tenant, "stepup:" + Hex(actor), 8, ct)',
                 'await AcceptTotp(u, actor, factor, Field(r, "code"))',
                 "C06ReadAuthorizedCase(u, tenant, actor, member, c06Context, r)",
                 'permission_code=\'MEMBERSHIP_APPROVE\'', "u.Actor is not { } actor",
                 'actor.SequenceEqual(Bytes(principal, "operator_id"))',
                 'Digest("c06-step-up-purpose", request.Operation'):
        need(term in auth, "tenant/actor/purpose guard missing: " + term)
    need(auth.index("ResolveC06TenantContextFromLockedRows(") < auth.index("C06StepUpInputValid(C06StepUpRealm.Tenant, r)") < auth.index('await AcceptTotp(u, actor, factor, Field(r, "code"))'), "tenant proof guard order")
    need(platform.index("await SlidePlatformSession(u, tokenHash, idle)") <
         platform.index("C06PlatformStepUpPurpose(u, principal, r)") <
         platform.index('if (r.Operation == "platform/sessions/keepalive")'), "platform dispatch precedes purpose")
    for term in ("code.All(char.IsAsciiDigit)", "code.All(Uri.IsHexDigit)", "reason.Any(char.IsLetterOrDigit)", "!reason.Any(char.IsControl)", "_ => false"):
        need(term in domain, "input invariant missing: " + term)
    for term in ("consumed_at_utc IS NULL FOR UPDATE", "last_accepted_step=@s", "crypto.VerifyTotp"):
        need(term in read("backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.PasswordMfa.cs"), "C05 proof weakened: " + term)
    need("await PlatformTotp(u, principal, Field(r, \"code\"))" in platform, "operator crypto absent")
    need("C06-05 consumed proof cannot be reused" in read("qa/c05_operational_flow_e2e.mjs"), "real replay scenario absent")

def architecture():
    domain = read(DOMAIN)
    need(not any(s in domain for s in ("MySqlConnector", "HttpContext", "Microsoft.AspNetCore", "Crecisol.Infrastructure")), "Domain crosses layers")
    need("NO CERTIFICADA" in read("docs/c06/C06_05_ESPECIFICACION.md"), "premature status")
    need("contents: read" in read(".github/workflows/c06-05-sensitive-step-up.yml"), "CI token not read-only")
    scope()

if __name__ == "__main__":
    need(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"), "usage A|B|C")
    stage = sys.argv[1]
    if stage == "A":
        scope()
        catalog()
    elif stage == "B":
        catalog()
        security()
    else:
        architecture()
        security()
    print(f"PASS C06-05 {stage}: {len(TENANT)} tenant and {len(PLATFORM_ACTIONS)} platform routes; static audit only")
