#!/usr/bin/env python3
"""C02-02 real contract tests: canonical coverage and three scoped A/B/C audits.

Source = pinned CRECISOL Word (via C02-01 extractor).
Security assertions here validate a DESIGN contract, not live authentication.
Read-only, Python stdlib only. No network, production, secrets, or database writes.
"""
from collections import Counter
from copy import deepcopy
from pathlib import Path
import json
import re
import sys

import c02_01_source

ROOT = Path(__file__).resolve().parents[1]
SPEC = ROOT / "docs/c02/C02_02_ESPECIFICACION.md"
DATA = ROOT / "docs/c02/C02_02_MAPA_NECESIDADES.json"
PLAN = ROOT / "docs/C02_PREPARACION.md"
ACTA = ROOT / "docs/c02/C02_02_ACTA_MICROFASE.md"
ALL_SOURCE_REFS = ({f"P{i:02d}" for i in range(1, 12)} |
                   {f"T01-R{i:02d}" for i in range(1, 7)})
EXPECTED_SHA = c02_01_source.EXPECTED_SHA
EXPECTED_SCOPE = {
    "E01": "platform-public",
    "E02": "platform-onboarding",
    "E03": "platform-global",
    "E04": "tenant-owner",
    "E05": "tenant-branch-assignment",
    "E06": "tenant-public-and-own-private",
}
EXPECTED_FLOW = [f"P{i:02d}" for i in range(3, 10)]


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def read_data():
    data = json.loads(DATA.read_text(encoding="utf-8"))
    require(isinstance(data, dict), "Contract must be a JSON object")
    return data


def need_ids(data):
    return [n["id"] for e in data["experiences"] for n in e["needs"]]


def valid_contract(data):
    require(data.get("artifact") == "CRECISOL-C02-02-MAPA-NECESIDADES",
            "Wrong contract artifact")
    require(data.get("schema_version") == 1, "Schema version changed without review")
    require(data.get("delivery_state") == "design-only-not-implemented",
            "C02-02 cannot assert application implementation")
    require(data.get("source", {}).get("sha256") == EXPECTED_SHA,
            "Word SHA is not pinned to approved master")
    require(data.get("source", {}).get("process_gate") == "MAE-C02-02",
            "Normative process namespace confused with operative microphase")
    require(data.get("source", {}).get("inventory") ==
            "docs/c02/C02_01_ESPECIFICACION.md", "Prior C02-01 inventory missing")
    constraints = data.get("constraints", {})
    require(constraints.get("login") == "automatic-verified-context-no-tenant-picker",
            "Tenant selector permitted during normal login")
    require(constraints.get("authorization") == "backend-deny-by-default",
            "Frontend or tenant supplied input incorrectly grants access")
    require(constraints.get("demo") == "synthetic-only-no-real-data",
            "Demo must not use production tenant/customer data")
    require(constraints.get("privacy") == "tenant-branch-resource-scope",
            "Tenant/branch/resource security scope missing")
    require(constraints.get("billing") == "no-real-charges",
            "No real charges in a design-only microphase")
    require(constraints.get("ui") == "WCAG-2.2-AA-keyboard-screen-reader-reduced-motion",
            "UX accessibility minimum changed")
    experiences = data.get("experiences")
    require(isinstance(experiences, list) and len(experiences) == 6,
            "Exactly six canonical experiences required")
    require([e.get("id") for e in experiences] == list(EXPECTED_SCOPE),
            "Canonical experience IDs/order changed")
    ids = []
    for exp in experiences:
        require(exp.get("scope") == EXPECTED_SCOPE[exp["id"]],
                f"Experience {exp['id']} changed scope")
        for field in ("label", "actor", "access_boundary"):
            require(isinstance(exp.get(field), str) and len(exp[field]) >= 15,
                    f"Experience {exp['id']} has no usable {field}")
        needs = exp.get("needs")
        require(isinstance(needs, list) and len(needs) == 3,
                f"Experience {exp['id']} must have three observable needs")
        for need in needs:
            ids.append(need.get("id"))
            require(need.get("delivery_state") == "design-only",
                    f"Need {need.get('id')} falsely claims implementation")
            for field in ("need", "task", "negative_case"):
                require(isinstance(need.get(field), str) and len(need[field].strip()) >= 22,
                        f"Need {need.get('id')} has no observable {field}")
            acceptance = need.get("acceptance")
            require(isinstance(acceptance, dict), "Missing acceptance criteria")
            for field in ("given", "when", "then"):
                require(isinstance(acceptance.get(field), str) and
                        len(acceptance[field].strip()) >= 18,
                        f"Need {need.get('id')} missing {field} acceptance")
            refs = need.get("source_refs")
            require(isinstance(refs, list) and refs and
                    len(set(refs)) == len(refs) and
                    set(refs).issubset(ALL_SOURCE_REFS),
                    f"Need {need.get('id')} has invalid/duplicate canonical refs")
            depends = need.get("dependencies")
            require(isinstance(depends, list) and depends and
                    all(re.fullmatch(r"C\d{2}(?:-\d{2})?", v) for v in depends),
                    f"Need {need.get('id')} has an untraceable dependency")
            require(need.get("risk") in ("P0", "P1", "P2"),
                    "Missing risk prioritization")
    require(ids == [f"N{i:02d}" for i in range(1, 19)],
            "Need IDs must be 18, unique, ordered, from N01 to N18")
    # Source role coverage: console/platform/business/employee/consumer plus synthetic demo.
    for ix, exp in enumerate(experiences):
        required = f"T01-R{ix:02d}" if ix >= 2 else "T01-R01"
        if ix == 2: required = "T01-R02"
        if ix == 3: required = "T01-R03"
        if ix == 4: required = "T01-R04"
        if ix == 5: required = "T01-R05"
        refs = [ref for need in exp["needs"] for ref in need["source_refs"]]
        require(required in refs, f"Role coverage missing for {exp['id']}")
    demo = next(n for e in experiences for n in e["needs"] if n["id"] == "N03")
    require("T01-R06" in demo["source_refs"], "Demo must reference synthetic row")
    require(all(n["risk"] == "P0" for e in experiences[2:]
                for n in e["needs"]),
            "Private/privileged journeys need P0 security gating in design")
    flow = data.get("omnichannel_flow")
    require(isinstance(flow, list) and len(flow) == 7, "Seven source flow steps required")
    require([f.get("id") for f in flow] == [f"F{i:02d}" for i in range(1, 8)],
            "Omnichannel flow steps missing or out of order")
    require([f.get("source_ref") for f in flow] == EXPECTED_FLOW,
            "Omnichannel source paragraphs P03..P09 no longer in order")
    for step in flow:
        require(step.get("experience") in EXPECTED_SCOPE,
                "Flow step refers to unknown experience")
        require(all(isinstance(step.get(k), str) and len(step[k]) >= 18
                    for k in ("planned_step", "safety_gate")), "Flow step not observable/safe")
        require(step.get("deferred_to") and
                all(re.fullmatch(r"C\d{2}", d) for d in step["deferred_to"]),
                "Flow step missing future implementing chapter")
    require(isinstance(data.get("cross_cutting"), list) and
            len(data["cross_cutting"]) >= 5, "Cross-cutting controls missing")
    require(isinstance(data.get("non_goals"), list) and len(data["non_goals"]) >= 5,
            "Explicit non-goals missing")
    return ids


