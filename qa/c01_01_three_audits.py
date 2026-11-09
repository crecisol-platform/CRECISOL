#!/usr/bin/env python3
"""Three isolated audits of C01-01 documentation, not of the whole SaaS."""
import re
import sys
from pathlib import Path
import c01_verify_source as canonical

ROOT = Path(__file__).resolve().parents[1]
MATRIX = ROOT / "docs/c01/C01_01_MATRIZ_REQUISITOS.md"
GATE = ROOT / "docs/c01/C01_01_DEPENDENCIAS_Y_GATE.md"
ACTA = ROOT / "docs/c01/C01_01_ACTA_MICROFASE.md"
PLAN = ROOT / "docs/C01_PREPARACION.md"
ADR = ROOT / "docs/DECISIONES_ABIERTAS.md"

def require(ok, reason):
    if not ok:
        raise AssertionError(reason)

def row_map(doc, prefix):
    expression = r"^\|\s*(" + (r"P\d{2}" if prefix == "P" else r"T\d{2}-R\d{2}") + r")\s*\|([^\n]+)$"
    output = {}
    for m in re.finditer(expression, doc, re.M):
        require(m.group(1) not in output, "Repeated row: " + m.group(1))
        output[m.group(1)] = m.group(2)
    return output

def audit_a():
    canonical.main()
    pars, tables, _ = canonical.source_parts()
    text = MATRIX.read_text(encoding="utf-8")
    rows = row_map(text, "P")
    data = row_map(text, "T")
    anchors = [
        ("SaaS universal","SaaS universal"), ("sola base de producto","sola base modular"),
        ("Invariantes de diseño","invariantes de diseño"), ("Ningún dato privado","Aislamiento total"),
        ("No existe cobro real","Cobros reales"), ("Una URL no representa autorización","URL e ID aleatorio"),
        ("datos validados","datos validados"), ("El SuperAdmin gobierna","SuperAdmin controla"),
        ("modificación de horario","Cambios de horario"), ("restauración verificada","restauración verificada"),
        ("Límites deliberados","límites deliberados"), ("sistemas hospitalarios","hospital"),
        ("Identidad oficial","identidad oficial"), ("nombre comercial definitivo","Nombre CRECISOL"),
        ("CRECI","CRECI"), ("Relación institucional con SOLQARYN","relación institucional con SOLQARYN"),
        ("producto respaldado por SOLQARYN","Respaldo de marca SOLQARYN"),
        ("Repositorio GitHub propio","Repositorio, CI"), ("Hostinger VPS","Hostinger VPS"),
        ("Cloudflare R2","Cloudflare R2"), ("SuperAdmin de CRECISOL","SuperAdmin CRECISOL"),
        ("persona jurídica responsable","entidad jurídica"), ("anticlonación","anticlonación"),
        ("Dominio raíz crecisol.com","crecisol.com"), ("favicon","favicon"),
        ("SPF, DKIM y DMARC","SPF/DKIM/DMARC"), ("enlaces de autenticación","Enlaces auth"),
        ("slugs administrativos","Slugs del sistema"), ("Verificar antes del lanzamiento","Antes de lanzamiento"),
        ("Mensajes comerciales de referencia","mensajes comerciales de referencia"),
        ("Encabezado comercial principal","Encabezado y subtítulo"),
        ("Propuesta de valor","Propuesta de valor"), ("Llamados a la acción","CTAs sugeridos"),
        ("Pruebas de aceptación de marca","pruebas de aceptación de marca")
    ]
    require(len(anchors)==len(pars)==len(rows)==34, "Wrong paragraph anchor count")
    for index, ((_, master), (original, summary)) in enumerate(zip(pars,anchors),start=1):
        key=f"P{index:02d}"
        require(original.casefold() in master.casefold(), "Master anchor missing "+key)
        require(summary.casefold() in rows[key].casefold(), "Matrix summary anchor missing "+key)
    subjects=[
        ["Público objetivo","Producto y servicio mixtos","Diseño sin código","Seguridad por defecto",
         "Performance percibida","Rapidez controlada","No-limitación artificial"],
        ["Nombre público","Lema","Descriptor","Relación de marca","Público objetivo",
         "Dominio comercial elegido","Identidad del negocio","Naturaleza del producto"],
        ["Marca global","Marca del tenant","Relación matriz","Dominio y seguridad",
         "Mensajería y enlaces","Comprobación previa al lanzamiento"]
    ]
    for t,labels in enumerate(subjects,start=1):
        for i,label in enumerate(labels,start=1):
            key=f"T{t:02d}-R{i:02d}"
            require(tables[t-1][i][0]==label and key in data,"Table source/ID mismatch "+key)
    require(len(data)==21,"21 table rows required")
    mutant=dict(rows)
    mutant.pop("P34")
    require(len(mutant)!=34,"Negative: missing source row not detected")
    require(len(re.findall(r"^\|\s*P01\s*\|", text+"\n| P01 | DUPLICATE |",re.M))==2,
            "Negative: duplicate source row not detected")
    print("AUDIT A PASS: 34 semantic anchors, 21 source table subjects,")
    print("55 IDs and two negative controls (missing/duplicate).")

