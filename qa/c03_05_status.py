"""CRECISOL C03: append-only sequential state guards for stages five and six.

Historical acts remain immutable. Only the active plan heading and current C03
row can promote status. Sixth-stage certification requires its *separate*
documentary evidence and MUST keep C03-07, C03 chapter and C04 locked.
"""
FIFTH_STATUS = (
    "**C03-01, C03-02, C03-03 Y C03-04 CERTIFICADAS (4/10); "
    "C03-05 CERRADA TÉCNICAMENTE Y ACEPTADA PARA CERTIFICACIÓN (5/10)**"
)
CERTIFIED_STATUS = "**C03-01 A C03-05 CERTIFICADAS (5/10)**"
SIXTH_STATUS = "**C03-01 A C03-06 CERTIFICADAS (6/10)**"
SEVENTH_STATUS = "**C03-01 A C03-07 CERTIFICADAS (7/10)**"
SEVENTH_MERGE = "b1e019afabfb44f05076024b8d34cde74a340acc"
SEVENTH_TECH_MERGE = "a256885afa5ee2f639bd4ed6f7d8595b709e6409"
SEVENTH_PROOF = "C03_07_CERTIFICACION_POSTMERGE.md"

CERTIFIED_MERGE = "12fb19b0c35038c297939e17b27770c4fd63f77e"
SIXTH_MERGE = "a1923d117febc5fd4e5a544b558c0f85ad517b44"
PROOF_DOC = "C03_05_CERTIFICACION_POSTMERGE.md"
SIXTH_PROOF = "C03_06_CERTIFICACION_POSTMERGE.md"


def seventh_certified(c03_row):
    return SEVENTH_STATUS in c03_row


def sixth_certified(c03_row):
    return SIXTH_STATUS in c03_row or seventh_certified(c03_row)


def fifth_certified(c03_row):
    return CERTIFIED_STATUS in c03_row or sixth_certified(c03_row)


def fifth_active(c03_row):
    return fifth_certified(c03_row) or FIFTH_STATUS in c03_row


def next_locked(c03_row):
    if seventh_certified(c03_row):
        return "C03-08 NO INICIADA"
    return "C03-07 NO INICIADA" if sixth_certified(c03_row) else "C03-06 NO INICIADA"


def next_illicit(c03_row):
    if seventh_certified(c03_row):
        return "C03-08 INICIADA"
    return "C03-07 INICIADA" if sixth_certified(c03_row) else "C03-06 INICIADA"


def next_topic(c03_row):
    if seventh_certified(c03_row):
        return "C03-08"
    return "C03-07" if sixth_certified(c03_row) else "C03-06"


def fifth_status_marker(plan):
    """The exactly documented current-stage marker used by historical mutations."""
    for marker in (SEVENTH_STATUS, SIXTH_STATUS, CERTIFIED_STATUS, FIFTH_STATUS):
        if marker in plan:
            return marker
    raise AssertionError("No recognized fifth/sixth/seventh phase provenance in plan")


def fifth_count_before(plan):
    if SEVENTH_STATUS in plan:
        return SEVENTH_STATUS
    if SIXTH_STATUS in plan:
        return SIXTH_STATUS
    return CERTIFIED_STATUS if CERTIFIED_STATUS in plan else "CERTIFICACIÓN (5/10)**"


def fifth_count_after(plan):
    if SEVENTH_STATUS in plan:
        return "**C03-01 A C03-08 CERTIFICADAS (8/10)**"
    if SIXTH_STATUS in plan:
        return SEVENTH_STATUS
    return (SIXTH_STATUS if CERTIFIED_STATUS in plan else "CERTIFICACIÓN (6/10)**")


