#!/usr/bin/env python3
"""CRECISOL C05-11 institutional A/B/C - chapter audit, synthetic ONLY.

The original 20-risk matrix keeps PENDIENTE as preparation; the conditional
act becomes effective ONLY after verified technical and documentary gates.
No C06 development or productive identity API is authorized here.
"""
from __future__ import annotations
import json
import os
from pathlib import Path
import re
import subprocess
import sys
from c05_01_preparation_audits import ROOT

REPO="crecisol-platform/CRECISOL"
BASE="81a31107077fa439e2a7a3ecb41964cfca5ea081"
BRANCH="feature/c05-11-institutional-20261114"
FILES={
 "act":"docs/c05/C05_11_CERTIFICACION_INSTITUCIONAL.md",
 "qa":"qa/c05_11_closure_gate.py",
 "ci":".github/workflows/c05-11-certification.yml",
}
ALLOWED=set(FILES.values())
PROOF=ROOT/".c05-11-institutional-proof"
GUARDS={
 "act":(
  "Acta institucional CONDICIONADA",
  "ACTA CONDICIONADA, NO EFICAZ",
  "C05-11 NO CERTIFICADA","CAPÍTULO C05 NO CERTIFICADO",
  "C06 BLOQUEADO","crecisol-platform/CRECISOL",
  "1450414592","Documento Maestro Único",
  "ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c",
  "0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77",
  "fe7aaab60b603f1844e18666e2f99c0ea97e86e9",
  "93c8412b2450e85a882b622e7a012477c61c2359",
  "No habilitar C06 sin identidades confiables",
  "COM-T08/COM-T09/COM-T10","NO EJECUTADAS",
  "C05-01","C05-02","C05-03","C05-04","C05-05",
  "C05-06","C05-07","C05-08","C05-09","C05-10",
  "crecisol-platform/CRECISOL/pull/114",
  "crecisol-platform/CRECISOL/pull/130",
  "crecisol-platform/CRECISOL/pull/132",
  "crecisol-platform/CRECISOL/pull/134",
  "crecisol-platform/CRECISOL/pull/136",
  "crecisol-platform/CRECISOL/pull/138",
  "crecisol-platform/CRECISOL/pull/140",
  "crecisol-platform/CRECISOL/pull/142",
  "crecisol-platform/CRECISOL/pull/144",
  "crecisol-platform/CRECISOL/pull/147",
  "open/draft","PR antiguo #121",
  "43643788104","5 ADMITTED / 4 LIMITED / 7 UNAVAILABLE",
  "SYSDATE(6)","UTC_TIMESTAMP(6)",
  "7e560b019d4a15abc73594ed0ce116cc7ddf16c4",
  "81a31107077fa439e2a7a3ecb41964cfca5ea081",
  "EXACTAMENTE cuatro archivos",
  "43648572848","43648572867","43649707384","43649707336",
  "128 mutaciones","17 runners MySQL8.4",
  "Argon2id","WebAuthn","HMAC","AES256-GCM",
  "C04-11","MySQL8.4","Angular22","Chromium",
  "6x16","5/11/0",
  "R05-01..20","R05-18","R05-19",
  "exactamente 3 archivos",
  "2/2 SUCCESS","2/2 completed/success",
  "NO-GO PRODUCTIVO","Rollback",
  "backend/src/Crecisol.Api/Program.cs",
  "/api/v1/public/branding/platform",
  "5/37","C05 productivo certificado",
  "solqaryn/Solqaryn","main",
 ),
 "ci":(
  "permissions:\n  contents: read",
  "persist-credentials: false","fetch-depth: 2",
  "dotnet-version: '10.0.x'",
  "node-version: '24.15.0'",
  "qa/c05_11_closure_gate.py A",
  "qa/c05_11_closure_gate.py B",
  "qa/c05_11_closure_gate.py C",
  "qa/c05_11_integral_audits.py A",
  "qa/c05_11_integral_audits.py B",
  "Crecisol.AccountIdentity.ContractsTests.csproj",
  "Crecisol.PasswordHash.ContractsTests.csproj",
  "Crecisol.ContextualLogin.ContractsTests.csproj",
  "Crecisol.MfaRisk.ContractsTests.csproj",
  "c05_02_neutral_registration_http_mysql.sh",
  "c05_02_signed_recipient_mysql_smoke.sh",
  "c05_02_privacy_retention_mysql.sh",
  "c05_05_session_http_mysql.sh",
  "c05_06_recovery_http_mysql.sh",
  "c05_07_mailbox_http_mysql.sh",
  "c05_08_mfa_http_mysql.sh",
  "c05_09_private_profiles_http_mysql.sh",
  "c05_10_rate_limit_http_mysql.sh",
  ".c05-11-institutional-proof",
  "GITHUB_SHA","GITHUB_RUN_ID",
 ),
}

def need(ok:bool,why:str)->None:
 if not ok:raise AssertionError("C05-11 institutional NO-GO: "+why)

def sources()->dict[str,str]:
 return {key:(ROOT/path).read_text(encoding="utf-8")
         for key,path in FILES.items() if key!="qa"}

