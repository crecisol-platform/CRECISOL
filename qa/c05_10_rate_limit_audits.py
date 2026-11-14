#!/usr/bin/env python3
"""C05-10 A/B/C: atomic anti-bruteforce & private access trace, TEST ONLY.
Static guard mutations do not replace .NET10 HTTP/MySQL8.4 offensive tests.
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
BASE="6b7e0d39785bcad00cc059954eaef510f10d84fa"
BRANCH="feature/c05-10-rate-limit-access-trace-20261114"
REMEDIATION_BRANCH="feature/c05-10-concurrency-hardening-20261114"
REMEDIATION_BASE="1b0a3ec6a62ef38582b0ef514f53cb62965b15cd"
FILES={
 "docs/c05/C05_10_ESPECIFICACION.md",
 "backend/src/Crecisol.Domain/Identity/PreAuthRateLimitModels.cs",
 "backend/src/Crecisol.Application/Identity/ThrottledContextualPasswordAttempt.cs",
 "backend/src/Crecisol.Infrastructure/Identity/SyntheticPreAuthDigestProtector.cs",
 "backend/src/Crecisol.Infrastructure/Identity/MySqlDisposablePreAuthRateLimitLedger.cs",
 "database/tests/fixtures/c05_10_disposable_rate_limits.sql",
 "database/tests/c05_10_rate_limit_http_mysql.sh",
 "backend/tests/Crecisol.RateLimitAccess.MySqlTests/Crecisol.RateLimitAccess.MySqlTests.csproj",
 "backend/tests/Crecisol.RateLimitAccess.MySqlTests/Program.cs",
 "qa/c05_10_rate_limit_audits.py",
 ".github/workflows/c05-10-rate-limit.yml",
}
REMEDIATION_FILES={
 "backend/tests/Crecisol.RateLimitAccess.MySqlTests/Program.cs",
 "database/tests/fixtures/c05_10_disposable_rate_limits.sql",
 "docs/c05/C05_10_ESPECIFICACION.md",
 "qa/c05_10_rate_limit_audits.py",
}
PROOF=ROOT/".c05-10-rate-limit-proof"
GUARDS={
 "docs/c05/C05_10_ESPECIFICACION.md":(
  "C05-10","NO CERTIFICADA","C05-11 BLOQUEADA",
  "Documento Maestro Único","MASTER_REFERENCE.md","C05-09",
  "6884289441","6b7e0d39785bcad00cc059954eaef510f10d84fa",
  "Rate limit y trazas de acceso",
  "Pruebas contra brute-force y filtrado de logs",
  "No registrar tokens o contraseñas",
  "tenant_id","SOURCE","SUBJECT","HMAC-SHA256",
  "FOR UPDATE","MySQL8.4","Argon2id","cooldown_seconds",
  "16 intentos concurrentes 5 admitidos / 11 frenados",
  "Política administrable","NO-GO","C05-11","C06",
  "main","solqaryn/Solqaryn","Rollback",
  "Carrera de reloj durante locks InnoDB",
  "43643788104","UTC_TIMESTAMP(6)","SYSDATE(6)",
  "cinco rondas adicionales de 16 HTTP","C05-10 PENDIENTE",
 ),
 "backend/src/Crecisol.Domain/Identity/PreAuthRateLimitModels.cs":(
  "PreAuthRateLimitPurpose","PasswordLogin",
  "PasswordRecovery","MfaChallenge","PreAuthAdmissionStatus",
  "PreAuthPublicDisposition","PreAuthPrivateOutcome",
  "PreAuthKeyedDigest[redacted]","FromTrustedProtector",
  "IndexOfAnyExcept","CopyForTrustedLedger",
 ),
 "backend/src/Crecisol.Application/Identity/ThrottledContextualPasswordAttempt.cs":(
  "ITrustedPreAuthTenantHostReader","ITrustedPreAuthNetworkReader",
  "ITrustedPreAuthDigestProtector","ITrustedThrottledPasswordVerifier",
  "ITrustedPreAuthRateLimitLedger","ReadServerVerifiedTenantAsync",
  "ReadServerSocketPeerAsync","FingerprintSocketPeer","FingerprintSubject",
  "ledger.AdmitAsync","passwordVerifier.VerifyAsync","ledger.CompleteAsync",
  "ContextualLoginPreparationStatus.Ready",
  "proof.TenantId==host.TenantId","proof.HostRevision==host.Revision",
  "PreAuthPublicDisposition.RateLimited",
  "PreAuthPublicDisposition.Unavailable",
  "ThrottledContextualPasswordAttempt[redacted]",
 ),
 "backend/src/Crecisol.Infrastructure/Identity/SyntheticPreAuthDigestProtector.cs":(
  "HMACSHA256.HashData(key,buffer)","CRECISOL.C05-10.",
  "FingerprintSubject","FingerprintSocketPeer",
  "IsIPv4MappedToIPv6","MapToIPv4()",
  "address.AsSpan(8,8).Clear()","tenant.Value.TryWriteBytes",
  "buffer[label.Length+17]=(byte)purpose",
  "CryptographicOperations.ZeroMemory(key)",
  "CryptographicOperations.ZeroMemory(buffer)",
  "SyntheticPreAuthDigestProtector[redacted]",
 ),
 "backend/src/Crecisol.Infrastructure/Identity/MySqlDisposablePreAuthRateLimitLedger.cs":(
  "b.Server!=\"127.0.0.1\"",
  "b.Database!=\"c05_10_disposable\"",
  "b.AllowLoadLocalInfile","Pooling=false",
  "CommandType=CommandType.StoredProcedure",
  "c05_10_admit_once","c05_10_complete_once",
  "p_host_revision","MySqlDbType.UInt64",
  "CryptographicOperations.ZeroMemory(sourceBytes)",
  "CryptographicOperations.ZeroMemory(subjectBytes)",
  "MySqlDisposablePreAuthRateLimitLedger[redacted]",
 ),
 "database/tests/fixtures/c05_10_disposable_rate_limits.sql":(
  "disposable MySQL8.4",
  "c05_10_policies","c05_10_buckets",
  "c05_10_access_traces","c05_10_access_outcomes",
  "subject_limit","source_limit","window_seconds",
  "cooldown_seconds","enabled","host_revision",
  "revision BIGINT UNSIGNED",
  "PRIMARY KEY (tenant_id,purpose,dimension,digest)",
  "SQL SECURITY DEFINER","FOR SHARE","FOR UPDATE",
  "dimension='SOURCE'","dimension='SUBJECT'",
  "IF v_decision='ADMITTED' THEN",
  "SET v_write_log=FALSE",
  "v_host_revision<>p_host_revision",
  "SELECT 'DENIED' AS result",
  "v_locked>v_now",
  "DATE_SUB(v_now,INTERVAL v_window SECOND)",
  "SET @@session.time_zone='+00:00'",
  "SET v_now=SYSDATE(6)",
  "DATE_ADD(v_now,INTERVAL v_cooldown SECOND)",
  "INSERT INTO c05_10_access_traces",
  "INSERT INTO c05_10_access_outcomes",
  "CREATE TRIGGER c05_10_trace_no_update",
  "CREATE TRIGGER c05_10_trace_no_delete",
  "CREATE TRIGGER c05_10_outcome_no_update",
  "CREATE TRIGGER c05_10_outcome_no_delete",
  "v_disposition<>'ADMITTED'",
  "COMMIT","ROLLBACK",
 ),
 "backend/tests/Crecisol.RateLimitAccess.MySqlTests/Program.cs":(
  "Argon2idPasswordHasher","PrepareContextualPasswordLogin",
  "FixtureDummyVerifier","http://127.0.0.1:0",
  "X-Forwarded-For","X-Tenant-Id","fakeAuthorization",
  "CacheControl=\"no-store\"","UseProxy=false",
  "16 concurrent HTTP brute force admissions => exactly 5 accepted and 11 blocked",
  "Rotating unlimited unknown mailboxes cannot exceed source budget",
  "Source cooldown resistant to X-Forwarded-For",
  "A locked subject cannot deny B",
  "16 concurrent attempts to finish one trace",
  "Unknown mailbox has same neutral 202",
  "Disabled or missing policy denies BEFORE password verifier",
  "Stale host revision cannot re-use old limiter policy",
  "Missing real network/verified host/MySQL is fail-closed",
  "All five admitted synthetic unknown attempts did actual C05-04 dummy Argon2id",
  "No raw mailbox or 127.0.0.1 socket string",
  "Append-only access trace cannot be UPDATEd",
  "Append-only authentication outcome cannot be modified",
  "NO-GO product",
  "Repeated 16-way HTTP burst must admit 5/limit 11 without SQL 503",
  "FixtureRateLimitDiagnosticLedger","SafeAggregateDiagnostics",
 ),
 "database/tests/c05_10_rate_limit_http_mysql.sh":(
  "mysql:8.4@sha256:","127.0.0.1::3306",
  "c05_10_disposable","c05_10_disposable_rate_limits.sql",
  "GRANT EXECUTE ON PROCEDURE",
  "CRECISOL_C05_10_RUNNER_DSN",
  "CRECISOL_C05_10_SETUP_DSN",
  "CREATE USER 'c05_10_runner'","--integration",
 ),
 ".github/workflows/c05-10-rate-limit.yml":(
  "permissions:\n  contents: read",
  "persist-credentials: false","fetch-depth: 2",
  "dotnet-version: '10.0.x'",
  "qa/c05_10_rate_limit_audits.py A",
  "qa/c05_10_rate_limit_audits.py B",
  "qa/c05_10_rate_limit_audits.py C",
  "Crecisol.RateLimitAccess.MySqlTests.csproj",
  "database/tests/c05_10_rate_limit_http_mysql.sh",
  "database/tests/c05_09_private_profiles_http_mysql.sh",
  "database/tests/c05_08_mfa_http_mysql.sh",
  "database/tests/c05_07_mailbox_http_mysql.sh",
  "database/tests/c05_06_recovery_http_mysql.sh",
  "database/tests/c05_05_session_http_mysql.sh",
  ".c05-10-rate-limit-proof","GITHUB_SHA","GITHUB_RUN_ID",
 ),
}
def need(condition:bool,reason:str)->None:
 if not condition: raise AssertionError(reason)
def material()->dict[str,str]:
 return {p:(ROOT/p).read_text(encoding="utf-8") for p in GUARDS}
def invariants(values:dict[str,str])->None:
 for path,markers in GUARDS.items():
  for marker in markers:
   need(marker in values[path],"Removed C05-10 protected guard "+path+": "+marker)
 product=(ROOT/"backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
 routes=re.findall(r'app\.Map(?:Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"',product)
 need(routes==["/api/v1/public/branding/platform"] and
      "ThrottledContextualPasswordAttempt" not in product and
      "MySqlDisposablePreAuthRateLimitLedger" not in product,
      "C05-10 MUST NOT enable fake authentication, limiter or audit API in product")
 sql=values["database/tests/fixtures/c05_10_disposable_rate_limits.sql"]
 need("FOREIGN KEY (tenant_id,purpose)" in sql and
      "REFERENCES c05_10_policies(tenant_id,purpose)" in sql and
      "UNIQUE KEY uq_c05_10_trace_scope (tenant_id,purpose,attempt_id)" in sql and
      "FROM c05_10_access_traces WHERE attempt_id=p_attempt FOR UPDATE" in sql and
      "p_source" in sql and "p_subject" in sql,
      "Actual tenant/purpose private trace FK, one-shot completion required")
 csproj=(ROOT/"backend/src/Crecisol.Infrastructure/Crecisol.Infrastructure.csproj").read_text()
 need('PackageReference Include="MySqlConnector" Version="2.6.2"' in csproj,
      "No unreviewed MySqlConnector version")
 ci=values[".github/workflows/c05-10-rate-limit.yml"]
 need("workflow_dispatch:" not in ci and
      "id-token: write" not in ci and
      "deploy" not in ci.lower(),
      "No privileged automation, deployment or external production side effects")

def a()->None:
 subprocess.run([sys.executable,"qa/master_source_guard.py","--self-test"],
                cwd=ROOT,check=True)
 subprocess.run([sys.executable,"qa/c05_09_profile_audits.py","A"],
                cwd=ROOT,check=True)
 plan=(ROOT/"docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md").read_text()
 need("C05-10 | Rate limit y trazas de acceso" in plan and
      "No registrar tokens o contraseñas" in plan,
      "Canonical Word-derived C05-10 requirement absent")
 invariants(material())
 print("PASS C05-10 A: Documento Maestro/C05-09 precedent, brute force and no-secret trace contract")

def b()->None:
 values=material()
 invariants(values)
 killed=0
 for path,markers in GUARDS.items():
  for marker in markers:
   changed=dict(values)
   changed[path]=values[path].replace(marker,"[MISSING_C05_10_GUARD]")
   need(changed[path]!=values[path],"Ineffective deletion mutation: "+marker)
   try: invariants(changed)
   except AssertionError:killed+=1
   else:raise AssertionError("Undetected C05-10 rate-limit or secret-trace bypass: "+marker)
 need(killed>=125,"Insufficient negative guard deletion sample")
 print(f"PASS C05-10 B: {killed} rate-limit, HMAC, trace and tenant guard removals rejected")
 print("Static B does NOT replace 16-way genuine HTTP/MySQL8.4 brute force tests")

def git(*args:str)->str:
 return subprocess.check_output(["git",*args],cwd=ROOT,text=True,
       stderr=subprocess.DEVNULL).strip()

def c()->None:
 need(os.getenv("GITHUB_ACTIONS")=="true" and
      os.getenv("GITHUB_REPOSITORY")==REPO,
      "Only genuine CRECISOL GitHub Actions prove C05-10")
 sha,run=os.getenv("GITHUB_SHA",""),os.getenv("GITHUB_RUN_ID","")
 need(re.fullmatch(r"[0-9a-f]{40}",sha) is not None and
      re.fullmatch(r"[0-9]+",run) is not None and
      git("rev-parse","HEAD")==sha,
      "Exact immutable commit and run ID required")
 for stage in ("A","B","build","throttle","regressions"):
  p=PROOF/stage
  need(p.is_file() and p.read_text(encoding="utf-8").strip()==
       f"PASS {stage} sha={sha} run={run}",
       "Missing real .NET10 HTTP/MySQL proof on current SHA/run: "+stage)
 if os.getenv("GITHUB_EVENT_NAME")=="pull_request":
  f=Path(os.getenv("GITHUB_EVENT_PATH",""))
  need(f.is_file(),"Actual signed PR event required")
  event=json.loads(f.read_text(encoding="utf-8"))["pull_request"]
  base,head=event["base"],event["head"]
  need(base["repo"]["full_name"]==REPO and
       head["repo"]["full_name"]==REPO and base["ref"]=="dev",
       "Untrusted GitHub repository or base branch")
  # Fail closed to TWO explicitly reviewed release/diff contracts:
  # original C05-10 11-file technical PR or 4-file postmerge remediation.
  if head["ref"]==BRANCH:
   need(base["sha"]==BASE,"Original C05-10 PR base changed")
   expected=FILES
  elif head["ref"]==REMEDIATION_BRANCH:
   need(base["sha"]==REMEDIATION_BASE,
        "Remediation must start exactly from failed C05-10 technical squash")
   expected=REMEDIATION_FILES
  else:
   raise AssertionError("Unapproved C05-10 PR branch or release scope")
  need(git("rev-list","--parents","-n","1","HEAD").split()==
       [sha,base["sha"],head["sha"]],
       "PR merge ancestry not exact")
  changed=git("diff","--name-only",base["sha"],head["sha"]).splitlines()
 else:
  need(os.getenv("GITHUB_EVENT_NAME")=="push" and
       os.getenv("GITHUB_REF")=="refs/heads/dev",
       "Only dev postmerge qualifies")
  changed=git("show","--format=","--name-only",sha).splitlines()
  expected=FILES if set(changed)==FILES else REMEDIATION_FILES
 need(set(changed)==expected and len(changed)==len(expected),
      "C05-10 diff MUST match original 11-file or reviewed remediation 4-file contract: "+
      repr(changed))
 invariants(material())
 print(f"PASS C05-10 C: real HTTP/MySQL8.4 A/B, .NET10, regressions and exact SHA={sha} run={run}")
 print("NO-GO productive API, Redis, telemetry, Cloudflare, production authentication")

if __name__=="__main__":
 try:
  need(len(sys.argv)==2 and sys.argv[1] in ("A","B","C"),
       "Usage: qa/c05_10_rate_limit_audits.py A|B|C")
  {"A":a,"B":b,"C":c}[sys.argv[1]]()
 except (AssertionError,OSError,KeyError,ValueError,subprocess.SubprocessError) as e:
  print("FAIL C05-10 audit: "+str(e),file=sys.stderr)
  sys.exit(1)
