#!/usr/bin/env python3
"""C05-11 integrative A/B/C security acceptance of the synthetic chapter.

A/B static and hostile-mutation probes DO NOT substitute for actual .NET10,
MySQL8.4, Angular22 or Chromium tests. Keep C06 blocked if Chapter 05's
synthetic identity boundary is incomplete or any P0/P1 is found.
"""
from __future__ import annotations
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
from zipfile import ZipFile

ROOT=Path(__file__).resolve().parents[1]
REPO="crecisol-platform/CRECISOL"
BASE="52efec64f43245771af59459f8db3b38685c1703"
BRANCH="feature/c05-11-integral-security-20261114"
WORD_SHA="ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c"
PDF_SHA="0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77"
WORD_BLOB="fe7aaab60b603f1844e18666e2f99c0ea97e86e9"
PDF_BLOB="93c8412b2450e85a882b622e7a012477c61c2359"
FILES={
 "docs/c05/C05_11_ESPECIFICACION.md",
 "docs/c05/C05_11_MATRIZ_RIESGOS.md",
 "qa/c05_11_integral_audits.py",
 ".github/workflows/c05-11-integral.yml",
}
PROOF=ROOT/".c05-11-integral-proof"
ACTS={
 "01":"docs/c05/ACTA_C05_01_CONTRATOS.md",
 "02":"docs/c05/ACTA_CERTIFICACION_ACOTADA_C05_02_2026_11_13.md",
 "03":"docs/c05/ACTA_CERTIFICACION_ACOTADA_C05_03_2026_11_13.md",
 "04":"docs/c05/ACTA_CERTIFICACION_ACOTADA_C05_04_2026_11_13.md",
 "05":"docs/c05/ACTA_CERTIFICACION_ACOTADA_C05_05_2026_11_13.md",
 "06":"docs/c05/ACTA_CERTIFICACION_ACOTADA_C05_06_2026_11_13.md",
 "07":"docs/c05/ACTA_CERTIFICACION_ACOTADA_C05_07_2026_11_13.md",
 "08":"docs/c05/ACTA_CERTIFICACION_ACOTADA_C05_08_2026_11_14.md",
 "09":"docs/c05/ACTA_CERTIFICACION_ACOTADA_C05_09_2026_11_14.md",
 "10":"docs/c05/ACTA_CERTIFICACION_ACOTADA_C05_10_2026_11_14.md",
}
AUDITS={
 "01":"qa/c05_01_identity_audits.py",
 "02":"qa/c05_02_scope_closure_audits.py",
 "03":"qa/c05_03_password_audits.py",
 "04":"qa/c05_04_contextual_login_audits.py",
 "05":"qa/c05_05_session_audits.py",
 "06":"qa/c05_06_recovery_audits.py",
 "07":"qa/c05_07_mailbox_audits.py",
 "08":"qa/c05_08_mfa_integration_audits.py",
 "09":"qa/c05_09_profile_audits.py",
 "10":"qa/c05_10_rate_limit_audits.py",
}
C05_SQL=[
 "c05_02_invitation_atomic_smoke.sh",
 "c05_02_dotnet_mysql_adapter_smoke.sh",
 "c05_02_issuer_scope_mysql_smoke.sh",
 "c05_02_bound_claim_mysql_smoke.sh",
 "c05_02_mailbox_proof_mysql_smoke.sh",
 "c05_02_signed_recipient_mysql_smoke.sh",
 "c05_02_neutral_registration_http_mysql.sh",
 "c05_02_mailbox_bound_mysql_smoke.sh",
 "c05_02_independent_receiver_mysql_smoke.sh",
 "c05_02_proof_outbox_mysql_smoke.sh",
 "c05_02_privacy_retention_mysql.sh",
 "c05_05_session_http_mysql.sh",
 "c05_06_recovery_http_mysql.sh",
 "c05_07_mailbox_http_mysql.sh",
 "c05_08_mfa_http_mysql.sh",
 "c05_09_private_profiles_http_mysql.sh",
 "c05_10_rate_limit_http_mysql.sh",
]
PROJECTS=[
 "Crecisol.AccountIdentity.ContractsTests",
 "Crecisol.InvitationSecurity.ContractsTests",
 "Crecisol.PasswordHash.ContractsTests",
 "Crecisol.ContextualLogin.ContractsTests",
 "Crecisol.MfaRisk.ContractsTests",
 "Crecisol.NeutralRegistration.IntegrationTests",
 "Crecisol.SessionRevocation.MySqlTests",
 "Crecisol.PasswordRecovery.MySqlTests",
 "Crecisol.MailboxApproval.MySqlTests",
 "Crecisol.MfaRisk.MySqlTests",
 "Crecisol.PrivateProfiles.MySqlTests",
 "Crecisol.RateLimitAccess.MySqlTests",
]
# Deliberately sensitive guards in genuine current project code and tests.
# B removes each individual marker in-memory and proves audit rejection.
GUARDS={
 "backend/src/Crecisol.Application/Identity/ThrottledContextualPasswordAttempt.cs":(
  "ReadServerVerifiedTenantAsync","ReadServerSocketPeerAsync",
  "FingerprintSocketPeer","FingerprintSubject","ledger.AdmitAsync",
  "passwordVerifier.VerifyAsync","ledger.CompleteAsync",
  "proof.TenantId==host.TenantId","proof.HostRevision==host.Revision",
  "PreAuthPublicDisposition.Unavailable","PreAuthPublicDisposition.RateLimited",
 ),
 "backend/src/Crecisol.Infrastructure/Identity/SyntheticPreAuthDigestProtector.cs":(
  "HMACSHA256.HashData(key,buffer)",
  "CRECISOL.C05-10.","IsIPv4MappedToIPv6",
  "address.AsSpan(8,8).Clear()","tenant.Value.TryWriteBytes",
  "CryptographicOperations.ZeroMemory(key)",
 ),
 "database/tests/fixtures/c05_10_disposable_rate_limits.sql":(
  "PRIMARY KEY (tenant_id,purpose,dimension,digest)",
  "dimension='SOURCE'","dimension='SUBJECT'",
  "SQL SECURITY DEFINER","FOR SHARE","FOR UPDATE",
  "SET @@session.time_zone='+00:00'",
  "SET v_now=SYSDATE(6)","ON DUPLICATE KEY UPDATE attempts=attempts",
  "v_host_revision<>p_host_revision",
  "CREATE TRIGGER c05_10_trace_no_update",
  "CREATE TRIGGER c05_10_trace_no_delete",
  "CREATE TRIGGER c05_10_outcome_no_update",
  "CREATE TRIGGER c05_10_outcome_no_delete",
 ),
 "backend/tests/Crecisol.RateLimitAccess.MySqlTests/Program.cs":(
  "16 concurrent HTTP brute force admissions => exactly 5 accepted and 11 blocked",
  "for(int round=1;round<=5;round++)",
  "Repeated 16-way HTTP burst must admit 5/limit 11 without SQL 503",
  "Source cooldown resistant to X-Forwarded-For",
  "A locked subject cannot deny B",
  "Unknown mailbox has same neutral 202",
  "Missing real network/verified host/MySQL is fail-closed",
  "Append-only access trace cannot be UPDATEd",
  "No raw mailbox or 127.0.0.1 socket string",
 ),
 "backend/src/Crecisol.Infrastructure/Identity/SyntheticPrivateProfileCipher.cs":(
  "AesGcm(key,16)","RandomNumberGenerator.GetBytes(12)",
  "CryptographicOperations.ZeroMemory(key)","gcm.Decrypt",
 ),
 "backend/src/Crecisol.Application/Identity/PrivateTenantProfileUseCases.cs":(
  "VerifyFreshAsync","proof.TenantId==host.TenantId",
  "proof.HostRevision==host.Revision",
  "EncryptForTenantActor","DecryptForTenantActor",
  "EraseSelfAsync","CryptographicOperations.ZeroMemory",
 ),
 "database/tests/fixtures/c05_09_disposable_private_profiles.sql":(
  "SQL SECURITY DEFINER","FOR UPDATE",
  "CREATE TRIGGER c05_09_audit_no_update",
  "CREATE TRIGGER c05_09_audit_no_delete",
  "FOREIGN KEY (tenant_id,actor_id,photo_id)",
  "pii_ciphertext=NULL,photo_id=NULL",
 ),
 "database/tests/fixtures/c05_08_disposable_mfa.sql":(
  "FOR UPDATE","tenant_id",
 ),
 "database/tests/fixtures/c05_07_disposable_mailbox.sql":(
  "FOR UPDATE","tenant_id",
 ),
 "database/tests/fixtures/c05_06_disposable_recovery.sql":(
  "FOR UPDATE","tenant_id",
 ),
 "database/tests/fixtures/c05_05_disposable_sessions.sql":(
  "FOR UPDATE","tenant_id",
 ),
 "docs/c05/C05_11_ESPECIFICACION.md":(
  "C05-11 NO CERTIFICADA","C06 BLOQUEADO",
  "C05-01","C05-02","C05-03","C05-04","C05-05",
  "C05-06","C05-07","C05-08","C05-09","C05-10",
  "Documento Maestro Único","6885019825",
  "No habilitar C06 sin identidades confiables",
  "COM-T08/T09/T10","NO EJECUTADAS",
  "52efec64f43245771af59459f8db3b38685c1703",
  "SYSDATE(6)","NO-GO productivo",
 ),
 "docs/c05/C05_11_MATRIZ_RIESGOS.md":(
  "R05-01","R05-02","R05-03","R05-04","R05-05",
  "R05-06","R05-07","R05-08","R05-09","R05-10",
  "R05-11","R05-12","R05-13","R05-14","R05-15",
  "R05-16","R05-17","R05-18","R05-19","R05-20",
  "PENDIENTE","NO-GO","C06 BLOQUEADO",
  "43643788104","6885019825",
 ),
 ".github/workflows/c05-11-integral.yml":(
  "permissions:\n  contents: read",
  "persist-credentials: false",
  "dotnet-version: '10.0.x'",
  "node-version: '24.15.0'",
  "qa/c05_11_integral_audits.py A",
  "qa/c05_11_integral_audits.py B",
  "qa/c05_11_integral_audits.py C",
  "c05_10_rate_limit_http_mysql.sh",
  "c05_09_private_profiles_http_mysql.sh",
  "c05_08_mfa_http_mysql.sh",
  "c05_07_mailbox_http_mysql.sh",
  "c05_06_recovery_http_mysql.sh",
  "c05_05_session_http_mysql.sh",
  "c05_02_neutral_registration_http_mysql.sh",
  "c04_11_offensive_sql.sh",
  ".c05-11-integral-proof","GITHUB_SHA","GITHUB_RUN_ID",
 ),
}