def is_secret(value):
    return bool(re.search(r"(?i)\b(?:password|pwd|secret|api[_-]?key)\s*[:=]\s*[^\s|]+",value))

def audit_b():
    for directory in ("frontend","backend","database"):
        root=ROOT/directory
        if root.exists():
            dangerous=[p for p in root.rglob("*") if p.is_file()
                       and p.suffix in {".cs",".csproj",".ts",".sql",".json",".env"}]
            require(not dangerous,"Product code present in isolated gate: "+directory)
    for path in (MATRIX,GATE,ACTA,PLAN,ADR):
        require(not is_secret(path.read_text(encoding="utf-8")),
                "Secret assignment in "+str(path))
    for path in (ROOT/"qa/c01_verify_source.py",ROOT/"qa/c01_01_three_audits.py"):
        source=path.read_text(encoding="utf-8")
        require(not re.search(r"^\s*(?:import|from)\s+(?:urllib|requests|httpx|socket|subprocess)\b",source,re.M),
                "Unexpected remote execution in "+str(path))
    require(is_secret("Password=unsafe-value"),"Negative: unsafe DSN not detected")
    require(not is_secret("A password must never be committed"),"Negative: prose false positive")
    require("NO VERIFICADO" in GATE.read_text(encoding="utf-8"),
            "Unverified Hostinger/domain claim missing")
    print("AUDIT B PASS: no application code, DSN, secrets or network")
    print("executables in C01-01; positive/negative secret detection.")

def linked(path,target):
    if target.startswith(("http://","https://","#","mailto:")):
        return True
    destination=(path.parent/target.split("#",1)[0]).resolve()
    return destination.is_relative_to(ROOT.resolve()) and destination.is_file()

def audit_c():
    link_count=0
    for path in (MATRIX,GATE,ACTA,PLAN,ADR):
        body=path.read_text(encoding="utf-8")
        for target in re.findall(r"\[[^\]]+\]\(([^)]+)\)",body):
            require(linked(path,target),"Broken Markdown link: "+target+" in "+path.name)
            link_count+=1
    require(not linked(GATE,"nonexistent.md"),"Negative: broken link not detected")
    plan=PLAN.read_text(encoding="utf-8")
    gate=GATE.read_text(encoding="utf-8")
    for i in range(1,9):
        stage=f"C01-{i:02d}"
        require(re.search(r"^\|\s*"+stage+r"\s*\|",plan,re.M),
                "Missing ordered microphase "+stage)
        require(stage in gate,"Missing dependency mapping "+stage)
    require("MAE-C01-01" in gate and "MAE-C01-08" in gate,
            "Master vs operating namespace resolution missing")
    decisions=ADR.read_text(encoding="utf-8")
    require("ADR-010" in decisions and "ADR-011" in decisions,
            "Missing Hostinger/phase numbering ADR")
    if "**Estado:** CAPÍTULO 01 CERTIFICADO" in plan:
        require("8/8" in plan and "1/37" in plan,
                "Certified C01 must have an updated 8/8 and 1/37 plan")
    else:
        require("0/37" in plan,"Uncertified C01 must preserve its 0/37 gate")
    require("C01-08" in gate and "auditoría a" in gate.casefold(),
            "Full chapter audit retained for C01-08")
    print(f"AUDIT C PASS: {link_count} Markdown links, 8 dependency gates,")
    print("dual nomenclature, chapter-gate status and broken-link negative test.")

if __name__=="__main__":
    try:
        require(len(sys.argv)==2 and sys.argv[1] in ("A","B","C"),
                "Choose one audit A, B, C")
        {"A":audit_a,"B":audit_b,"C":audit_c}[sys.argv[1]]()
    except (AssertionError,ValueError,FileNotFoundError) as exc:
        print(f"AUDIT {sys.argv[1] if len(sys.argv)>1 else '?'} FAIL: {exc}",file=sys.stderr)
        sys.exit(1)