def mutation_must_fail(original, mutator, label):
    other = deepcopy(original)
    mutator(other)
    try:
        valid_contract(other)
    except (AssertionError, KeyError, TypeError, ValueError):
        return
    raise AssertionError(f"Negative mutation accepted: {label}")


def audit_a():
    original, rows, _, _ = c02_01_source.original_parts()
    require(len(original) == 11 and len(rows) == 7,
            "Canonical Word chapter 02 source counts changed")
    data = read_data()
    ids = valid_contract(data)
    doc = SPEC.read_text(encoding="utf-8")
    for marker in ("C02-02", "MAE-C02-02", EXPECTED_SHA,
                   "11 párrafos", "seis", "18 necesidades", "7 pasos",
                   "C02 NO CERTIFICADO"):
        require(marker.casefold() in doc.casefold(),
                f"Specification lacks source/contract marker: {marker}")
    for id_ in ids + [f"E{i:02d}" for i in range(1, 7)] + [
                    f"F{i:02d}" for i in range(1, 8)]:
        require(re.search(r"\b" + re.escape(id_) + r"\b", doc),
                f"Missing observable journey/flow in specification: {id_}")
    for i, row in enumerate(rows[1:], 1):
        require(row[0] in c02_01_source.SPEC.read_text(encoding="utf-8"),
                f"C02-01 source person not traced: T01-R{i:02d}")
    mutation_must_fail(data, lambda d: d["experiences"][5]["needs"].pop(),
                       "missing N18")
    mutation_must_fail(data, lambda d: d["experiences"][1]["needs"][0].update(id="N01"),
                       "duplicate N01")
    mutation_must_fail(data, lambda d: d["experiences"][0]["needs"][0].update(source_refs=["P99"]),
                       "invented source requirement")
    mutation_must_fail(data, lambda d: d["omnichannel_flow"][2].update(source_ref="P09"),
                       "wrong omnichannel source order")
    print("AUDIT A PASS C02-02: exact Word C02 SHA/11 paragraphs/6 roles,")
    print("  6 experiences, 18 traceable needs and 7 ordered source flow steps;")
    print("  4 rejection mutations (missing, duplicate, forged source, wrong order).")