def need(ok:bool,message:str)->None:
 if not ok:raise AssertionError("C05-11 NO-GO: "+message)

def values()->dict[str,str]:
 return {p:(ROOT/p).read_text(encoding="utf-8") for p in GUARDS}

def invariants(data:dict[str,str])->None:
 for path,markers in GUARDS.items():
  for marker in markers:
   need(marker in data[path],f"Guard missing: {path} :: {marker}")
 # The public API is deliberately NOT a product identity/session service.
 product=(ROOT/"backend/src/Crecisol.Api/Program.cs").read_text()
 routes=re.findall(r'app\.Map(?:Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"',product)
 need(routes==["/api/v1/public/branding/platform"],
      "C05-11 must not claim or enable a private production auth API")
 need("ThrottledContextualPasswordAttempt" not in product and
      "MySqlDisposablePreAuthRateLimitLedger" not in product,
      "A disposable auth fixture was connected to public production API")
 # CI is code/testing only, not a deployment or live consumer test.
 ci=data[".github/workflows/c05-11-integral.yml"]
 need("workflow_dispatch:" not in ci and "id-token: write" not in ci and
      "deploy" not in ci.lower() and
      "permissions:\n  contents: read" in ci,
      "Unexpected high privileges or product deployment")
 # Full chapter does not authorize a production endpoint or active C06.
 matrix=data["docs/c05/C05_11_MATRIZ_RIESGOS.md"]
 rows=re.findall(r"(?m)^\| R05-(\d\d) \|",matrix)
 need(rows==[f"{i:02d}" for i in range(1,21)],
      "20 distinct R05-01..20 matrix rows required")
 need(matrix.count("| PENDIENTE |")>=20,
      "No provisional PASS without actual integrated testing")
 return None

