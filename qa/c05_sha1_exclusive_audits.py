#!/usr/bin/env python3
"""C05 exclusive TOTP-SHA1 current-source A/B/C. Historical certificates unchanged."""
from __future__ import annotations
from pathlib import Path
import sys,subprocess
ROOT=Path(__file__).resolve().parents[1]
BASE="b996798c1c1adf8cf08e9798f03d6570de061c0b"
F={
"backend/src/Crecisol.CredentialSecurity/TotpSha1Verifier.cs":["HMACSHA1.HashData(seed, counter)","seed.Length != 20","MaxPastSteps = 1","step <= lastCommittedStep","FixedTimeEquals"],
"backend/src/Crecisol.CredentialSecurity/OperationalIdentityCrypto.cs":["TotpSha1Verifier.TryVerify"],
"backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.cs":["SELECT policy_id FROM c05_totp_sha1_policy WHERE policy_id=1 AND status=1","algorithm=SHA1&digits=6&period=30"],
"backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.PasswordMfa.cs":["GetBytes(20)",'TotpUri(actor, seed, "cuenta")'],
"backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.OperatorRecovery.cs":["GetBytes(20)",'TotpUri(target, seed, "operador")','TotpUri(actor, seed, "operador")'],
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
"backend/tests/Crecisol.MfaRisk.ContractsTests/Program.cs",
"backend/tests/Crecisol.MfaRisk.MySqlTests/Program.cs",
"backend/tests/Crecisol.NeutralRegistration.IntegrationTests/LocalMfaBoundSessionHarness.cs",
"backend/tools/Crecisol.LocalIdentityProvisioner/Program.cs",
"database/migrations/017_c05_totp_sha1_exclusive.sql",
"database/migrations/017_c05_totp_sha1_exclusive.down.sql",
"database/operations/c05_local_runtime_role.sql",
"database/tests/c05_sha1_cutover_mysql.sh",
"database/tests/c05_sha1_offline_rekey_mysql.sh",
}
def ensure(ok,msg):
 if not ok: raise AssertionError(msg)
def read(): return {p:(ROOT/p).read_text() for p in F}
def verify(d):
 for p,markers in F.items():
  for marker in markers: ensure(marker in d[p],"missing mandatory SHA1 guard: "+p+" "+marker)
 ensure(not (ROOT/"backend/src/Crecisol.CredentialSecurity/TotpSha256Verifier.cs").exists(),"legacy verifier present")
 ensure("HMACSHA256" not in d["backend/src/Crecisol.CredentialSecurity/TotpSha1Verifier.cs"],"TOTP downgrade")
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
  print(f"PASS exclusive SHA1 B: {n} negative guard removals rejected")
 else:
  verify(d)
  out=subprocess.run(["git","diff","--name-only",BASE+"...HEAD"],cwd=ROOT,capture_output=True,text=True,check=True).stdout.splitlines()
  ensure(out,"missing diff")
  for path in out:
   if path.startswith(("backend/","database/")):
    ensure(path in PROTECTED,"unauthorized protected scope "+path)
   ensure(not path.startswith(("frontend/src/app/pages/platform-home","docs/CRECISOL_DOCUMENTO_MAESTRO")),"C01/master changed")
  ensure("database/migrations/017_c05_totp_sha1_exclusive.sql" in out,"cutover absent")
  print(f"PASS exclusive SHA1 C: {len(out)} scoped paths, no C01/C02/master changes")
if __name__=="__main__":
 try:
  ensure(len(sys.argv)==2 and sys.argv[1] in ("A","B","C"),"usage A|B|C")
  run(sys.argv[1])
 except (AssertionError,OSError,subprocess.SubprocessError) as e:
  print("FAIL exclusive SHA1 audit: "+str(e),file=sys.stderr);raise SystemExit(1)