def audit_b():
    c02_01_source.original_parts()
    data = read_data()
    valid_contract(data)
    doc = SPEC.read_text(encoding="utf-8")
    for marker in ("backend", "tenant", "sucursal", "propiedad",
                   "MFA", "motivo", "caducidad", "PII", "demo",
                   "sintética", "no hay selector", "cobros reales",
                   "NO EJECUTADAS", "shared/dedicated"):
        require(marker.casefold() in doc.casefold(),
                f"Security boundary not described: {marker}")
    mutation_must_fail(data, lambda d: d["constraints"].update(
        login="choose-tenant-from-login-picker"), "client-chosen login tenant")
    mutation_must_fail(data, lambda d: d["constraints"].update(
        authorization="frontend-guard-only"), "frontend grants authorization")
    mutation_must_fail(data, lambda d: d["constraints"].update(
        demo="uses-live-customer-data"), "real data in demo")
    mutation_must_fail(data, lambda d: d["experiences"][3]["needs"][0].update(
        negative_case=""), "missing cross-tenant negative")
    mutation_must_fail(data, lambda d: d["experiences"][2]["needs"][2].update(
        risk="P2"), "privileged PII risk downgraded")
    for file in (SPEC, DATA):
        require(not re.search(r"(?i)\b(?:password|pwd|api[_-]?key|secret)\s*[:=]\s*\S+",
                              file.read_text(encoding="utf-8")),
                f"Secret-like assignment in {file.name}")
    print("AUDIT B PASS C02-02: documented tenant/access/PII/demo/consent boundaries;")
    print("  5 adversarial design mutations rejected; no credential-like assignment.")
    print("LIMIT: no private API, MFA, BOLA/IDOR or live cross-tenant tests were executed.")


def audit_c():
    data = read_data()
    valid_contract(data)
    doc = SPEC.read_text(encoding="utf-8")
    plan = PLAN.read_text(encoding="utf-8")
    require("1/37" in plan and "C03 bloqueado" in plan and
            "NO CERTIFICADO" in plan, "Chapter certification/sequence gate changed")
    if ACTA.is_file():
        acta = ACTA.read_text(encoding="utf-8")
        require("2/9" in plan and "C02-02" in plan and "CERRADA TÉCNICAMENTE" in plan,
                "Closed C02-02 needs consistent 2/9 status")
        require(all(m in acta for m in (
            "C02-02 CERRADA TÉCNICAMENTE", "POSTMERGE", "NO CERTIFICADO",
            "C02-03", "PR #32", "455987893827cb5b25d4b127457efd81225267b7",
            "42619923854", "42619923974", "42619923990")),
                "Acta missing exact technical merge SHA or postmerge CI evidence")
    else:
        require("1/9" in plan, "C02-02 must not count as closed without an acta")
    for key in ("WCAG 2.2 AA", "prefers-reduced-motion", "teclado",
                "Angular 22", ".NET 10", "C02-03", "C02-04",
                "C02-05", "C02-06", "C02-08", "rollback",
                "frontend/", "backend/", "database/"):
        require(key.casefold() in doc.casefold() or key.casefold() in plan.casefold(),
                f"Architecture/UX/future-phase requirement missing: {key}")
    for file in (SPEC, PLAN):
        for target in re.findall(r"\[[^\]]+\]\(([^)]+)\)",
                                 file.read_text(encoding="utf-8")):
            if target.startswith(("https://", "http://", "mailto:", "#")):
                continue
            destination = (file.parent / target.split("#", 1)[0]).resolve()
            require(destination.is_relative_to(ROOT.resolve()) and destination.is_file(),
                    f"Broken local link or traversal: {target}")
    require("design-only-not-implemented" == data["delivery_state"],
            "Data contract cannot declare shipping")
    mutation_must_fail(data, lambda d: d["experiences"].pop(),
                       "lost canonical experience")
    mutation_must_fail(data, lambda d: d["experiences"][0]["needs"][0].update(
        acceptance={"given":"", "when":"", "then":""}),
        "unobservable acceptance")
    print("AUDIT C PASS C02-02: JSON schema, dependency/UX/a11y mapping,")
    print("  no implemented endpoints, 6x3 need grid, 7 source flow steps,")
    print("  sequenced chapter gate, Markdown links and 2 negative mutations.")
    print("LIMIT: design-only; builds are baseline regression, not UI feature tests.")


def main():
    require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
            "Usage: python3 qa/c02_02_audits.py A|B|C")
    {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, KeyError, TypeError, ValueError, OSError) as err:
        print(f"FAIL C02-02: {err}", file=sys.stderr)
        sys.exit(1)
