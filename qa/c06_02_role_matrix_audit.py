#!/usr/bin/env python3
"""C06-02 role/action intent catalog A/B/C. Design-only: never authorizes HTTP."""
from copy import deepcopy
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = json.loads((ROOT / "docs/c06/C06_01_CATALOGO_ACCIONES.json").read_text(encoding="utf-8"))
FILE = ROOT / "docs/c06/C06_02_MATRIZ_ROLES_CAPACIDADES.json"
MATRIX = json.loads(FILE.read_text(encoding="utf-8"))

ROLE_CODES = {
    "platform-operator", "platform-security-operator", "platform-incident-responder",
    "tenant-owner", "tenant-admin", "tenant-branch-manager", "tenant-staff",
    "tenant-viewer", "tenant-finance-specialist", "tenant-custom",
}
HIGH_RISK = {
    "platform.release-share", "platform.session-policy.update",
    "platform.case", "platform.review", "authorization.role.assign",
    "authorization.role.delegate", "authorization.role.revoke", "case.close",
    "dispute.manage", "diagnosis.publish", "media.private.export",
    "finance.cash.manage", "finance.margin.read", "finance.refund.issue",
    "finance.report.export", "finance.revenue.read",
}
NON_ROLE = {
    "harness_non_product", "anonymous_public_projection",
    "verified_data_subject_not_role", "reserved_c06_06_exception",
    "proof_or_preauth_not_role", "own_account_or_session_not_role",
    "withheld_unassigned_sensitive",
}
SENSITIVE_SUBJECT = {
    "approval.decide", "privacy.subject.access", "privacy.subject.erase",
    "privacy.subject.object", "privacy.subject.rectify",
}
PLATFORM_EXCEPTION = {
    "platform.breakglass.open", "platform.breakglass.read",
    "platform.breakglass.close",
}
MEMBERSHIP_APPROVAL = {
    "identity.invitations.issue", "identity.memberships.pending",
    "identity.memberships.decide",
}

def need(value, message):
    if not value:
        raise AssertionError("C06-02 ROLE MATRIX NO-GO: " + message)

