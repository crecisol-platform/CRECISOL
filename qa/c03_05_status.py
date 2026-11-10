"""CRECISOL C03: append-only sequential state guards for stages five to eight.

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
EIGHTH_STATUS = "**C03-01 A C03-08 CERTIFICADAS (8/10)**"
CERTIFIED_MERGE = "12fb19b0c35038c297939e17b27770c4fd63f77e"
SIXTH_MERGE = "a1923d117febc5fd4e5a544b558c0f85ad517b44"
SEVENTH_TECH_MERGE = "a256885afa5ee2f639bd4ed6f7d8595b709e6409"
SEVENTH_ACTA_MERGE = "b1e019afabfb44f05076024b8d34cde74a340acc"
EIGHTH_TECH_MERGE = "221afb6cc515fc79a3b6ca6e010af11f2e20b0a2"
EIGHTH_ACTA_MERGE = "b333e58378b6a5ea0438f1ee6b11de85d9ce04df"
PROOF_DOC = "C03_05_CERTIFICACION_POSTMERGE.md"
SIXTH_PROOF = "C03_06_CERTIFICACION_POSTMERGE.md"
SEVENTH_PROOF = "C03_07_CERTIFICACION_POSTMERGE.md"
EIGHTH_PROOF = "C03_08_CERTIFICACION_POSTMERGE.md"


def eighth_certified(c03_row):
    return EIGHTH_STATUS in c03_row


def seventh_certified(c03_row):
    return SEVENTH_STATUS in c03_row or eighth_certified(c03_row)


def sixth_certified(c03_row):
    # Includes its certified successor. Archived sixth-stage evidence remains immutable.
    return SIXTH_STATUS in c03_row or seventh_certified(c03_row)


def fifth_certified(c03_row):
    return CERTIFIED_STATUS in c03_row or sixth_certified(c03_row)


def fifth_active(c03_row):
    return fifth_certified(c03_row) or FIFTH_STATUS in c03_row


def next_locked(c03_row):
    return ("C03-09 NO INICIADA" if eighth_certified(c03_row) else
            "C03-08 NO INICIADA" if seventh_certified(c03_row) else
            "C03-07 NO INICIADA" if sixth_certified(c03_row) else "C03-06 NO INICIADA")


def next_illicit(c03_row):
    return ("C03-09 INICIADA" if eighth_certified(c03_row) else
            "C03-08 INICIADA" if seventh_certified(c03_row) else
            "C03-07 INICIADA" if sixth_certified(c03_row) else "C03-06 INICIADA")


def next_topic(c03_row):
    return "C03-09" if eighth_certified(c03_row) else "C03-08" if seventh_certified(c03_row) else "C03-07" if sixth_certified(c03_row) else "C03-06"


def fifth_status_marker(plan):
    """Exact first matching active marker for negative documentary mutations."""
    if EIGHTH_STATUS in plan:
        return EIGHTH_STATUS
    if SEVENTH_STATUS in plan:
        return SEVENTH_STATUS
    if SIXTH_STATUS in plan:
        return SIXTH_STATUS
    if CERTIFIED_STATUS in plan:
        return CERTIFIED_STATUS
    if FIFTH_STATUS in plan:
        return FIFTH_STATUS
    raise AssertionError("No recognized fifth/sixth-phase provenance in plan")


def fifth_count_before(plan):
    if EIGHTH_STATUS in plan:
        return EIGHTH_STATUS
    if SEVENTH_STATUS in plan:
        return SEVENTH_STATUS
    if SIXTH_STATUS in plan:
        return SIXTH_STATUS
    return CERTIFIED_STATUS if CERTIFIED_STATUS in plan else "CERTIFICACIÓN (5/10)**"


def fifth_count_after(plan):
    if EIGHTH_STATUS in plan:
        return "**C03-01 A C03-09 CERTIFICADAS (9/10)**"
    if SEVENTH_STATUS in plan:
        return "**C03-01 A C03-08 CERTIFICADAS (8/10)**"
    if SIXTH_STATUS in plan:
        return "**C03-01 A C03-07 CERTIFICADAS (7/10)**"
    return ("**C03-01 A C03-06 CERTIFICADAS (6/10)**"
            if CERTIFIED_STATUS in plan else "CERTIFICACIÓN (6/10)**")


def fifth_safe(c03_row):
    eighth = eighth_certified(c03_row)
    seventh = seventh_certified(c03_row)
    sixth = sixth_certified(c03_row)
    fifth = CERTIFIED_STATUS in c03_row
    historical = FIFTH_STATUS in c03_row
    require_exactly_one = int(sixth) + int(fifth) + int(historical)
    if require_exactly_one == 0:
        return False
    if require_exactly_one != 1:
        raise AssertionError("Conflicting current fifth/sixth-phase statuses")
    common = ("C03 NO CERTIFICADO", "C04 BLOQUEADO",
              "2/37 capítulos certificados (C01 y C02)")
    if not all(item in c03_row for item in common):
        raise AssertionError("Sequential chapter locks lost")
    if eighth:
        exact = (
            "C03-05 CERTIFICADA (5/10)", "C03-06 CERTIFICADA (6/10)",
            "C03-07 CERTIFICADA (7/10)", "C03-08 CERTIFICADA (8/10)",
            "C03-09 NO INICIADA", "PR #61", "PR #63", "PR #64",
            "PR #70", "PR #71", "8/8 POSTMERGE SUCCESS",
            "5/5 POSTMERGE SUCCESS", "6/6 POSTMERGE SUCCESS",
            SIXTH_MERGE, SIXTH_PROOF, SEVENTH_TECH_MERGE,
            SEVENTH_ACTA_MERGE, SEVENTH_PROOF, EIGHTH_TECH_MERGE,
            EIGHTH_ACTA_MERGE, EIGHTH_PROOF
        )
        if not all(item in c03_row for item in exact):
            raise AssertionError("Eighth-stage status missing verified technical/acta provenance or ninth lock")
        if "9/10" in c03_row or "C03 CERTIFICADO" in c03_row:
            raise AssertionError("Ninth microphase or whole chapter illegally advanced")
    elif seventh:
        exact = (
            "C03-05 CERTIFICADA (5/10)", "C03-06 CERTIFICADA (6/10)",
            "C03-07 CERTIFICADA (7/10)", "C03-08 NO INICIADA",
            "PR #61", "PR #63", "PR #64",
            "8/8 POSTMERGE SUCCESS", "5/5 POSTMERGE SUCCESS",
            SIXTH_MERGE, SIXTH_PROOF, SEVENTH_TECH_MERGE,
            SEVENTH_ACTA_MERGE, SEVENTH_PROOF
        )
        if not all(item in c03_row for item in exact):
            raise AssertionError("Seventh-stage status lacks exact verified tech/acta CI or C03-08 lock")
        if "8/10" in c03_row or "C03 CERTIFICADO" in c03_row:
            raise AssertionError("Eighth microphase or whole chapter illegally advanced")
    elif sixth:
        exact = (
            "C03-05 CERTIFICADA (5/10)", "C03-06 CERTIFICADA (6/10)",
            "C03-07 NO INICIADA", "PR #61", "5/5 POSTMERGE SUCCESS",
            SIXTH_MERGE, SIXTH_PROOF, CERTIFIED_MERGE
        )
        if not all(item in c03_row for item in exact):
            raise AssertionError("Sixth-stage status has no verified previous gates or locked next stage")
        if "7/10" in c03_row or "C03 CERTIFICADO" in c03_row:
            raise AssertionError("Seventh microphase or chapter illegally advanced")
    else:
        if "C03-06 NO INICIADA" not in c03_row:
            raise AssertionError("Fifth-stage status lost next-phase lock")
        if fifth:
            for item in ("PR #58", "11/11 POSTMERGE SUCCESS", CERTIFIED_MERGE,
                         PROOF_DOC, "C03-05 CERTIFICADA (5/10)"):
                if item not in c03_row:
                    raise AssertionError("Final fifth-stage status lacks verified provenance: " + item)
        if "6/10" in c03_row or "C03 CERTIFICADO" in c03_row:
            raise AssertionError("Sixth phase/chapter illegally advanced before its acta")
    return True


def fifth_plan_safe(plan_text):
    """Fail closed when active heading and C03 row disagree on current stage."""
    rows = plan_text.splitlines()
    row = next((line for line in rows if line.startswith("C03 | ")), "")
    if not fifth_safe(row):
        return False
    active = next((line for line in rows[:7] if "**ESTADO VIGENTE:" in line), "")
    mandatory = (
        "2/37 capítulos certificados (C01 y C02)",
        next_locked(row), "C03 NO CERTIFICADO", "C04 BLOQUEADO",
    )
    if not all(item in active for item in mandatory):
        raise AssertionError("Active heading contradicts the locked C03 row")
    if eighth_certified(row):
        for item in ("C03-05 CERTIFICADA (5/10)",
                     "C03-06 CERTIFICADA (6/10)",
                     "C03-07 CERTIFICADA (7/10)",
                     "C03-08 CERTIFICADA (8/10)",
                     "8/8 CI POSTMERGE SUCCESS", "5/5 CI POSTMERGE SUCCESS",
                     "6/6 CI POSTMERGE SUCCESS",
                     SEVENTH_TECH_MERGE, SEVENTH_ACTA_MERGE,
                     EIGHTH_TECH_MERGE, EIGHTH_ACTA_MERGE,
                     "C03-09 NO INICIADA"):
            if item not in active:
                raise AssertionError("Eighth-stage heading lacks verified historical and current CI: " + item)
        if "9/10" in active or "C03 CERTIFICADO" in active:
            raise AssertionError("Ninth stage/chapter illegally promoted")
    elif seventh_certified(row):
        for item in ("C03-05 CERTIFICADA (5/10)",
                     "C03-06 CERTIFICADA (6/10)",
                     "C03-07 CERTIFICADA (7/10)",
                     "8/8 CI POSTMERGE SUCCESS", "5/5 CI POSTMERGE SUCCESS",
                     SEVENTH_TECH_MERGE, SEVENTH_ACTA_MERGE,
                     "C03-08 NO INICIADA"):
            if item not in active:
                raise AssertionError("Seventh-stage heading missing exact historical evidence: " + item)
        if "8/10" in active or "C03 CERTIFICADO" in active:
            raise AssertionError("Eighth stage/chapter illegally promoted")
    elif sixth_certified(row):
        for item in ("C03-05 CERTIFICADA (5/10)",
                     "C03-06 CERTIFICADA (6/10)",
                     "5/5 CI POSTMERGE SUCCESS", SIXTH_MERGE):
            if item not in active:
                raise AssertionError("Certified sixth-stage heading lacks immutable evidence: " + item)
        if "7/10" in active or "C03 CERTIFICADO" in active:
            raise AssertionError("Seventh phase/chapter illegally advanced in active heading")
    elif CERTIFIED_STATUS in row:
        for item in ("C03-05 CERTIFICADA (5/10)", "11/11 CI POSTMERGE SUCCESS",
                     CERTIFIED_MERGE):
            if item not in active:
                raise AssertionError("Certified fifth-stage heading lacks immutable evidence: " + item)
        if "6/10" in active or "C03 CERTIFICADO" in active:
            raise AssertionError("Sixth phase/chapter illegally advanced in active heading")
    else:
        if "C03-05 CERRADA TÉCNICAMENTE" not in active:
            raise AssertionError("Historical fifth technical heading missing")
        if "6/10" in active or "C03 CERTIFICADO" in active:
            raise AssertionError("Sixth phase/chapter illegally advanced")
    return True