def invariants(data:dict[str,str])->None:
 for kind,markers in GUARDS.items():
  for marker in markers:
   need(marker in data[kind],"Missing institutional guard: "+kind+" :: "+marker)
 app=(ROOT/"backend/src/Crecisol.Api/Program.cs").read_text()
 routes=re.findall(r'app\.Map(?:Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"',app)
 need(routes==["/api/v1/public/branding/platform"] and
      "ThrottledContextualPasswordAttempt" not in app,
      "Fixture identity API must never leak into production")
 matrix=(ROOT/"docs/c05/C05_11_MATRIZ_RIESGOS.md").read_text()
 need(re.findall(r"(?m)^\| R05-(\d\d) \|",matrix)==
      [f"{i:02d}" for i in range(1,21)],
      "Chapter matrix must preserve 20 distinct risks")
 technical=(ROOT/"qa/c05_11_integral_audits.py").read_text()
 need("C05_SQL=[" in technical and "GUARDS={" in technical and
      "C06 stays blocked" in technical,
      "Technical A/B/C and no-go identity acceptance must remain intact")
 ci=data["ci"]
 need("workflow_dispatch:" not in ci and
      "id-token: write" not in ci and
      "deploy" not in ci.lower(),
      "Institutional CI may not publish or access production")

def audit_a()->None:
 subprocess.run([sys.executable,"qa/master_source_guard.py","--self-test"],
                cwd=ROOT,check=True)
 subprocess.run([sys.executable,"qa/c05_11_integral_audits.py","A"],
                cwd=ROOT,check=True)
 plan=(ROOT/"docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md").read_text()
 need("C05-11 | Auditorías integrales y acta C05" in plan and
      "No habilitar C06 sin identidades confiables" in plan,
      "Master-derived chapter-negative C06 gate missing")
 invariants(sources())
 print("PASS C05-11 institutional A: exact Word/PDF authority, 10 previous merged act references, 20 risks, C06 NO-GO")

def audit_b()->None:
 data=sources();invariants(data)
 killed=0
 for kind,markers in GUARDS.items():
  for marker in markers:
   altered=dict(data)
   altered[kind]=data[kind].replace(marker,"[C05_11_REMOVED_GUARD]")
   need(altered[kind]!=data[kind],"Ineffective mutation "+marker)
   try:invariants(altered)
   except AssertionError:killed+=1
   else:raise AssertionError("Undetected hostile identity/PII scope mutation "+marker)
 need(killed>=90,"Insufficient independent institutional guard removals")
 subprocess.run([sys.executable,"qa/c05_11_integral_audits.py","B"],
                cwd=ROOT,check=True)
 print(f"PASS C05-11 institutional B: {killed} hostile documentary/security guard removals rejected; technical A/B and SQL required separately")

def git(*args:str)->str:
 return subprocess.check_output(["git",*args],cwd=ROOT,
     stderr=subprocess.DEVNULL,text=True).strip()

def audit_c()->None:
 need(os.getenv("GITHUB_ACTIONS")=="true" and
      os.getenv("GITHUB_REPOSITORY")==REPO,
      "Institutional attestation requires genuine CRECISOL Actions")
 sha,run=os.getenv("GITHUB_SHA",""),os.getenv("GITHUB_RUN_ID","")
 need(re.fullmatch(r"[0-9a-f]{40}",sha) is not None and
      re.fullmatch(r"[0-9]+",run) is not None and
      git("rev-parse","HEAD")==sha,
      "Exact SHA and workflow run proof required")
 for stage in ("A","B","build","mysql","frontend","chromium"):
  f=PROOF/stage
  need(f.is_file() and f.read_text(encoding="utf-8").strip()==
       f"PASS {stage} sha={sha} run={run}",
       "Missing actual same-SHA/same-run institutional chapter test: "+stage)
 if os.getenv("GITHUB_EVENT_NAME")=="pull_request":
  f=Path(os.getenv("GITHUB_EVENT_PATH",""))
  need(f.is_file(),"Signed PR event required")
  event=json.loads(f.read_text())["pull_request"]
  base,head=event["base"],event["head"]
  need(base["ref"]=="dev" and base["sha"]==BASE and
       head["ref"]==BRANCH and base["repo"]["full_name"]==REPO and
       head["repo"]["full_name"]==REPO,
       "Wrong chapter institutional PR base/head")
  need(git("rev-list","--parents","-n","1","HEAD").split()==
       [sha,base["sha"],head["sha"]],
       "PR merge lineage not from exact C05-11 technical squash")
  changed=git("diff","--name-only",base["sha"],head["sha"]).splitlines()
 else:
  need(os.getenv("GITHUB_EVENT_NAME")=="push" and
       os.getenv("GITHUB_REF")=="refs/heads/dev",
       "Only dev institutional POSTMERGE eligible")
  need(git("rev-list","--parents","-n","1","HEAD").split()==
       [sha,BASE],
       "Institutional squash must be direct child of technical C05-11")
  changed=git("show","--format=","--name-only",sha).splitlines()
 need(len(changed)==len(ALLOWED) and set(changed)==ALLOWED,
      "Exactly 3 independent C05-11 institutional files required: "+repr(changed))
 invariants(sources())
 print(f"PASS C05-11 institutional C: Word+ A/B, .NET10, real HTTP/MySQL8.4, Angular22/Chromium SAME SHA={sha} run={run}; C06 blocked")

if __name__=="__main__":
 try:
  need(len(sys.argv)==2 and sys.argv[1] in ("A","B","C"),
       "Use qa/c05_11_closure_gate.py A|B|C")
  {"A":audit_a,"B":audit_b,"C":audit_c}[sys.argv[1]]()
 except (AssertionError,OSError,KeyError,ValueError,subprocess.SubprocessError) as e:
  print("FAIL C05-11 institutional: "+str(e),file=sys.stderr)
  sys.exit(1)
