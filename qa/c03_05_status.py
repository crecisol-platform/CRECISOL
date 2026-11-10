"""C03-05 status machine: historic technical 5/10 or verified final 5/10.

No shortcut to C03-06 or C04. Historical evidence may remain verbatim in actas,
but only the CURRENT plan heading and CURRENT C03 row define active status.
"""
FIFTH_STATUS = (
    "**C03-01, C03-02, C03-03 Y C03-04 CERTIFICADAS (4/10); "
    "C03-05 CERRADA TÉCNICAMENTE Y ACEPTADA PARA CERTIFICACIÓN (5/10)**"
)
CERTIFIED_STATUS = "**C03-01 A C03-05 CERTIFICADAS (5/10)**"
CERTIFIED_MERGE = "12fb19b0c35038c297939e17b27770c4fd63f77e"
PROOF_DOC = "C03_05_CERTIFICACION_POSTMERGE.md"


def fifth_certified(c03_row):
    return CERTIFIED_STATUS in c03_row


def fifth_active(c03_row):
    return fifth_certified(c03_row) or FIFTH_STATUS in c03_row


def fifth_status_marker(plan):
    """Return exact marker for documentary mutation tests, no substr shortcuts."""
    if CERTIFIED_STATUS in plan:
        return CERTIFIED_STATUS
    if FIFTH_STATUS in plan:
        return FIFTH_STATUS
    raise AssertionError("No recognized fifth-phase provenance in plan")


def fifth_count_before(plan):
    return (CERTIFIED_STATUS if CERTIFIED_STATUS in plan
            else "CERTIFICACIÓN (5/10)**")


def fifth_count_after(plan):
    return ("**C03-01 A C03-06 CERTIFICADAS (6/10)**"
            if CERTIFIED_STATUS in plan else "CERTIFICACIÓN (6/10)**")


def fifth_safe(c03_row):
    certified = fifth_certified(c03_row)
    historical = FIFTH_STATUS in c03_row
    if not certified and not historical:
        return False
    if certified and historical:
        raise AssertionError("Contradictory active fifth-phase statuses")
    required = (
        "C03-06 NO INICIADA", "C03 NO CERTIFICADO", "C04 BLOQUEADO",
        "2/37 capítulos certificados (C01 y C02)",
    )
    if not all(token in c03_row for token in required):
        raise AssertionError("C03-05 status lost downstream chapter locks")
    if certified:
        for item in ("PR #58", "11/11 POSTMERGE SUCCESS", CERTIFIED_MERGE,
                     PROOF_DOC, "C03-05 CERTIFICADA (5/10)"):
            if item not in c03_row:
                raise AssertionError("Final C03-05 status lacks verified provenance: " + item)
    if "6/10" in c03_row or "C03 CERTIFICADO" in c03_row:
        raise AssertionError("C03 chapter/sixth phase unlawfully promoted")
    return True


def fifth_plan_safe(plan_text):
    """Fail closed if active header and chapter row diverge."""
    rows = plan_text.splitlines()
    c03 = next((row for row in rows if row.startswith("C03 | ")), "")
    if not fifth_safe(c03):
        return False
    active = next((row for row in rows[:7] if "**ESTADO VIGENTE:" in row), "")
    mandatory = (
        "2/37 capítulos certificados (C01 y C02)", "C03-06 NO INICIADA",
        "C03 NO CERTIFICADO", "C04 BLOQUEADO",
    )
    if not all(item in active for item in mandatory):
        raise AssertionError("C03-05 heading diverges from locked chapter row")
    if fifth_certified(c03):
        for item in ("C03-05 CERTIFICADA (5/10)", "11/11 CI POSTMERGE SUCCESS",
                     CERTIFIED_MERGE):
            if item not in active:
                raise AssertionError("Active heading lacks certified fifth-stage proof: " + item)
    elif "C03-05 CERRADA TÉCNICAMENTE" not in active:
        raise AssertionError("Technical fifth-stage heading is missing")
    if "6/10" in active or "C03 CERTIFICADO" in active:
        raise AssertionError("C03-06/C03 chapter unlawfully advanced in heading")
    return True
