#!/usr/bin/env python3
"""C05 exclusive TOTP-SHA1 current-source A/B/C. Historical certificates unchanged."""
from __future__ import annotations
from pathlib import Path
import os
import sys,subprocess
ROOT=Path(__file__).resolve().parents[1]
BASE="b996798c1c1adf8cf08e9798f03d6570de061c0b"
F={
"backend/src/Crecisol.CredentialSecurity/TotpSha1Verifier.cs":["HMACSHA1.HashData(seed, counter)","seed.Length != 20","MaxPastSteps = 1","step <= lastCommittedStep","FixedTimeEquals"],
"backend/src/Crecisol.CredentialSecurity/OperationalIdentityCrypto.cs":["TotpSha1Verifier.TryVerify"],
"backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.cs":["SELECT policy_id FROM c05_totp_sha1_policy WHERE policy_id=1 AND status=1","algorithm=SHA1&digits=6&period=30"],
"backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.PasswordMfa.cs":["GetBytes(20)",'TotpUri(actor, seed, "cuenta")'],
"backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.OperatorRecovery.cs":["GetBytes(20)",'TotpUri(target, seed, "operador")','TotpUri(actor, seed, "operador")'],
"backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.PendingApprovals.cs":[
 "WHERE tenant_id=@t AND account_id=@a", "scope_kind=2 AND resource_id IS NULL AND branch_id=@t",
 "WHERE m.tenant_id=@t AND m.membership_state=0", "LIMIT 13",
 "MaskedPendingMailbox(row)", "CryptographicOperations.ZeroMemory(plain)"],
"backend/tools/Crecisol.LocalIdentityProvisioner/Program.cs":["--isolated-existing-factor-bundle","@example.test","GetBytes(20)"],
"database/migrations/017_c05_totp_sha1_exclusive.sql":["c05_totp_sha1_policy","UPDATE c05_identity_mfa_factor","protected_secret=X''","UPDATE c05_platform_operator","recovery_pending=1,protected_seed=X''","UPDATE c05_identity_session SET revoked_at_utc","UPDATE c05_platform_session SET revoked_at_utc","NOT EXISTS(SELECT 1 FROM c05_platform_operator"],
"database/operations/c05_local_runtime_role.sql":["GRANT SELECT ON crecisol_c05.c05_totp_sha1_policy"],
"frontend/src/app/identity/totp-setup-qr.ts":["QRCode.toDataURL(uri","algorithm') !== 'SHA1'","data:image/png;base64","otpauth:"],
"frontend/src/app/pages/identity-mfa.component.html":["identity.enrollmentQr()","No puedo escanear: mostrar clave manual"],
"frontend/src/app/identity/identity-api.service.ts":["this.challengeValue !== challenge","this.stateValue() !== 'challenge'","this.enrollmentSecret.set('');"],
"frontend/src/app/pages/platform-operator-security.component.html":["platform.factorQr()","platform.discardFactorSetup()"],
"frontend/src/app/pages/platform-identity-panel.component.html":["platform.operatorRecoveryQr()","platform.discardRecoverySetup()"],
"qa/c05_operational_flow_e2e.mjs":["017_c05_totp_sha1_exclusive","createHmac('sha1'","verifySha1EnrollmentQr"],
"qa/c05_totp_qr_checks.mjs":["jsQR(","searchParams.get('algorithm') === 'SHA1'"],
"frontend/src/app/identity/platform-identity-api.service.ts":["this.recoveryReference.set('');","this.operatorRecoveryQr.set('');",
 "revision !== this.factorQrRevision","revision !== this.recoveryQrRevision",
 "++this.factorQrRevision","++this.recoveryQrRevision",
 "this.discardFactorSetup();","this.discardRecoverySetup();"],
"database/tests/c05_sha1_offline_rekey_mysql.sh":["createDecipheriv('aes-256-gcm'","C05/operational/v1/platform-factor:","C05/platform-backup:","offline SHA1 crypto"],
# C05-018 policy must not regress to browser-only timeout or delegated tenant admin.
"backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.SessionPolicy.cs":[
 "MinSessionIdleMinutes = 5", "MaxSessionIdleMinutes = 120",
 "can_manage_session_policy", "await PlatformTotp(u, operatorPrincipal",
 "expected != revision", "last_seen_at_utc=UTC_TIMESTAMP(6)",
 "PlatformSessionPolicy", "SessionIdleMinutes",
 "SET expires_at_utc=DATE_ADD(last_seen_at_utc,INTERVAL @idle MINUTE)"],
"database/migrations/018_c05_session_idle_policy.sql":[
 "idle_minutes SMALLINT UNSIGNED NOT NULL DEFAULT 30",
 "CHECK(idle_minutes BETWEEN 5 AND 120)",
 "can_manage_session_policy BOOLEAN NOT NULL DEFAULT FALSE",
 "UPDATE c05_identity_session SET revoked_at_utc=UTC_TIMESTAMP(6)",
 "UPDATE c05_platform_session SET revoked_at_utc=UTC_TIMESTAMP(6)"],
"frontend/src/app/identity/browser-session-window.ts":[
 "establish(expiresAtUtc: string, idleMinutes: number)",
 "this.expiryTimer = setTimeout",
 "this.clear();"],
"qa/c05_local_persistent.mjs":[
 "C05-local-backup-v2","backupManifestMac","authenticatedManifest",
 "timingSafeEqual","assertProtectedFile","legacy manifest is insufficient",
 "'mysql', '--protocol=TCP', '-h127.0.0.1'"],
"qa/c05_sha1_local_operator_rekey.mjs":["--rekey-after-verified-017",
 "CRECISOL_C05_LOCAL_SHA1_REKEY_APPROVED", "START TRANSACTION;",
 "c05_rekey_guard", "access.md.sha1-pending",
 "recovery_pending=1 AND LENGTH(protected_seed)=0"],
}
PROTECTED={
"backend/src/Crecisol.CredentialSecurity/OperationalIdentityCrypto.cs",
"backend/src/Crecisol.CredentialSecurity/TotpSha1Verifier.cs",
"backend/src/Crecisol.CredentialSecurity/TotpSha256Verifier.cs",
"backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.cs",
"backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.PasswordMfa.cs",
"backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.OperatorRecovery.cs",
"backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.PendingApprovals.cs",
"backend/tests/Crecisol.MfaRisk.ContractsTests/Program.cs",
"backend/tests/Crecisol.MfaRisk.MySqlTests/Program.cs",
"backend/tests/Crecisol.NeutralRegistration.IntegrationTests/LocalMfaBoundSessionHarness.cs",
"backend/tools/Crecisol.LocalIdentityProvisioner/Program.cs",
"database/migrations/017_c05_totp_sha1_exclusive.sql",
"database/migrations/017_c05_totp_sha1_exclusive.down.sql",
"database/operations/c05_local_runtime_role.sql",
"database/tests/c05_sha1_cutover_mysql.sh",
"database/tests/c05_sha1_offline_rekey_mysql.sh",
# C05-018 can change only the explicit session transport/authority,
# the isolated inactivity-policy schema, and its tests. Keep fail-closed
# for C01/C02/C03/C04, arbitrary SQL/crypto, and the Word/PDF master.
"backend/src/Crecisol.Application/Identity/OperationalIdentityContracts.cs",
"backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Authorized.cs",
"backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Platform.cs",
"backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.SessionPolicy.cs",
"backend/src/Crecisol.Api/Modules/Identity/OperationalIdentityModule.cs",
"backend/src/Crecisol.Api/Modules/Identity/OperationalBrowserSessionTransport.cs",
"database/migrations/018_c05_session_idle_policy.sql",
"database/migrations/018_c05_session_idle_policy.down.sql",
}
def ensure(ok,msg):
 if not ok: raise AssertionError(msg)