def validate(m):
    need(m.get("schema_version") == 1 and
         m.get("model") == "CRECISOL_C06_02_SCOPED_ROLE_INTENT_MATRIX" and
         m.get("authority") == "DESIGN_AND_TESTED_PROPOSALS_ONLY_NOT_EXECUTABLE_AUTHORIZATION",
         "unsafe authority/schema")
    src = m["source_catalog"]
    need(src["path"] == "docs/c06/C06_01_CATALOGO_ACCIONES.json" and
         src["git_blob_sha"] == "5b72dd4a35db1cdd53187a62637a5c3217afda50" and
         (src["http_actions"], src["future_actions"], src["non_http_contracts"]) == (80,28,12),
         "C06-01 catalog provenance drift")
    need(len(SOURCE["api_actions"]) == 80 and
         len(SOURCE["normative_actions"]) == 28 and
         len(SOURCE["non_http_contracts"]) == 12, "source inventory mismatch")
    guarantees = m["guarantees"]
    for field in ("default_deny","assignment_requires_server_verified_membership"):
        need(guarantees.get(field) is True, "missing default-deny guarantee " + field)
    for field in ("auto_grant","tenant_role_can_be_platform_operator",
                  "platform_role_can_be_tenant_member","branch_inheritance",
                  "default_finance_privilege","default_breakglass_privilege",
                  "ui_authorization_source"):
        need(guarantees.get(field) is False, "unsafe guarantee " + field)
    roles = m["roles"]
    need(len(roles) == 10, "ten role profiles required")
    index = {r["role_code"]: r for r in roles}
    need(len(index) == len(roles) and set(index) == ROLE_CODES,
         "missing, duplicate or unknown role")
    for code, role in index.items():
        need(re.fullmatch(r"[a-z][a-z0-9-]{2,63}", code) is not None,
             "role format or wildcard: " + code)
        need(role["realm"] == ("platform" if code.startswith("platform-") else "tenant"),
             "role realm confusion: " + code)
        need(role.get("scope") not in ("global", "all_tenants", "unscoped", ""),
             "unrestricted scope: " + code)
        need(role.get("default_grants") == [] and role.get("auto_assigned") is False and
             role.get("executable_in_c06_02") is False and
             role.get("cross_tenant_inheritance") is False and
             role.get("grant_ceiling") == "future_server_policy_required",
             "silent default grant or inherited privilege: " + code)
        need(role.get("display_name") and role.get("purpose"), "unexplained role")
    for code in ("tenant-branch-manager", "tenant-staff","tenant-viewer","tenant-custom"):
        need(index[code]["scope"] == "explicit_branch_only",
             "branch role must be explicitly branch scoped: " + code)
    need(index["tenant-finance-specialist"]["scope"] ==
         "explicit_tenant_branch_and_sensitive_approval",
         "finance role lacks independent scope and approval")
    need(index["platform-incident-responder"]["scope"] == "platform_exception_only",
         "breakglass role is not exceptional")

    canonical = {a["action_id"]: (a,"existing_http",a["access_boundary"])
                 for a in SOURCE["api_actions"]}
    canonical.update({a["action_id"]: (a,"future_normative",a["minimum_boundary"])
                      for a in SOURCE["normative_actions"]})
    actions = m["actions"]
    by_id = {a["action_id"]: a for a in actions}
    need(len(actions) == len(canonical) == len(by_id) == 108 and
         set(by_id) == set(canonical), "missing, duplicate or invented action")
    for key, row in by_id.items():
        original, provenance, boundary = canonical[key]
        need((row.get("realm"), row.get("module"), row.get("source"), row.get("boundary")) ==
             (original["realm"], original["module"], provenance, boundary),
             "catalog action/realm/boundary drift: " + key)
        need(row.get("grantable") is False and
             row.get("policy_activated") is False and
             row.get("inherited_across_tenants") is False,
             "candidate became executable or inherited: " + key)
        need(row.get("implementation_state") ==
             ("future_not_http_registered" if provenance == "future_normative"
              else "c05_existing_gate_unmodified"),
             "future status or existing boundary changed: " + key)
        codes = row["candidate_role_codes"]
        need(len(codes) == len(set(codes)) and all(c in index for c in codes),
             "wildcard/duplicate/unknown role candidate: " + key)
        handling = row.get("handling")
        need((handling == "role_candidate") == bool(codes) and
             (handling == "role_candidate" or handling in NON_ROLE),
             "role classification invalid: " + key)
        need(all(index[c]["realm"] == row["realm"] for c in codes),
             "platform / tenant cross-realm privilege: " + key)
        checks = row["required_checks"]
        need(len(checks) == len(set(checks)) and checks,
             "missing/double security checks: " + key)
        if codes:
            essential = ("verified_platform_actor","explicit_platform_operator_membership")
            if row["realm"] == "tenant":
                essential = ("trusted_actor","verified_tenant","active_membership",
                             "explicit_branch_scope","resource_state_and_ownership")
            need(all(c in checks for c in essential) and
                 "server_policy_not_activated" in checks,
                 "backend boundary checks missing: " + key)
        if key in HIGH_RISK and codes:
            need(("mfa_step_up" in checks and "purpose_and_expiry" in checks
                  if row["realm"] == "platform"
                  else "step_up_or_segregated_approval" in checks),
                 "sensitive operation lacks step-up: " + key)

    for key in MEMBERSHIP_APPROVAL:
        need(set(by_id[key]["candidate_role_codes"]) ==
             {"tenant-owner","tenant-admin"}, "membership approval delegation too broad")
    need("tenant-branch-manager" in by_id["cases.read"]["candidate_role_codes"] and
         "tenant-branch-manager" in by_id["note.create"]["candidate_role_codes"] and
         "tenant-branch-manager" in by_id["case.close"]["candidate_role_codes"],
         "branch manager does not have explicit work intentions")
    need("tenant-viewer" not in by_id["case.close"]["candidate_role_codes"] and
         "tenant-staff" not in by_id["authorization.role.assign"]["candidate_role_codes"],
         "operational or viewer role escalated to authority")
    for key in SENSITIVE_SUBJECT:
        need(by_id[key]["handling"] == "verified_data_subject_not_role" and
             not by_id[key]["candidate_role_codes"],
             "subject right / approval incorrectly granted by company role")
    for key in PLATFORM_EXCEPTION:
        need(by_id[key]["handling"] == "reserved_c06_06_exception" and
             not by_id[key]["candidate_role_codes"],
             "breakglass auto-provisioned")
    for key in ("public.platform_brand.read","private.case.read.harness_only"):
        need(not by_id[key]["candidate_role_codes"] and
             by_id[key]["handling"] in ("anonymous_public_projection","harness_non_product"),
             "public/harness turned into a role action")
    for key in ("finance.cash.manage","finance.margin.read","finance.refund.issue",
                "finance.report.export","finance.revenue.read"):
        need(by_id[key]["candidate_role_codes"] == ["tenant-finance-specialist"],
             "sensitive finance inherited by owner, admin or other role")
    for key in ("platform.case","platform.review","platform.release-share",
                "platform.session-policy.update"):
        need(by_id[key]["candidate_role_codes"] == ["platform-security-operator"],
             "privileged platform operations leaked to operator")
    need("platform-operator" in by_id["platform.support.metadata.read"]["candidate_role_codes"],
         "platform operator lacks metadata intent")
    for a in actions:
        need("tenant-custom" not in a["candidate_role_codes"] and
             "platform-incident-responder" not in a["candidate_role_codes"],
             "custom or breakglass role incorrectly auto-eligible")
    non_http = m["internal_contracts"]
    expected = {x["name"] for x in SOURCE["non_http_contracts"]}
    need(len(non_http) == 12 and {x["name"] for x in non_http} == expected and
         all(x.get("role_assignable") is False and
             x.get("exposed_by_matrix") is False for x in non_http),
         "internal non-HTTP contract exposed")
    return {"roles":len(roles), "covered_actions":len(actions),
            "role_candidates":sum(bool(a["candidate_role_codes"]) for a in actions),
            "non_role_actions":sum(not a["candidate_role_codes"] for a in actions)}

