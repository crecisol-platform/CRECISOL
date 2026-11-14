#!/usr/bin/env python3
"""C05-09 A/B/C: PII and photo references tenant-private, test-only.
A/B static audits never replace real .NET10 HTTP/MySQL8.4 proof.
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
BASE="ba9ad6a706cfb1d75e183a83fc023935cdbc89a8"
BRANCH="feature/c05-09-profiles-private-photo-20261114"
FILES={
    "docs/c05/C05_09_ESPECIFICACION.md",
    "backend/src/Crecisol.Domain/Identity/PrivateTenantProfileModels.cs",
    "backend/src/Crecisol.Application/Identity/PrivateTenantProfileUseCases.cs",
    "backend/src/Crecisol.CredentialSecurity/SyntheticPrivateProfileCipher.cs",
    "backend/src/Crecisol.Infrastructure/Identity/MySqlDisposablePrivateProfileLedger.cs",
    "database/tests/fixtures/c05_09_disposable_private_profiles.sql",
    "database/tests/c05_09_private_profiles_http_mysql.sh",
    "backend/tests/Crecisol.PrivateProfiles.MySqlTests/Crecisol.PrivateProfiles.MySqlTests.csproj",
    "backend/tests/Crecisol.PrivateProfiles.MySqlTests/Program.cs",
    "qa/c05_09_profile_audits.py",
    ".github/workflows/c05-09-private-profiles.yml",
}
PROOF=ROOT/".c05-09-profile-proof"
GUARDS={
    "docs/c05/C05_09_ESPECIFICACION.md":(
        "C05-09", "NO CERTIFICADA", "C05-10 BLOQUEADA",
        "Documento Maestro Único", "C05-08", "6883945140",
        "ADR-015", "C21/C32", "Perfiles y fotos seguras",
        "Datos personales y medios aislados", "No lectura cruzada de foto/PII",
        "AES-256-GCM", "Argon2id", "MySQL8.4", "Cloudflare",
        "16 updates concurrentes", "NO-GO productivo",
        "main", "solqaryn/Solqaryn", "Rollback",
    ),
    "backend/src/Crecisol.Domain/Identity/PrivateTenantProfileModels.cs":(
        "PrivateDisplayName", "Rune.IsControl",
        "NormalizationForm.FormC", "rune.Value is 0x27",
        "PrivateTenantProfileView[redacted]",
        "PrivateDisplayName[redacted]",
    ),
    "backend/src/Crecisol.Application/Identity/PrivateTenantProfileUseCases.cs":(
        "ITrustedImmediatePasswordReauthentication",
        "VerifyFreshAsync", "ContextualLoginPreparationStatus.Ready",
        "proof.TenantId==host.TenantId",
        "proof.HostRevision==host.Revision",
        "PrivateDisplayName.Parse(requestedName)",
        "EncryptForTenantActor", "DecryptForTenantActor",
        "SaveSelfAsync", "ReadSelfAsync", "EraseSelfAsync",
        "CryptographicOperations.ZeroMemory",
        "PrivateTenantProfileUseCases[redacted]",
    ),
    "backend/src/Crecisol.CredentialSecurity/SyntheticPrivateProfileCipher.cs":(
        "AesGcm(key,16)", "RandomNumberGenerator.GetBytes(12)",
        "tenant.Value.TryWriteBytes", "actor.Value.TryWriteBytes",
        "gcm.Encrypt", "gcm.Decrypt", "CryptographicOperations.ZeroMemory(key)",
        "SyntheticPrivateProfileCipher[redacted]",
    ),
    "backend/src/Crecisol.Infrastructure/Identity/MySqlDisposablePrivateProfileLedger.cs":(
        "MySqlDbType.UInt64", "b.Server", # static guard corrected below in source
        "c05_09_read_self", "c05_09_save_self", "c05_09_erase_self",
        "config.Server!=\"127.0.0.1\"",
        "config.Database!=\"c05_05_disposable\"",
        "CommandType=CommandType.StoredProcedure",
        "CryptographicOperations.ZeroMemory",
        "MySqlDisposablePrivateProfileLedger[redacted]",
    ),
    "database/tests/fixtures/c05_09_disposable_private_profiles.sql":(
        "DISPOSABLE MySQL8.4", "c05_09_media_manifest",
        "c05_09_private_profiles", "c05_09_profile_audit",
        "pii_ciphertext VARBINARY(512)", "photo_id BINARY(16)",
        "SQL SECURITY DEFINER", "FOR UPDATE",
        "c05_09_read_self", "c05_09_save_self", "c05_09_erase_self",
        "v_realm<>'TENANT'", "v_link<>p_link", "v_h<>p_host",
        "v_member_active<>1", "v_mail_verified<>1",
        "v_photo_state<>'VERIFIED'", "v_scan<>'CLEAN'",
        "LOWER(HEX(p_tenant))", "LOWER(HEX(p_actor))",
        "LOWER(HEX(p_photo))", "scan_state", "is_private",
        "SET v_new_profile=v_missing", "profile_revision=profile_revision+1",
        "pii_ciphertext=NULL", "photo_id=NULL",
        "CREATE TRIGGER c05_09_audit_no_update",
        "CREATE TRIGGER c05_09_audit_no_delete",
        "ROLLBACK", "COMMIT",
    ),
    "backend/tests/Crecisol.PrivateProfiles.MySqlTests/Program.cs":(
        "Argon2idPasswordHasher", "PrivateTenantProfileUseCases",
        "SyntheticPrivateProfileCipher", "http://127.0.0.1:0",
        "X-Tenant-Id", "X-Forwarded-Host",
        "UseProxy=false", "CacheControl=\"no-store\"",
        "Tenant B media ID cannot attach",
        "Same tenant different actor media ID cannot attach",
        "Pending upload cannot", "Revoked upload cannot",
        "SVG active content", "R2-style media manifest",
        "16 concurrent HTTP profile CAS updates: exactly one winner",
        "Reactivated membership cannot resurrect",
        "PLATFORM realm denied", "Erased profile",
        "Append-only profile audit UPDATE prohibited",
        "Append-only profile audit DELETE prohibited",
        "real C05-04 Argon2id reauthentication",
        "NO-GO production",
    ),
    "database/tests/c05_09_private_profiles_http_mysql.sh":(
        "mysql:8.4@sha256:", "127.0.0.1::3306",
        "c05_05_disposable_sessions.sql",
        "c05_06_disposable_recovery.sql",
        "c05_09_disposable_private_profiles.sql",
        "GRANT EXECUTE ON PROCEDURE",
        "CRECISOL_C05_09_RUNNER_DSN",
        "CRECISOL_C05_09_SETUP_DSN",
        "--integration",
    ),
    ".github/workflows/c05-09-private-profiles.yml":(
        "permissions:\n  contents: read",
        "persist-credentials: false", "fetch-depth: 2",
        "dotnet-version: '10.0.x'",
        "qa/c05_09_profile_audits.py A",
        "qa/c05_09_profile_audits.py B",
        "qa/c05_09_profile_audits.py C",
        "Crecisol.PrivateProfiles.MySqlTests.csproj",
        "database/tests/c05_09_private_profiles_http_mysql.sh",
        "Crecisol.MfaRisk.ContractsTests.csproj",
        "database/tests/c05_08_mfa_http_mysql.sh",
        "database/tests/c05_07_mailbox_http_mysql.sh",
        "database/tests/c05_06_recovery_http_mysql.sh",
        "database/tests/c05_05_session_http_mysql.sh",
        ".c05-09-profile-proof", "GITHUB_SHA", "GITHUB_RUN_ID",
    ),
}
# Catch accidental guard-copy typo before run; exact reviewed code markers.
GUARDS["backend/src/Crecisol.Infrastructure/Identity/MySqlDisposablePrivateProfileLedger.cs"] = tuple(
    x for x in GUARDS["backend/src/Crecisol.Infrastructure/Identity/MySqlDisposablePrivateProfileLedger.cs"]
    if x!="b.Server"
)

def need(ok:bool,why:str)->None:
    if not ok: raise AssertionError(why)
def material()->dict[str,str]:
    return {path:(ROOT/path).read_text(encoding="utf-8") for path in GUARDS}

def invariants(values:dict[str,str])->None:
    for path,markers in GUARDS.items():
        for marker in markers:
            need(marker in values[path], "Removed C05-09 security control: "+path+": "+marker)
    product=(ROOT/"backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
    routes=re.findall(r'app\.Map(?:Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"',product)
    need(routes==["/api/v1/public/branding/platform"] and
         "PrivateTenantProfileUseCases" not in product and
         "MySqlDisposablePrivateProfileLedger" not in product,
         "Profile/PII test endpoints MUST NOT enter public product API")
    sql=values["database/tests/fixtures/c05_09_disposable_private_profiles.sql"]
    need("REFERENCES c05_05_subjects" in sql and
         "FOREIGN KEY (tenant_id,actor_id,photo_id)" in sql and
         "ON DELETE RESTRICT" in sql and
         "content_type='image/webp'" in sql and
         "byte_length BETWEEN 128 AND 2097152" in sql,
         "Composite tenant FK and private image metadata validation mandatory")
    infra=(ROOT/"backend/src/Crecisol.Infrastructure/Crecisol.Infrastructure.csproj").read_text()
    need('PackageReference Include="MySqlConnector" Version="2.6.2"' in infra,
         "No unreviewed MySqlConnector dependency/version")
    ci=values[".github/workflows/c05-09-private-profiles.yml"]
    need("workflow_dispatch:" not in ci and
         "id-token: write" not in ci and
         "deploy" not in ci.lower(),
         "No privileged CI, production deployment or manual activation")

def a()->None:
    subprocess.run([sys.executable,"qa/master_source_guard.py","--self-test"],
        cwd=ROOT,check=True)
    subprocess.run([sys.executable,"qa/c05_08_mfa_crypto_audits.py","A"],
        cwd=ROOT,check=True)
    spec=(ROOT/"docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md").read_text()
    adr=(ROOT/"docs/ADR-015_CLOUDFLARE_ALMACENAMIENTO_MULTIMEDIA.md").read_text()
    need("C05-09 | Perfiles y fotos seguras" in spec and
         "No lectura cruzada de foto/PII" in spec and
         "MySQL almacenará la referencia estable" in adr and
         "No persistir URL presignada temporal" in adr,
         "Document Maestro-derived profile and ADR-015 obligations missing")
    invariants(material())
    print("PASS C05-09 A: Word source, C05-08 certified, ADR-015 and exact private PII/photo contract")

def b()->None:
    values=material()
    invariants(values)
    count=0
    for path,markers in GUARDS.items():
        for marker in markers:
            changed=dict(values)
            changed[path]=values[path].replace(marker,"[REMOVED_C05_09_GUARD]")
            need(changed[path]!=values[path],"Ineffective mutation: "+path+": "+marker)
            try: invariants(changed)
            except AssertionError: count+=1
            else: raise AssertionError("Undetected PII/media/tenancy guard deletion "+path+": "+marker)
    need(count>=100,"Insufficient C05-09 negative security mutation sample")
    print(f"PASS C05-09 B: {count} static PII/photo tenancy security guard removals rejected")
    print("B static guard deletion MUST be paired with genuine HTTP/MySQL negatives")

def git(*args:str)->str:
    return subprocess.check_output(["git",*args],cwd=ROOT,text=True,
        stderr=subprocess.DEVNULL).strip()

def c()->None:
    need(os.getenv("GITHUB_ACTIONS")=="true" and
         os.getenv("GITHUB_REPOSITORY")==REPO,
         "Only genuine CRECISOL CI can attest C05-09")
    sha=os.getenv("GITHUB_SHA","")
    run=os.getenv("GITHUB_RUN_ID","")
    need(re.fullmatch(r"[a-f0-9]{40}",sha) is not None and
         re.fullmatch(r"[0-9]+",run) is not None and
         git("rev-parse","HEAD")==sha,
         "Exact SHA/run immutable checkout required")
    for stage in ("A","B","build","profiles","regressions"):
        file=PROOF/stage
        need(file.is_file() and
             file.read_text(encoding="utf-8").strip()==
               f"PASS {stage} sha={sha} run={run}",
             "Missing current SHA/run real proof "+stage)
    if os.getenv("GITHUB_EVENT_NAME")=="pull_request":
        file=Path(os.getenv("GITHUB_EVENT_PATH",""))
        need(file.is_file(),"Signed CI PR event required")
        event=json.loads(file.read_text())["pull_request"]
        base,head=event["base"],event["head"]
        need(base["ref"]=="dev" and base["sha"]==BASE and
             head["ref"]==BRANCH and
             base["repo"]["full_name"]==REPO and
             head["repo"]["full_name"]==REPO,
             "Untrusted PR base or head branch/owner")
        need(git("rev-list","--parents","-n","1","HEAD").split()==
             [sha,base["sha"],head["sha"]],
             "GitHub synthesized merge must have exact base/head")
        changed=git("diff","--name-only",base["sha"],head["sha"]).splitlines()
    else:
        need(os.getenv("GITHUB_EVENT_NAME")=="push" and
             os.getenv("GITHUB_REF")=="refs/heads/dev",
             "Only dev postmerge may count")
        changed=git("show","--format=","--name-only",sha).splitlines()
    need(set(changed)==FILES and len(changed)==len(FILES),
         "C05-09 diff must contain EXACTLY 11 approved files: "+repr(changed))
    invariants(material())
    print(f"PASS C05-09 C: A/B, .NET10 HTTP/MySQL8.4 real, C05 regressions SHA={sha} run={run}")
    print("Scope TEST ONLY: no customer PII, real Cloudflare, image upload, login or productive profile API")

if __name__=="__main__":
    try:
        need(len(sys.argv)==2 and sys.argv[1] in ("A","B","C"),
             "Usage: qa/c05_09_profile_audits.py A|B|C")
        {"A":a,"B":b,"C":c}[sys.argv[1]]()
    except (AssertionError,OSError,KeyError,ValueError,subprocess.SubprocessError) as e:
        print("FAIL C05-09 profile audit: "+str(e),file=sys.stderr)
        sys.exit(1)