# C05's original SHA1 cutover has already landed. Restrict later change
# policing to C05 security boundaries; other chapters have their own gates.
# All SHA1/TOTP source invariants and original 017-cutover provenance remain.
def is_c05_security_path(path):
 return (
  path.startswith((
   "backend/src/Crecisol.CredentialSecurity/",
   "backend/src/Crecisol.Infrastructure/Identity/",
   "backend/src/Crecisol.Application/Identity/",
   "backend/src/Crecisol.Api/Modules/Identity/",
   "backend/tools/Crecisol.LocalIdentityProvisioner/",
   "backend/tests/Crecisol.MfaRisk.",
   "database/operations/c05_",
   "database/tests/c05_",
   "frontend/src/app/identity/",
   ".github/workflows/c05-",
  )) or
  (path.startswith("database/migrations/") and "_c05_" in path) or
  path in {
    "frontend/src/app/pages/identity-mfa.component.html",
    "frontend/src/app/pages/platform-operator-security.component.html",
    "frontend/src/app/pages/platform-identity-panel.component.html",
  }
 )

def check_current_scope(paths):
 ensure(bool(paths), "empty current change scope")
 if "docs/c06/C06_10_ESPECIFICACION.md" in paths:
  from c06_10_offensive_scope import require_exact
  require_exact(paths)
  return len(paths)
 if "docs/c06/C06_09_ESPECIFICACION.md" in paths:
  from c06_09_delivery_scope import require_exact
  require_exact(paths)
  return len(paths)
 if "docs/c06/C06_08_ESPECIFICACION.md" in paths:
  from c06_08_query_scope import require_exact
  require_exact(paths)
  return len(paths)
 for path in paths:
  if path.startswith(("frontend/src/app/pages/platform-home",
                      "docs/CRECISOL_DOCUMENTO_MAESTRO")):
   raise AssertionError("unapproved original brand/master change "+path)
  if is_c05_security_path(path) and path not in PROTECTED:
   raise AssertionError("unapproved NEW C05 security entrypoint "+path)
 return len(paths)

