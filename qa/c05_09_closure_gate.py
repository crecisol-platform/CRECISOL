#!/usr/bin/env python3
"""C05-09 institutional A/B/C: SHA-locked REAL HTTP/MySQL8.4, synthetic-only."""
from __future__ import annotations
import json
import os
from pathlib import Path
import re
import subprocess
import sys
from c05_01_preparation_audits import ROOT

REPO="crecisol-platform/CRECISOL"
BASE="558c54cc9b1aee20a2fac6eb0c637b4f5700a52e"
BRANCH="feature/c05-09-private-profiles-institutional-20261114"
FILES={
 "act":"docs/c05/ACTA_CERTIFICACION_ACOTADA_C05_09_2026_11_14.md",
 "qa":"qa/c05_09_closure_gate.py",
 "ci":".github/workflows/c05-09-certification.yml",
}
ALLOWED=set(FILES.values())
PROOF=ROOT/".c05-09-institutional-proof"
GUARDS={
 "act":(
  "Acta institucional CONDICIONADA","ACTA NO EFICAZ",
  "C05-09 NO CERTIFICADA","C05-10 BLOQUEADA",
  "crecisol-platform/CRECISOL","Documento Maestro Único",
  "MASTER_REFERENCE.md","43364292640",
  "ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c",
  "0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77",
  "6883945140","ba9ad6a706cfb1d75e183a83fc023935cdbc89a8",
  "Perfiles y fotos seguras","Datos personales y medios aislados",
  "No lectura cruzada de foto/PII","ADR-015","Cloudflare R2",
  "aeb368e9c72992b90cf26386ec7821c934daf7c7",BASE,
  "EXACTAMENTE 11 archivos","AES-256-GCM","Argon2id C05-04 fresco",
  "MySqlConnector@2.6.2","12 bytes","tenant+actor",
  "SQL SECURITY DEFINER","FOR UPDATE","EXECUTE-only",
  "PENDING/REVOKED","16 solicitudes concurrentes",
  "221 comprobaciones","135 mutaciones",
  "43640233079","43640233071","43640233067",
  "43640697333","43640697309","43640697359",
  "43640697323","43640697311","43640697347",
  "NO VERIFICADAS TODAS AL REDACTAR","43640129673","43640206786",
  "C05-01","C05-02","C05-03","C05-04",
  "C05-05","C05-06","C05-07","C05-08",
  "NO-GO productivo","ACTA SERÁ EFICAZ SOLO SI",
  "POSTMERGE técnico","PREMERGE documental","POSTMERGE documental",
  "EXACTAMENTE tres archivos","Rollback controlado","9/11",
  "C06 bloqueado","solqaryn/Solqaryn","127.0.0.1","c05_05_disposable",
 ),
 "ci":(
  "permissions:\n  contents: read",
  "persist-credentials: false","fetch-depth: 2",
  "dotnet-version: '10.0.x'",
  "qa/c05_09_closure_gate.py A",
  "qa/c05_09_closure_gate.py B",
  "qa/c05_09_closure_gate.py C",
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
  ".c05-09-institutional-proof","GITHUB_SHA","GITHUB_RUN_ID",
 ),
}

def need(ok:bool,why:str)->None:
 if not ok:raise AssertionError(why)
def source()->dict[str,str]:
 return {k:(ROOT/v).read_text(encoding="utf-8") for k,v in FILES.items() if k!="qa"}

