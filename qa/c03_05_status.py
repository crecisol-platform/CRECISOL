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