def master_hash(path:Path)->str:
 h=hashlib.sha256()
 with path.open("rb") as f:
  for chunk in iter(lambda:f.read(1048576),b""):
   h.update(chunk)
 return h.hexdigest()

def audit_a()->None:
 word=ROOT/"docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
 pdf=ROOT/"docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.pdf"
 need(word.is_file() and pdf.is_file(),"Canonical Word and PDF not available")
 need(master_hash(word)==WORD_SHA and master_hash(pdf)==PDF_SHA,
      "Canonical Word/PDF byte-level SHA256 mismatch")
 ref=(ROOT/"docs/MASTER_REFERENCE.md").read_text()
 need(all(x in ref for x in (WORD_SHA,PDF_SHA,WORD_BLOB,PDF_BLOB)),
      "Canonical Word/PDF source register drifted")
 with ZipFile(word) as z:
  need(z.testzip() is None,"DOCX ZIP is corrupted")
  xml=z.read("word/document.xml").decode("utf-8")
 need("identidad" in xml.lower() or "autentic" in xml.lower(),
      "Canonical Word omits fundamental identity/authentication subject")
 plan=(ROOT/"docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md").read_text()
 rows=re.findall(r"(?m)^\| C05-(\d\d) \|",plan)
 need(rows==[f"{i:02d}" for i in range(1,12)],
      "11 microphases must be in canonical derived plan order")
 need(all(x in plan for x in (
  "Auditorías integrales y acta C05",
  "A/B/C de autentificación, abuso y aceptación",
  "No habilitar C06 sin identidades confiables",
  "COM-T08","COM-T09","COM-T10",
  "Puerta de certificación del capítulo")),
      "Master-derived chapter acceptance and commercial NO-GO absent")
 for n,path in ACTS.items():
  need((ROOT/path).is_file(),"Missing certified chapter-05 predecessor act C05-"+n)
  need((ROOT/AUDITS[n]).is_file(),"Missing C05-"+n+" A/B code")
  need(any((ROOT/".github/workflows").glob(f"c05-{n}-*.yml")),
       "Missing C05-"+n+" historical CI evidence")
 for name in PROJECTS:
  path=ROOT/f"backend/tests/{name}/{name}.csproj"
  need(path.is_file(),"Real .NET10 project missing: "+name)
 for name in C05_SQL:
  need((ROOT/"database/tests"/name).is_file(),
       "MySQL8.4 integration test missing: "+name)
 historical=(ROOT/ACTS["10"]).read_text()
 need("43643788104" in historical and
      "8a387d2ebc17a166fb5ed0ae20e116cfbd008276" in historical,
      "Historical C05-10 postmerge 503 and recovery must remain traceable")
 specification=(ROOT/"docs/c05/C05_11_ESPECIFICACION.md").read_text()
 need("6885019825" in specification and "C06 BLOQUEADO" in specification,
      "Precedent #147 final verdict or fail-closed C06 gate absent")
 invariants(values())
 print("PASS C05-11 A: Word/PDF hashes, 11 roadmap rows, ten acts, 12 .NET projects, 17 MySQL tests, and C05-10 historical failure")