def audit_b():
    # Each hostile edit must fail validation in a fresh complete copy.
    attacks = [
        ("auto grants",lambda m:m["roles"][0].update(default_grants=["platform.case"])),
        ("auto assignment",lambda m:m["roles"][0].update(auto_assigned=True)),
        ("platform realm tenant",lambda m:m["roles"][0].update(realm="tenant")),
        ("tenant global scope",lambda m:next(x for x in m["roles"] if x["role_code"]=="tenant-branch-manager").update(scope="global")),
        ("custom default privilege",lambda m:next(x for x in m["roles"] if x["role_code"]=="tenant-custom").update(default_grants=["finance.refund.issue"])),
        ("finance inherited by owner",lambda m:next(x for x in m["actions"] if x["action_id"]=="finance.refund.issue")["candidate_role_codes"].append("tenant-owner")),
        ("platform tenant private",lambda m:next(x for x in m["actions"] if x["action_id"]=="identity.private.read")["candidate_role_codes"].append("platform-operator")),
        ("branch private export",lambda m:next(x for x in m["actions"] if x["action_id"]=="media.private.export")["candidate_role_codes"].append("tenant-staff")),
        ("privileged platform owner",lambda m:next(x for x in m["actions"] if x["action_id"]=="platform.case")["candidate_role_codes"].append("tenant-owner")),
        ("public action role",lambda m:next(x for x in m["actions"] if x["action_id"]=="public.platform_brand.read")["candidate_role_codes"].append("platform-operator")),
        ("harness role",lambda m:next(x for x in m["actions"] if x["action_id"]=="private.case.read.harness_only")["candidate_role_codes"].append("tenant-owner")),
        ("subject approval role",lambda m:next(x for x in m["actions"] if x["action_id"]=="approval.decide")["candidate_role_codes"].append("tenant-admin")),
        ("privacy erase role",lambda m:next(x for x in m["actions"] if x["action_id"]=="privacy.subject.erase")["candidate_role_codes"].append("tenant-owner")),
        ("breakglass candidate",lambda m:next(x for x in m["actions"] if x["action_id"]=="platform.breakglass.open")["candidate_role_codes"].append("platform-incident-responder")),
        ("action executable flag",lambda m:next(x for x in m["actions"] if x["action_id"]=="cases.read").update(grantable=True)),
        ("policy activation",lambda m:next(x for x in m["actions"] if x["action_id"]=="cases.read").update(policy_activated=True)),
        ("role inheritance",lambda m:next(x for x in m["roles"] if x["role_code"]=="tenant-owner").update(cross_tenant_inheritance=True)),
        ("remove required scope",lambda m:next(x for x in m["actions"] if x["action_id"]=="cases.read")["required_checks"].remove("explicit_branch_scope")),
        ("skip step up",lambda m:next(x for x in m["actions"] if x["action_id"]=="finance.refund.issue")["required_checks"].remove("step_up_or_segregated_approval")),
        ("grant owner membership decision to staff",lambda m:next(x for x in m["actions"] if x["action_id"]=="identity.memberships.decide")["candidate_role_codes"].append("tenant-staff")),
        ("alter original realm",lambda m:next(x for x in m["actions"] if x["action_id"]=="cases.read").update(realm="platform")),
        ("wildcard custom role",lambda m:m["roles"].append({**m["roles"][0],"role_code":"*"})),
        ("missing action",lambda m:m["actions"].pop()),
        ("duplicate action",lambda m:m["actions"].append(deepcopy(m["actions"][0]))),
        ("invent action",lambda m:m["actions"][0].update(action_id="madeup.grant")),
        ("missing unexposed contract",lambda m:m["internal_contracts"].pop()),
        ("source SHA replaced",lambda m:m["source_catalog"].update(git_blob_sha="0"*40)),
        ("open branch inheritance",lambda m:m["guarantees"].update(branch_inheritance=True)),
        ("skip platform MFA",lambda m:next(x for x in m["actions"] if x["action_id"]=="platform.release-share")["required_checks"].remove("mfa_step_up")),
        ("role runtime activated",lambda m:m["roles"][0].update(executable_in_c06_02=True)),
        ("claim UI authority",lambda m:m["guarantees"].update(ui_authorization_source=True)),
    ]
    for description,change in attacks:
        data = deepcopy(MATRIX)
        change(data)
        try:
            validate(data)
        except (AssertionError, KeyError, TypeError, ValueError):
            pass
        else:
            raise AssertionError("C06-02 ROLE MATRIX NO-GO negative mutation accepted: " + description)
    need(len(attacks) >= 30, "insufficient negative matrix testing")
    print("PASS C06-02 ROLE B: "+str(len(attacks))+" hostile mutations rejected; no grant or cross-tenant inheritance")