def current_diff():
 event=os.environ.get("GITHUB_EVENT_NAME","")
 if event=="pull_request":
  ensure(os.environ.get("GITHUB_BASE_REF")=="dev", "C05 scope PR must target dev")
  spec=["origin/dev...HEAD"]
 elif event=="push":
  ensure(os.environ.get("GITHUB_REF")=="refs/heads/dev", "C05 scope push must target dev")
  spec=["HEAD^","HEAD"]
 else:
  spec=[BASE+"...HEAD"]
 return subprocess.run(["git","diff","--name-only",*spec],cwd=ROOT,
                       capture_output=True,text=True,check=True).stdout.splitlines()

def read(): return {p:(ROOT/p).read_text() for p in F}
def verify(d):
 for p,markers in F.items():
  for marker in markers: ensure(marker in d[p],"missing mandatory SHA1 guard: "+p+" "+marker)
 ensure(not (ROOT/"backend/src/Crecisol.CredentialSecurity/TotpSha256Verifier.cs").exists(),"legacy verifier present")
 ensure("HMACSHA256" not in d["backend/src/Crecisol.CredentialSecurity/TotpSha1Verifier.cs"],"TOTP downgrade")
 policy=d["backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.SessionPolicy.cs"]
 ensure(policy.count("AND expires_at_utc>UTC_TIMESTAMP(6)") >= 2,
        "tenant and operator renewals must revalidate server-clock expiry at UPDATE")
 ensure(policy.count("AND last_seen_at_utc>UTC_TIMESTAMP(6)-INTERVAL @idle MINUTE") >= 2,
        "tenant and operator renewals must recheck inactivity at UPDATE")
 supervisor=d["qa/c05_local_persistent.mjs"]
 ensure(supervisor.count("'mysql', '--protocol=TCP', '-h127.0.0.1'") == 2,
        "C05 startup and isolated restore require final TCP readiness, not temporary socket")
 ensure("`MYSQL_PWD=${" not in supervisor,"MySQL credentials must never be interpolated into Docker CLI argv")
 workflow=(ROOT/".github/workflows/c05-sha1-persistent-backup.yml").read_text()
 ensure('-e "MYSQL_PWD=$ROOT_PWD"' not in workflow,"workflow exposes MySQL password in argv")
 sql=d["database/migrations/017_c05_totp_sha1_exclusive.sql"]
 ensure("DELETE FROM c05_identity_account" not in sql and "DROP TABLE c04_tenant_catalog" not in sql,"unscoped destructive migration")
 qr=d["frontend/src/app/identity/totp-setup-qr.ts"]
 ensure("fetch(" not in qr and "localStorage" not in qr and "sessionStorage" not in qr,"QR secret exfiltration")
 tenant=d["frontend/src/app/identity/identity-api.service.ts"].split("  async enrollMfa():",1)[-1].split("  async refreshSessions():",1)[0]
 ensure(tenant.count("this.challengeValue !== challenge") >= 2,"tenant MFA QR missing pre/post async invalidation")
 platform=d["frontend/src/app/identity/platform-identity-api.service.ts"]
 factor=platform.split("  async replaceFactor(",1)[-1].split("  async confirmFactor(",1)[0]
 recovery=platform.split("  async completeRecovery(",1)[-1].split("  async confirmRecovery(",1)[0]
 ensure(factor.count("revision !== this.factorQrRevision") >= 2,"operator factor QR missing async invalidation")
 ensure(recovery.count("revision !== this.recoveryQrRevision") >= 2,"operator recovery QR missing async invalidation")