def audit_b()->None:
 data=values()
 invariants(data)
 killed=0
 for path,markers in GUARDS.items():
  for marker in markers:
   replacement=dict(data)
   replacement[path]=data[path].replace(marker,"[C05_11_MUTATION_REMOVED_GUARD]")
   need(replacement[path]!=data[path],
        "Non-mutating security probe "+marker)
   try:invariants(replacement)
   except AssertionError:killed+=1
   else:raise AssertionError("Unchecked dangerous edit "+path+" :: "+marker)
 need(killed>=95,"Insufficient hostile HMAC, MFA, identity and PII guard probes")
 for name in C05_SQL:
  body=(ROOT/"database/tests"/name).read_text()
  need("docker" in body and "mysql" in body.lower(),
       "Script missing real Docker/MySQL execution: "+name)
  need("trap " in body and "cleanup" in body,
       "Disposable MySQL fixture missing cleanup trap: "+name)
 # Chain of responsibility: only backend socket peer and verified host,
 # not browser-supplied tenant/IP, may authorize pre-auth admission.
 print(f"PASS C05-11 B: {killed} negative guard removals rejected, 17 disposable MySQL test runners present, no private product API")
 print("Host, credential, session, MFA, MySQL and cross-tenant negative cases still MUST run for real")

def git(*args:str)->str:
 return subprocess.check_output(["git",*args],cwd=ROOT,
      stderr=subprocess.DEVNULL,text=True).strip()