def fifth_safe(c03_row):
    seventh = seventh_certified(c03_row)
    sixth = SIXTH_STATUS in c03_row
    fifth = CERTIFIED_STATUS in c03_row
    historical = FIFTH_STATUS in c03_row
    if sum(map(int, (seventh, sixth, fifth, historical))) != 1:
        raise AssertionError("Exactly one current C03 stage 5/6/7 required")
    common = ("C03 NO CERTIFICADO", "C04 BLOQUEADO",
              "2/37 capítulos certificados (C01 y C02)")
    if not all(marker in c03_row for marker in common):
        raise AssertionError("Sequential chapter locks lost")
    if seventh:
        exact = ("C03-05 CERTIFICADA (5/10)", "C03-06 CERTIFICADA (6/10)",
                 "C03-07 CERTIFICADA (7/10)", "C03-08 NO INICIADA",
                 "PR #61", "PR #64", "5/5 POSTMERGE SUCCESS",
                 CERTIFIED_MERGE, SIXTH_MERGE, SIXTH_PROOF,
                 SEVENTH_MERGE, SEVENTH_TECH_MERGE, SEVENTH_PROOF)
        if not all(marker in c03_row for marker in exact):
            raise AssertionError("Seventh stage lacks independent acta/CI provenance")
        if "8/10" in c03_row or "C03 CERTIFICADO" in c03_row:
            raise AssertionError("Eighth stage or entire chapter illegally advanced")
    elif sixth:
        exact = ("C03-05 CERTIFICADA (5/10)", "C03-06 CERTIFICADA (6/10)",
                 "C03-07 NO INICIADA", "PR #61", "5/5 POSTMERGE SUCCESS",
                 SIXTH_MERGE, SIXTH_PROOF, CERTIFIED_MERGE)
        if not all(marker in c03_row for marker in exact):
            raise AssertionError("Sixth stage lacks exact historical provenance")
        if "7/10" in c03_row or "C03 CERTIFICADO" in c03_row:
            raise AssertionError("Seventh stage illegally advanced")
    else:
        if "C03-06 NO INICIADA" not in c03_row:
            raise AssertionError("Fifth stage lost next-phase lock")
        if fifth:
            for item in ("PR #58", "11/11 POSTMERGE SUCCESS", CERTIFIED_MERGE,
                         PROOF_DOC, "C03-05 CERTIFICADA (5/10)"):
                if item not in c03_row:
                    raise AssertionError("Fifth stage lacks provenance: " + item)
        if "6/10" in c03_row or "C03 CERTIFICADO" in c03_row:
            raise AssertionError("Sixth stage illegally advanced")
    return True


def fifth_plan_safe(plan_text):
    """Fail closed unless the active heading, current C03 row and C04 agree."""
    lines = plan_text.splitlines()
    row = next((line for line in lines if line.startswith("C03 | ")), "")
    if not fifth_safe(row):
        return False
    active = next((line for line in lines[:7] if "**ESTADO VIGENTE:" in line), "")
    mandatory = ("2/37 capítulos certificados (C01 y C02)",
                 next_locked(row), "C03 NO CERTIFICADO", "C04 BLOQUEADO")
    if not all(marker in active for marker in mandatory):
        raise AssertionError("Active heading contradicts locked C03 row")
    if seventh_certified(row):
        markers = ("C03-05 CERTIFICADA (5/10)", "C03-06 CERTIFICADA (6/10)",
                   "C03-07 CERTIFICADA (7/10)", "5/5 CI POSTMERGE SUCCESS",
                   SEVENTH_MERGE, SEVENTH_TECH_MERGE)
        if not all(marker in active for marker in markers):
            raise AssertionError("Seventh-stage heading lacks exact acta/CI evidence")
        if "8/10" in active or "C03 CERTIFICADO" in active:
            raise AssertionError("Eighth stage/chapter advanced before approval")
    elif sixth_certified(row):
        for marker in ("C03-05 CERTIFICADA (5/10)",
                       "C03-06 CERTIFICADA (6/10)",
                       "5/5 CI POSTMERGE SUCCESS", SIXTH_MERGE):
            if marker not in active:
                raise AssertionError("Sixth-stage heading lacks immutable evidence")
        if "7/10" in active or "C03 CERTIFICADO" in active:
            raise AssertionError("Seventh stage/chapter prematurely advanced")
    elif CERTIFIED_STATUS in row:
        for marker in ("C03-05 CERTIFICADA (5/10)", "11/11 CI POSTMERGE SUCCESS",
                       CERTIFIED_MERGE):
            if marker not in active:
                raise AssertionError("Fifth-stage heading lacks immutable evidence")
        if "6/10" in active or "C03 CERTIFICADO" in active:
            raise AssertionError("Sixth stage/chapter prematurely advanced")
    else:
        if "C03-05 CERRADA TÉCNICAMENTE" not in active:
            raise AssertionError("Historical fifth technical heading missing")
        if "6/10" in active or "C03 CERTIFICADO" in active:
            raise AssertionError("Sixth stage/chapter prematurely advanced")
    return True
