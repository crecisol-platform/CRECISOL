"""C03-05 exact status semantics for historic C02/C03 gates.

Accepts a fifth microphase only when C03-01..04 remain independently certified;
a sixth cannot begin from any historical gate.
"""
FIFTH_STATUS = (
    "**C03-01, C03-02, C03-03 Y C03-04 CERTIFICADAS (4/10); "
    "C03-05 CERRADA TÉCNICAMENTE Y ACEPTADA PARA CERTIFICACIÓN (5/10)**"
)

def fifth_active(c03_row):
    return FIFTH_STATUS in c03_row

def fifth_safe(c03_row):
    if not fifth_active(c03_row):
        return False
    required = (
        FIFTH_STATUS, "C03-06 NO INICIADA", "C03 NO CERTIFICADO",
        "C04 BLOQUEADO", "2/37 capítulos certificados (C01 y C02)"
    )
    if not all(item in c03_row for item in required):
        raise AssertionError("C03-05 fifth-phase status lost its sequential locks")
    if "6/10" in c03_row or "C03 CERTIFICADO" in c03_row:
        raise AssertionError("C03 chapter/sixth phase unlawfully promoted")
    return True

def fifth_plan_safe(plan_text):
    """Fail closed when heading and chapter row disagree on fifth-phase state.

    This detects malicious edits to either representation. Historical gates
    need both; a C03 row alone is insufficient as a current-state proof.
    """
    rows = plan_text.splitlines()
    c03 = next((row for row in rows if row.startswith("C03 | ")), "")
    if not fifth_safe(c03):
        return False
    active = next((row for row in rows[:7] if "**ESTADO VIGENTE:" in row), "")
    mandatory = (
        "2/37 capítulos certificados (C01 y C02)",
        "C03-05 CERRADA TÉCNICAMENTE",
        "C03-06 NO INICIADA",
        "C03 NO CERTIFICADO",
        "C04 BLOQUEADO",
    )
    if not all(item in active for item in mandatory):
        raise AssertionError("C03-05 active heading differs from locked chapter row")
    if "6/10" in active or "C03 CERTIFICADO" in active:
        raise AssertionError("C03-06/C03 chapter illegally advanced in active heading")
    return True