def run(which):
 d=read()
 if which=="A":
  subprocess.run([sys.executable,"qa/validate_foundation.py"],cwd=ROOT,check=True)
  ensure("C05" in (ROOT/"docs/ESTADO_VIGENTE_CAPITULOS.md").read_text(),"authority missing")
  verify(d)
  print("PASS exclusive SHA1 A: source, RFC6238, QR, C05 realms")
 elif which=="B":
  verify(d);n=0
  for p,markers in F.items():
   for marker in markers:
    replaced=d.copy();replaced[p]=d[p].replace(marker,"[REMOVED]")
    ensure(replaced[p]!=d[p],"ineffective mutation")
    try: verify(replaced)
    except AssertionError: n+=1
    else: raise AssertionError("undetected negative guard deletion")
  ensure(n>=32,"insufficient negative tests")
  # Seven adversarial additions to C05 are still rejected; an independent
  # C06 authorization module must not fail a historical C05-only scope gate.
  for injected in (
    "backend/src/Crecisol.CredentialSecurity/TotpSha256Fallback.cs",
    "backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Bypass.cs",
    "backend/src/Crecisol.Application/Identity/UnsafeSessionOverride.cs",
    "backend/src/Crecisol.Api/Modules/Identity/DebugLogin.cs",
    "backend/tools/Crecisol.LocalIdentityProvisioner/Unreviewed.cs",
    "database/migrations/020_c05_unsafe_grants.sql",
    "database/operations/c05_unscoped_runtime_role.sql",
  ):
   try: check_current_scope([injected])
   except AssertionError: n+=1
   else: raise AssertionError("unapproved C05 security file passed: "+injected)
  ensure(check_current_scope([
    "backend/src/Crecisol.Application/Authorization/InspectPreparedTenantRoles.cs",
    "database/migrations/019_c06_prepared_roles.sql"])==2,
    "independent C06 contract incorrectly classified as C05")
  print(f"PASS exclusive SHA1 B: {n} negative guards (includes 7 C05 scope intrusions) rejected")
 else:
  verify(d)
  history=subprocess.run(["git","diff","--name-only",BASE+"...HEAD"],cwd=ROOT,
                         capture_output=True,text=True,check=True).stdout.splitlines()
  ensure("database/migrations/017_c05_totp_sha1_exclusive.sql" in history,
         "SHA1 cutover history is missing")
  changed=current_diff()
  checked=check_current_scope(changed)
  print(f"PASS exclusive SHA1 C: original 017 cutover intact; {checked} current paths checked for C05 security, no C01/master changes")
if __name__=="__main__":
 try:
  ensure(len(sys.argv)==2 and sys.argv[1] in ("A","B","C"),"usage A|B|C")
  run(sys.argv[1])
 except (AssertionError,OSError,subprocess.SubprocessError) as e:
  print("FAIL exclusive SHA1 audit: "+str(e),file=sys.stderr);raise SystemExit(1)