def invariants(data:dict[str,str])->None:
 for kind,markers in GUARDS.items():
  for marker in markers:
   need(marker in data[kind],"Missing C05-09 guard "+kind+": "+marker)
 product=(ROOT/"backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
 routes=re.findall(r'app\.Map(?:Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"',product)
 need(routes==["/api/v1/public/branding/platform"] and
      "PrivateTenantProfileUseCases" not in product and
      "MySqlDisposablePrivateProfileLedger" not in product,
      "Forbidden private profile routes in product API")
 app=(ROOT/"backend/src/Crecisol.Application/Identity/PrivateTenantProfileUseCases.cs").read_text()
 cipher=(ROOT/"backend/src/Crecisol.Infrastructure/Identity/SyntheticPrivateProfileCipher.cs").read_text()
 sql=(ROOT/"database/tests/fixtures/c05_09_disposable_private_profiles.sql").read_text()
 tests=(ROOT/"backend/tests/Crecisol.PrivateProfiles.MySqlTests/Program.cs").read_text()
 infra=(ROOT/"backend/src/Crecisol.Infrastructure/Crecisol.Infrastructure.csproj").read_text()
 need(all(x in app for x in (
  "VerifyFreshAsync","proof.TenantId==host.TenantId",
  "proof.HostRevision==host.Revision","EncryptForTenantActor",
  "DecryptForTenantActor","EraseSelfAsync",
  "CryptographicOperations.ZeroMemory")),
  "C05-04 fresh Argon2id and protected PII use case missing")
 need(all(x in cipher for x in (
  "AesGcm(key,16)","RandomNumberGenerator.GetBytes(12)",
  "tenant.Value.TryWriteBytes","actor.Value.TryWriteBytes",
  "CryptographicOperations.ZeroMemory(key)","gcm.Decrypt")),
  "AES-256-GCM actor+tenant authenticated envelope required")
 need(all(x in sql for x in (
  "CREATE TRIGGER c05_09_audit_no_update",
  "CREATE TRIGGER c05_09_audit_no_delete",
  "FOREIGN KEY (tenant_id,actor_id,photo_id)",
  "SQL SECURITY DEFINER","FOR UPDATE","v_realm<>'TENANT'",
  "v_photo_state<>'VERIFIED'","v_scan<>'CLEAN'",
  "v_link<>p_link","v_h<>p_host",
  "pii_ciphertext=NULL,photo_id=NULL",
  "profile_revision=profile_revision+1")),
  "SQL prevents cross-tenant photo/PII, stale CAS, audit edits")
 need(all(x in tests for x in (
  "16 concurrent HTTP profile CAS updates: exactly one winner",
  "Tenant B media ID cannot attach",
  "Same tenant different actor media ID cannot attach",
  "Reactivated membership cannot resurrect",
  "PLATFORM realm denied","Erased profile no longer returns PII",
  "Wrong Argon2id password cannot create or read PII",
  "Append-only profile audit DELETE prohibited")),
  "Real HTTP/MySQL negative cases missing")
 need('PackageReference Include="MySqlConnector" Version="2.6.2"' in infra,
      "Unreviewed MySqlConnector version")
 ci=data["ci"]
 need("workflow_dispatch:" not in ci and "id-token: write" not in ci and
      "deploy" not in ci.lower() and
      "permissions:\n  contents: read" in ci,
      "No privileged/deployment CI permitted")

def a()->None:
 subprocess.run([sys.executable,"qa/master_source_guard.py","--self-test"],cwd=ROOT,check=True)
 subprocess.run([sys.executable,"qa/c05_09_profile_audits.py","A"],cwd=ROOT,check=True)
 plan=(ROOT/"docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md").read_text()
 adr=(ROOT/"docs/ADR-015_CLOUDFLARE_ALMACENAMIENTO_MULTIMEDIA.md").read_text()
 need("C05-09 | Perfiles y fotos seguras" in plan and
      "No lectura cruzada de foto/PII" in plan and
      "MySQL almacenará la referencia estable" in adr and
      "No persistir URL presignada temporal" in adr,
      "Master-derived C05-09 + ADR-015 obligations missing")
 invariants(source())
 print("PASS C05-09 institutional A: canonical master, C05-08, ADR-015 and traceability")

def b()->None:
 data=source()
 invariants(data)
 killed=0
 for kind,markers in GUARDS.items():
  for marker in markers:
   mutant=dict(data)
   mutant[kind]=data[kind].replace(marker,"[C05_09_REMOVED_GUARD]")
   need(mutant[kind]!=data[kind],"Ineffective mutation "+marker)
   try:invariants(mutant)
   except AssertionError:killed+=1
   else:raise AssertionError("Undetected security guard removal "+marker)
 need(killed>=75,"Insufficient C05-09 hostile static mutations")
 print(f"PASS C05-09 institutional B: {killed} hostile documentary/security guards rejected")
 print("Static B never substitutes for actual .NET10/MySQL HTTP negatives")

def git(*args:str)->str:
 return subprocess.check_output(["git",*args],cwd=ROOT,text=True,
   stderr=subprocess.DEVNULL).strip()

def c()->None:
 need(os.getenv("GITHUB_ACTIONS")=="true" and os.getenv("GITHUB_REPOSITORY")==REPO,
      "Exact CRECISOL GitHub Actions required")
 sha,run=os.getenv("GITHUB_SHA",""),os.getenv("GITHUB_RUN_ID","")
 need(re.fullmatch("[0-9a-f]{40}",sha) is not None and
      re.fullmatch("[0-9]+",run) is not None and
      git("rev-parse","HEAD")==sha,"Immutable SHA and run")
 for stage in ("A","B","build","profiles","regressions"):
  file=PROOF/stage
  need(file.is_file() and file.read_text(encoding="utf-8").strip()==
       f"PASS {stage} sha={sha} run={run}",
       "Missing SAME-SHA/SAME-RUN actual test proof "+stage)
 if os.getenv("GITHUB_EVENT_NAME")=="pull_request":
  f=Path(os.getenv("GITHUB_EVENT_PATH",""))
  need(f.is_file(),"Signed PR event required")
  pr=json.loads(f.read_text(encoding="utf-8"))["pull_request"]
  base,head=pr["base"],pr["head"]
  need(base["ref"]=="dev" and base["sha"]==BASE and
       head["ref"]==BRANCH and base["repo"]["full_name"]==REPO and
       head["repo"]["full_name"]==REPO,"Wrong branch/owner/base SHA")
  need(git("rev-list","--parents","-n","1","HEAD").split()==
       [sha,base["sha"],head["sha"]],"PR synthetic ancestry mismatched")
  changed=git("diff","--name-only",base["sha"],head["sha"]).splitlines()
 else:
  need(os.getenv("GITHUB_EVENT_NAME")=="push" and
       os.getenv("GITHUB_REF")=="refs/heads/dev",
       "Only dev POSTMERGE eligible")
  changed=git("show","--format=","--name-only",sha).splitlines()
 need(set(changed)==ALLOWED and len(changed)==len(ALLOWED),
      "Institutional diff EXACTLY three reviewed files required: "+repr(changed))
 invariants(source())
 print(f"PASS C05-09 institutional C: A/B real .NET10 HTTP/MySQL8.4 plus regressions SHA={sha} run={run}")
 print("TEST-ONLY: no product Cloudflare, uploads, KMS, private authentication")

if __name__=="__main__":
 try:
  need(len(sys.argv)==2 and sys.argv[1] in ("A","B","C"),
       "Use C05-09 institutional A|B|C")
  {"A":a,"B":b,"C":c}[sys.argv[1]]()
 except (AssertionError,OSError,KeyError,ValueError,subprocess.SubprocessError) as exc:
  print("FAIL C05-09 institutional gate: "+str(exc),file=sys.stderr)
  sys.exit(1)