def audit_c()->None:
 need(os.getenv("GITHUB_ACTIONS")=="true" and
      os.getenv("GITHUB_REPOSITORY")==REPO,
      "Only genuine CRECISOL GitHub Actions count as integral proof")
 sha,run=os.getenv("GITHUB_SHA",""),os.getenv("GITHUB_RUN_ID","")
 need(re.fullmatch(r"[0-9a-f]{40}",sha) is not None and
      re.fullmatch(r"[0-9]+",run) is not None and
      git("rev-parse","HEAD")==sha,
      "Untrusted SHA or workflow ID")
 for stage in ("A","B","dotnet","mysql","frontend","chromium"):
  f=PROOF/stage
  need(f.is_file() and f.read_text(encoding="utf-8").strip()==
       f"PASS {stage} sha={sha} run={run}",
       "Missing real same-SHA/same-run full chapter evidence "+stage)
 if os.getenv("GITHUB_EVENT_NAME")=="pull_request":
  p=Path(os.getenv("GITHUB_EVENT_PATH",""))
  need(p.is_file(),"Signed PR event file required")
  event=json.loads(p.read_text())["pull_request"]
  base,head=event["base"],event["head"]
  need(base["ref"]=="dev" and base["sha"]==BASE and
       head["ref"]==BRANCH and base["repo"]["full_name"]==REPO and
       head["repo"]["full_name"]==REPO,
       "Chapter C05-11 PR base/head changed unexpectedly")
  need(git("rev-list","--parents","-n","1","HEAD").split()==
       [sha,base["sha"],head["sha"]],
       "PR merge SHA ancestry mismatch")
  changed=git("diff","--name-only",base["sha"],head["sha"]).splitlines()
 else:
  need(os.getenv("GITHUB_EVENT_NAME")=="push" and
       os.getenv("GITHUB_REF")=="refs/heads/dev",
       "Only dev POSTMERGE may count")
  ancestry=git("rev-list","--parents","-n","1","HEAD").split()
  need(ancestry==[sha,BASE],
       "Chapter technical squash must be a direct child of C05-10")
  changed=git("show","--format=","--name-only",sha).splitlines()
 need(len(changed)==len(FILES) and set(changed)==FILES,
      "Only four C05-11 technical QA/doc/workflow files may change: "+repr(changed))
 invariants(values())
 print(f"PASS C05-11 C: A/B + real .NET10 Angular22 Chromium MySQL8.4 on sha={sha} run={run}; no production identity, C06 stays blocked")

if __name__=="__main__":
 try:
  need(len(sys.argv)==2 and sys.argv[1] in ("A","B","C"),
       "Usage: qa/c05_11_integral_audits.py A|B|C")
  {"A":audit_a,"B":audit_b,"C":audit_c}[sys.argv[1]]()
 except (AssertionError,OSError,KeyError,ValueError,subprocess.SubprocessError) as e:
  print("FAIL C05-11 integral A/B/C: "+str(e),file=sys.stderr)
  sys.exit(1)
