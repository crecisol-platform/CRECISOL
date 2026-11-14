#!/usr/bin/env python3
"""CRECISOL C05-10 institutional A/B/C: strict 3-file SHA-locked ratification.

Never promote historic failed #145 POSTMERGE as success. Require real same-run
.NET10 loopback HTTP / disposable MySQL8.4 6x16 5/11/0 and regressions.
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
BASE="8a387d2ebc17a166fb5ed0ae20e116cfbd008276"
PRIOR="1b0a3ec6a62ef38582b0ef514f53cb62965b15cd"
BRANCH="feature/c05-10-rate-limit-institutional-final-20261114"
FILES={
 "act":"docs/c05/ACTA_CERTIFICACION_ACOTADA_C05_10_2026_11_14.md",
 "qa":"qa/c05_10_closure_gate.py",
 "ci":".github/workflows/c05-10-certification.yml",
}
ALLOWED=set(FILES.values())
PROOF=ROOT/".c05-10-institutional-proof"
GUARDS={
 "act":(
  "Acta institucional CONDICIONADA","ACTA NO EFICAZ",
  "C05-10 NO CERTIFICADA","C05-11 BLOQUEADA",
  "crecisol-platform/CRECISOL","1450414592",
  "Documento Maestro Único","MASTER_REFERENCE.md",
  "ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c",
  "0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77",
  "No registrar tokens o contraseñas",
  "Pruebas contra brute-force y filtrado de logs",
  "6884289441","6b7e0d39785bcad00cc059954eaef510f10d84fa",
  "PR original #145","9fc11e1c779c465975856a40ccb7a84bf7653fe0",
  PRIOR, "EXACTAMENTE 11 archivos",
  "43643373571","43643373529","43643373711",
  "43643788104","5 admitidos, 4 limitados, 7 no disponibles",
  "43643788149","43643788138","43643788151",
  "43643788122","43643788137",
  "PR correctivo #146","574150fba78d61fb168d71fa87e9933ed4e160ab",
  BASE,"exactamente cuatro archivos",
  "43644316467","43644316394","43644316391",
  "211 comprobaciones","174 mutaciones",
  "6 rondas","5/11/0","SYSDATE(6)","UTC_TIMESTAMP(6)",
  "43645885180","43645885132","43645885139",
  "POSTMERGE ORIGINAL #145: FALLO REAL HISTÓRICO",
  "SQL","Argon2id","HMAC-SHA256","FOR UPDATE",
  "SOURCE","SUBJECT","tenant","X-Forwarded-For",
  "202/429","traces","outcomes","EXECUTE-only",
  "C05-01","C05-02","C05-03","C05-04",
  "C05-05","C05-06","C05-07","C05-08","C05-09",
  "2/2 CI PREMERGE","2/2 CI POSTMERGE",
  "exactamente tres archivos","NO-GO productivo",
  "main","solqaryn/Solqaryn","Rollback",
  "C05 completo permanece abierto","C06 bloqueado",
 ),
 "ci":(
  "permissions:\n  contents: read",
  "persist-credentials: false","fetch-depth: 2",
  "dotnet-version: '10.0.x'",
  "qa/c05_10_closure_gate.py A",
  "qa/c05_10_closure_gate.py B",
  "qa/c05_10_closure_gate.py C",
  "Crecisol.RateLimitAccess.MySqlTests.csproj",
  "database/tests/c05_10_rate_limit_http_mysql.sh",
  "Crecisol.PrivateProfiles.MySqlTests.csproj",
  "database/tests/c05_09_private_profiles_http_mysql.sh",
  "Crecisol.MfaRisk.ContractsTests.csproj",
  "database/tests/c05_08_mfa_http_mysql.sh",
  "database/tests/c05_07_mailbox_http_mysql.sh",
  "database/tests/c05_06_recovery_http_mysql.sh",
  "database/tests/c05_05_session_http_mysql.sh",
  "Crecisol.AccountIdentity.ContractsTests.csproj",
  "Crecisol.InvitationSecurity.ContractsTests.csproj",
  "Crecisol.PasswordHash.ContractsTests.csproj",
  "Crecisol.ContextualLogin.ContractsTests.csproj",
  ".c05-10-institutional-proof","GITHUB_SHA","GITHUB_RUN_ID",
 ),
}
def need(ok:bool, why:str)->None:
 if not ok: raise AssertionError(why)
def source()->dict[str,str]:
 return {k:(ROOT/path).read_text(encoding="utf-8")
         for k,path in FILES.items() if k!="qa"}
def invariants(data:dict[str,str])->None:
 for kind,markers in GUARDS.items():
  for marker in markers:
   need(marker in data[kind],"Missing C05-10 institutional guard "+
        kind+": "+marker)
 product=(ROOT/"backend/src/Crecisol.Api/Program.cs").read_text()
 routes=re.findall(r'app\.Map(?:Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"',
                   product)
 need(routes==["/api/v1/public/branding/platform"] and
      "ThrottledContextualPasswordAttempt" not in product and
      "MySqlDisposablePreAuthRateLimitLedger" not in product,
      "Product API must remain public-only: no synthetic login or private logging")
 app=(ROOT/"backend/src/Crecisol.Application/Identity/ThrottledContextualPasswordAttempt.cs").read_text()
 hmac=(ROOT/"backend/src/Crecisol.Infrastructure/Identity/SyntheticPreAuthDigestProtector.cs").read_text()
 sql=(ROOT/"database/tests/fixtures/c05_10_disposable_rate_limits.sql").read_text()
 tests=(ROOT/"backend/tests/Crecisol.RateLimitAccess.MySqlTests/Program.cs").read_text()
 schema=(ROOT/"backend/src/Crecisol.Infrastructure/Crecisol.Infrastructure.csproj").read_text()
 need(all(m in app for m in (
   "ReadServerVerifiedTenantAsync","ReadServerSocketPeerAsync",
   "ledger.AdmitAsync","passwordVerifier.VerifyAsync","ledger.CompleteAsync",
   "PreAuthPublicDisposition.RateLimited",
   "PreAuthPublicDisposition.Unavailable","proof.TenantId==host.TenantId",
   "proof.HostRevision==host.Revision")),
      "Missing atomic pre-Argon2id admission and correct tenant proof binding")
 need(all(m in hmac for m in (
   "HMACSHA256.HashData(key,buffer)","FingerprintSubject",
   "FingerprintSocketPeer","address.AsSpan(8,8).Clear()",
   "CryptographicOperations.ZeroMemory(key)","tenant.Value.TryWriteBytes")),
      "Cross-tenant HMAC/source isolation or zeroing compromised")
 need(all(m in sql for m in (
   "CREATE TABLE c05_10_policies","CREATE TABLE c05_10_buckets",
   "CREATE TABLE c05_10_access_traces","CREATE TABLE c05_10_access_outcomes",
   "host_revision","source_limit","subject_limit",
   "dimension='SOURCE'","dimension='SUBJECT'",
   "FOR SHARE","FOR UPDATE","SET @@session.time_zone='+00:00'",
   "SET v_now=SYSDATE(6)",
   "ON DUPLICATE KEY UPDATE attempts=attempts",
   "INSERT INTO c05_10_access_traces",
   "INSERT INTO c05_10_access_outcomes",
   "CREATE TRIGGER c05_10_trace_no_update",
   "CREATE TRIGGER c05_10_trace_no_delete",
   "CREATE TRIGGER c05_10_outcome_no_update",
   "CREATE TRIGGER c05_10_outcome_no_delete",
   "v_disposition<>'ADMITTED'")),
      "DB must enforce UTC clock after locks, strict policy/CAS and immutable HMAC traces")
 need(all(m in tests for m in (
   "6", "for(int round=1;round<=5;round++)",
   "Repeated 16-way HTTP burst must admit 5/limit 11 without SQL 503",
   "16 concurrent HTTP brute force admissions => exactly 5 accepted and 11 blocked",
   "sql_fault_categories","FixtureRateLimitDiagnosticLedger",
   "No raw mailbox or 127.0.0.1 socket string",
   "A locked subject cannot deny B","Append-only access trace cannot be UPDATEd",
   "Append-only authentication outcome cannot be modified",
   "Missing real network/verified host/MySQL is fail-closed")),
      "Not testing five extra concurrency bursts, isolation or private append-only traces")
 need('PackageReference Include="MySqlConnector" Version="2.6.2"' in schema,
      "Unreviewed MySqlConnector dependency")
 ci=data["ci"]
 need("workflow_dispatch:" not in ci and "id-token: write" not in ci and
      "deploy" not in ci.lower(),
      "Institutional CI must have read-only permissions without deploy/test promotion")

def a()->None:
 subprocess.run([sys.executable,"qa/master_source_guard.py","--self-test"],
                cwd=ROOT,check=True)
 subprocess.run([sys.executable,"qa/c05_10_rate_limit_audits.py","A"],
                cwd=ROOT,check=True)
 plan=(ROOT/"docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md").read_text()
 need("C05-10 | Rate limit y trazas de acceso" in plan and
      "No registrar tokens o contraseñas" in plan,
      "Master-derived acceptance criterion absent or altered")
 invariants(source())
 print("PASS C05-10 institutional A: canonical master, C05-09, #145 failure, #146 corrected scope")

def b()->None:
 data=source();invariants(data)
 killed=0
 for kind,markers in GUARDS.items():
  for marker in markers:
   mutant=dict(data)
   mutant[kind]=data[kind].replace(marker,"[C05_10_REMOVED_GUARD]")
   need(mutant[kind]!=data[kind],"Ineffective hostile mutation: "+marker)
   try:invariants(mutant)
   except AssertionError:killed+=1
   else:raise AssertionError("Undetected documentary/HMAC guard removal: "+marker)
 need(killed>=90,"Insufficient C05-10 institutional hostile mutations")
 print(f"PASS C05-10 institutional B: {killed} adverse document/CI guards rejected")
 print("Static mutations alone never certify real HTTP and MySQL tests")

def git(*args:str)->str:
 return subprocess.check_output(["git",*args],cwd=ROOT,text=True,
     stderr=subprocess.DEVNULL).strip()

def c()->None:
 need(os.getenv("GITHUB_ACTIONS")=="true" and
      os.getenv("GITHUB_REPOSITORY")==REPO,
      "Only genuine CRECISOL GitHub workflow qualifies")
 sha,run=os.getenv("GITHUB_SHA",""),os.getenv("GITHUB_RUN_ID","")
 need(re.fullmatch(r"[0-9a-f]{40}",sha) is not None and
      re.fullmatch(r"[0-9]+",run) is not None and
      git("rev-parse","HEAD")==sha,
      "Exact SHA and real run ID required")
 for stage in ("A","B","build","throttle","regressions"):
  p=PROOF/stage
  need(p.is_file() and p.read_text().strip()==
       f"PASS {stage} sha={sha} run={run}",
       "Missing same-SHA/same-run genuine C05-10 proof: "+stage)
 if os.getenv("GITHUB_EVENT_NAME")=="pull_request":
  p=Path(os.getenv("GITHUB_EVENT_PATH",""))
  need(p.is_file(),"Signed pull_request event unavailable")
  event=json.loads(p.read_text())["pull_request"]
  base,head=event["base"],event["head"]
  need(base["ref"]=="dev" and base["sha"]==BASE and
       head["ref"]==BRANCH and base["repo"]["full_name"]==REPO and
       head["repo"]["full_name"]==REPO,
       "Wrong institutional PR base/head/repository")
  need(git("rev-list","--parents","-n","1","HEAD").split()==
       [sha,base["sha"],head["sha"]],
       "PR merge ancestry is not from certified #146 squash")
  changed=git("diff","--name-only",base["sha"],head["sha"]).splitlines()
 else:
  need(os.getenv("GITHUB_EVENT_NAME")=="push" and
       os.getenv("GITHUB_REF")=="refs/heads/dev",
       "Only dev institutional postmerge is eligible")
  parents=git("rev-list","--parents","-n","1","HEAD").split()
  need(parents==[sha,BASE],
       "Institutional POSTMERGE MUST have correct remediated technical parent")
  changed=git("show","--format=","--name-only",sha).splitlines()
 need(len(changed)==len(ALLOWED) and set(changed)==ALLOWED,
      "Institutional commit diff must be EXACTLY three documents: "+repr(changed))
 invariants(source())
 print(f"PASS C05-10 institutional C: A/B, .NET10 HTTP/MySQL8.4 six 5/11/0 bursts, C05-01..09 SHA={sha} run={run}")
 print("NO-GO for productive auth, logs, proxy, Cloudflare, KMS, WAF, VPS, payments")

if __name__=="__main__":
 try:
  need(len(sys.argv)==2 and sys.argv[1] in ("A","B","C"),
       "C05-10 institutional requires A|B|C")
  {"A":a,"B":b,"C":c}[sys.argv[1]]()
 except (AssertionError,OSError,KeyError,ValueError,subprocess.SubprocessError) as exc:
  print("FAIL C05-10 institutional gate: "+str(exc),file=sys.stderr)
  sys.exit(1)
