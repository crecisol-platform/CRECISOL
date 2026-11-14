#!/usr/bin/env python3
"""C05-10 institutional A/B/C, SHA-locked real rate-limit HTTP/MySQL proof.
No product auth, no real credential/PII pipeline, no certification shortcuts.
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
BRANCH="feature/c05-10-rate-limit-institutional-20261114"
BASE="REMEDIATION_SHA_PENDING"
FILES={
 "act":"docs/c05/ACTA_CERTIFICACION_ACOTADA_C05_10_2026_11_14.md",
 "qa":"qa/c05_10_closure_gate.py",
 "ci":".github/workflows/c05-10-certification.yml",
}
ALLOWED=set(FILES.values())
PROOF=ROOT/".c05-10-institutional-proof"
GUARDS={
 "act":(
  "Acta institucional CONDICIONADA",
  "ACTA NO EFICAZ","C05-10 NO CERTIFICADA",
  "C05-11 BLOQUEADA","C05 9/11",
  "crecisol-platform/CRECISOL",
  "Documento Maestro Único","MASTER_REFERENCE.md",
  "ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c",
  "0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77",
  "Perfiles y fotos seguras","C05-09","6884289441",
  "Rate limit y trazas de acceso",
  "Pruebas contra brute-force y filtrado de logs",
  "No registrar tokens o contraseñas",
  "9fc11e1c779c465975856a40ccb7a84bf7653fe0",
  "1b0a3ec6a62ef38582b0ef514f53cb62965b15cd",
  "EXACTAMENTE 11 archivos",
  "c05_10_policies","c05_10_buckets",
  "c05_10_access_traces","c05_10_access_outcomes",
  "SOURCE","SUBJECT","HMAC-SHA256",
  "Argon2id","MySqlConnector@2.6.2",
  "MySQL 8.4","127.0.0.1",
  "16 solicitudes","5 admitidas / 11 limitadas",
  "120 comprobaciones","163 mutaciones",
  "43643373571","43643373529","43643373711",
  "43643788104","43642793271","43642793209",
  "43642979726",
  "c05_10_admit_once","FOR UPDATE",
  "ON DUPLICATE KEY UPDATE",
  "SYSDATE(6)","UTC_TIMESTAMP(6)",
  "No registrar tokens o contraseñas",
  "No P0/P1","NO-GO PRODUCTIVO",
  "A/B/C","PREMERGE documental","POSTMERGE documental",
  "EXACTAMENTE tres archivos",
  "Rollback","main","solqaryn/Solqaryn",
  "C05-11","C06",
 ),
 "ci":(
  "permissions:\n  contents: read",
  "persist-credentials: false",
  "fetch-depth: 2",
  "dotnet-version: '10.0.x'",
  "qa/c05_10_closure_gate.py A",
  "qa/c05_10_closure_gate.py B",
  "qa/c05_10_closure_gate.py C",
  "Crecisol.RateLimitAccess.MySqlTests.csproj",
  "database/tests/c05_10_rate_limit_http_mysql.sh",
  "Crecisol.PrivateProfiles.MySqlTests.csproj",
  "database/tests/c05_09_private_profiles_http_mysql.sh",
  "Crecisol.MfaRisk.MySqlTests.csproj",
  "database/tests/c05_08_mfa_http_mysql.sh",
  "database/tests/c05_07_mailbox_http_mysql.sh",
  "database/tests/c05_06_recovery_http_mysql.sh",
  "database/tests/c05_05_session_http_mysql.sh",
  "Crecisol.AccountIdentity.ContractsTests.csproj",
  "Crecisol.InvitationSecurity.ContractsTests.csproj",
  "Crecisol.PasswordHash.ContractsTests.csproj",
  "Crecisol.ContextualLogin.ContractsTests.csproj",
  ".c05-10-institutional-proof",
  "GITHUB_SHA","GITHUB_RUN_ID",
 ),
}

def need(ok:bool,reason:str)->None:
 if not ok:raise AssertionError(reason)
def source()->dict[str,str]:
 return {k:(ROOT/v).read_text(encoding="utf-8") for k,v in FILES.items() if k!="qa"}

def invariants(values:dict[str,str])->None:
 for kind,markers in GUARDS.items():
  for marker in markers:
   need(marker in values[kind],
        "Removed C05-10 institutional security/evidence guard "+kind+": "+marker)
 product=(ROOT/"backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
 routes=re.findall(r'app\.Map(?:Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"',product)
 need(routes==["/api/v1/public/branding/platform"] and
      "ThrottledContextualPasswordAttempt" not in product and
      "MySqlDisposablePreAuthRateLimitLedger" not in product,
      "No productive HTTP brute force/PII integration certified")
 sql=(ROOT/"database/tests/fixtures/c05_10_disposable_rate_limits.sql").read_text(encoding="utf-8")
 app=(ROOT/"backend/src/Crecisol.Application/Identity/ThrottledContextualPasswordAttempt.cs").read_text(encoding="utf-8")
 protector=(ROOT/"backend/src/Crecisol.Infrastructure/Identity/SyntheticPreAuthDigestProtector.cs").read_text(encoding="utf-8")
 test=(ROOT/"backend/tests/Crecisol.RateLimitAccess.MySqlTests/Program.cs").read_text(encoding="utf-8")
 csproj=(ROOT/"backend/src/Crecisol.Infrastructure/Crecisol.Infrastructure.csproj").read_text(encoding="utf-8")
 need(all(x in sql for x in (
      "SET @@session.time_zone='+00:00'",
      "SET v_now=SYSDATE(6)",
      "SELECT attempts,window_started_utc,blocked_until_utc",
      "dimension='SOURCE'","dimension='SUBJECT'",
      "v_started>v_now","ON DUPLICATE KEY UPDATE attempts=attempts",
      "CREATE TRIGGER c05_10_trace_no_update",
      "CREATE TRIGGER c05_10_trace_no_delete",
      "CREATE TRIGGER c05_10_outcome_no_update",
      "CREATE TRIGGER c05_10_outcome_no_delete",
      "v_host_revision<>p_host_revision",
      "v_disposition<>'ADMITTED'",
      "INSERT INTO c05_10_access_outcomes",
  )) and sql.count("SET v_now=SYSDATE(6);")==2,
  "Exact UTC post-lock clock / source-subject CAS and immutable audit required")
 need(all(x in app for x in (
      "ReadServerSocketPeerAsync","ReadServerVerifiedTenantAsync",
      "FingerprintSocketPeer","FingerprintSubject",
      "ledger.AdmitAsync","passwordVerifier.VerifyAsync",
      "ledger.CompleteAsync","PreAuthPublicDisposition.RateLimited",
      "proof.TenantId==host.TenantId")),
      "Pre-Argon2id authenticated tenant/socket HMAC admission required")
 need(all(x in protector for x in (
      "HMACSHA256.HashData(key,buffer)","tenant.Value.TryWriteBytes",
      "address.AsSpan(8,8).Clear()",
      "CryptographicOperations.ZeroMemory(key)",
      "SyntheticPreAuthDigestProtector[redacted]")),
      "Separate tenant/actor/action HMAC scope and key zeroization")
 need(all(x in test for x in (
      "16 concurrent HTTP brute force admissions => exactly 5 accepted and 11 blocked",
      "Repeated 16-way HTTP burst must admit 5/limit 11 without SQL 503",
      "for(int round=1;round<=5;round++)",
      "sql_fault_categories","SafeAggregateDiagnostics",
      "Unknown mailbox has same neutral 202",
      "Rotating unlimited unknown mailboxes cannot exceed source budget",
      "Append-only access trace cannot be UPDATEd",
      "Append-only authentication outcome cannot be modified",
      "Missing real network/verified host/MySQL is fail-closed")),
      "Actual 6x16 HTTP/MySQL tests, PII-safe diagnostics and fail-closed checks required")
 need('PackageReference Include="MySqlConnector" Version="2.6.2"' in csproj,
      "Reviewed DB provider version changed")
 ci=values["ci"]
 need("workflow_dispatch:" not in ci and
      "id-token: write" not in ci and
      "deploy" not in ci.lower(),
      "Institutional CI cannot deploy, dispatch or write GitHub privileges")

def a()->None:
 subprocess.run([sys.executable,"qa/master_source_guard.py","--self-test"],
                cwd=ROOT,check=True)
 subprocess.run([sys.executable,"qa/c05_10_rate_limit_audits.py","A"],
                cwd=ROOT,check=True)
 plan=(ROOT/"docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md").read_text(encoding="utf-8")
 need("C05-10 | Rate limit y trazas de acceso" in plan and
      "No registrar tokens o contraseñas" in plan,
      "Canonical Word-derived C05-10 requirement absent")
 invariants(source())
 print("PASS C05-10 institutional A: canonical Word, C05-09, initial postmerge failure and full remediation traced")

def b()->None:
 data=source()
 invariants(data)
 killed=0
 for kind,markers in GUARDS.items():
  for marker in markers:
   mutated=dict(data)
   mutated[kind]=data[kind].replace(marker,"[REMOVED_C05_10_GUARD]")
   need(mutated[kind]!=data[kind],"Ineffective institutional mutation "+marker)
   try:invariants(mutated)
   except AssertionError:killed+=1
   else:raise AssertionError("Undetected C05-10 institutional abuse/privacy guard deletion: "+marker)
 need(killed>=85,"Insufficient institutional security mutations")
 print(f"PASS C05-10 institutional B: {killed} trace/privacy and rate limit guards rejected")
 print("Static audit B never substitutes for real .NET10 HTTP/MySQL8.4")

def git(*args:str)->str:
 return subprocess.check_output(["git",*args],cwd=ROOT,text=True,
                                stderr=subprocess.DEVNULL).strip()

def c()->None:
 need(os.getenv("GITHUB_ACTIONS")=="true" and
      os.getenv("GITHUB_REPOSITORY")==REPO,
      "Only authenticated CRECISOL CI may attest")
 sha,run=os.getenv("GITHUB_SHA",""),os.getenv("GITHUB_RUN_ID","")
 need(re.fullmatch("[a-f0-9]{40}",sha) is not None and
      re.fullmatch("[0-9]+",run) is not None and
      git("rev-parse","HEAD")==sha,
      "SHA/run mismatch in final attestation")
 for stage in ("A","B","build","throttle","regressions"):
  file=PROOF/stage
  need(file.is_file() and file.read_text(encoding="utf-8").strip()==
       f"PASS {stage} sha={sha} run={run}",
       "Missing current run real HTTP/MySQL or regression evidence "+stage)
 if os.getenv("GITHUB_EVENT_NAME")=="pull_request":
  f=Path(os.getenv("GITHUB_EVENT_PATH",""))
  need(f.is_file(),"Signed GitHub PR event required")
  event=json.loads(f.read_text(encoding="utf-8"))["pull_request"]
  base,head=event["base"],event["head"]
  need(base["ref"]=="dev" and base["sha"]==BASE and
       head["ref"]==BRANCH and
       base["repo"]["full_name"]==REPO and
       head["repo"]["full_name"]==REPO,
       "Unexpected repository/base/head SHA or branch for C05-10 institution")
  need(git("rev-list","--parents","-n","1","HEAD").split()==
       [sha,base["sha"],head["sha"]],
       "Synthetic GitHub PR must have exact immutable base/head")
  changed=git("diff","--name-only",base["sha"],head["sha"]).splitlines()
 else:
  need(os.getenv("GITHUB_EVENT_NAME")=="push" and
       os.getenv("GITHUB_REF")=="refs/heads/dev",
       "Only actual dev POSTMERGE may ratify")
  changed=git("show","--format=","--name-only",sha).splitlines()
 need(set(changed)==ALLOWED and len(changed)==len(ALLOWED),
      "Institutional diff MUST be exactly THREE reviewed files: "+repr(changed))
 invariants(source())
 print(f"PASS C05-10 institutional C: A/B + real MySQL8.4/HTTP 6x16, all C05 regressions, SHA={sha} run={run}")
 print("NO-GO production: no product login/PII, real auth, Redis, Cloudflare or external logging")

if __name__=="__main__":
 try:
  need(len(sys.argv)==2 and sys.argv[1] in ("A","B","C"),
       "Usage: qa/c05_10_closure_gate.py A|B|C")
  {"A":a,"B":b,"C":c}[sys.argv[1]]()
 except (AssertionError,OSError,KeyError,ValueError,subprocess.SubprocessError) as e:
  print("FAIL C05-10 institutional audit: "+str(e),file=sys.stderr)
  sys.exit(1)