def audit_c():
    sql=(ROOT/"database/migrations/019_c06_prepared_roles.sql").read_text(encoding="utf-8")
    domain=(ROOT/"backend/src/Crecisol.Domain/Authorization/C06PreparedRoleSnapshot.cs").read_text(encoding="utf-8")
    app=(ROOT/"backend/src/Crecisol.Application/Authorization/InspectPreparedTenantRoles.cs").read_text(encoding="utf-8")
    need("ck_c06_role_realm_separation" in sql and
         "NOT LIKE 'platform-%'" in sql and
         "grantable = FALSE" in sql and "proposal_only = TRUE" in sql and
         "FOREIGN KEY (tenant_id, branch_id)" in sql and
         "FOREIGN KEY (tenant_id, membership_id)" in sql,
         "SQL cannot enforce tenant role realm, grant or scope segregation")
    need("if (actionGrantable)" in domain and
         "AuthorizesPrivateResource => false;" in app,
         "prepared roles accidentally authorize resources")
    print("PASS C06-02 ROLE C: tenant-only SQL role codes, backend checks and no HTTP authority")

if __name__ == "__main__":
    stage = sys.argv[1] if len(sys.argv)==2 else ""
    stats=validate(MATRIX)
    if stage=="A":
        print("PASS C06-02 ROLE A: canonical C06-01 full coverage, role profiles and non-HTTP boundaries:",stats)
    elif stage=="B":
        audit_b()
    elif stage=="C":
        audit_c()
    else:
        raise SystemExit("expected A|B|C")
