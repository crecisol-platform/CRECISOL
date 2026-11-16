"""Prospective C05 integral diff boundary; historical microphase gates remain strict."""
from pathlib import Path
import re
import subprocess

INTEGRAL_BRANCH = "feature/c05-integral-20261115"
CORE_FLOW = "backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.cs"
REQUIRED = frozenset({CORE_FLOW,
 "backend/src/Crecisol.Api/Modules/Identity/OperationalIdentityModule.cs",
 "frontend/src/app/identity/identity-api.service.ts",
 "database/migrations/007_c05_passkeys.sql"})
ALLOWED = frozenset(['.gitattributes',
 '.github/workflows/c05-operational-flow.yml',
 'backend/src/Crecisol.Api/Modules/Identity/OperationalBrowserSessionTransport.cs',
 'backend/src/Crecisol.Api/Modules/Identity/OperationalIdentityModule.cs',
 'backend/src/Crecisol.Api/Modules/Identity/OperationalRegistrationCompletionModule.cs',
 'backend/src/Crecisol.Application/Identity/OperationalIdentityContracts.cs',
 'backend/src/Crecisol.CredentialSecurity/Crecisol.CredentialSecurity.csproj',
 'backend/src/Crecisol.CredentialSecurity/KnownCompromisedPasswordScreen.cs',
 'backend/src/Crecisol.CredentialSecurity/OperationalIdentityCrypto.cs',
 'backend/src/Crecisol.CredentialSecurity/OperationalPasskeyCrypto.cs',
 'backend/src/Crecisol.CredentialSecurity/Resources/SecLists-LICENSE.txt',
 'backend/src/Crecisol.CredentialSecurity/Resources/compromised-password-sha256.txt',
 'backend/src/Crecisol.CredentialSecurity/Resources/compromised-password-source.json',
 'backend/src/Crecisol.Infrastructure/Identity/CanonicalProfilePng.cs',
 'backend/src/Crecisol.Infrastructure/Identity/LoopbackOnlySmtpProofSink.cs',
 'backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Assisted.cs',
 'backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Invitations.cs',
 'backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.MembershipRequest.cs',
 'backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Notices.cs',
 'backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.OperatorRecovery.cs',
 'backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Pairing.cs',
 'backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Passkeys.cs',
 'backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Platform.cs',
 'backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.PlatformMailbox.cs',
 'backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Profile.cs',
 'backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.cs',
 'backend/tests/Crecisol.LoopbackSmtpReceiver/Program.cs',
 'backend/tools/Crecisol.LocalIdentityProvisioner/Crecisol.LocalIdentityProvisioner.csproj',
 'backend/tools/Crecisol.LocalIdentityProvisioner/Program.cs',
 'database/migrations/007_c05_passkeys.down.sql',
 'database/migrations/007_c05_passkeys.sql',
 'database/migrations/008_c05_terms_invitations.down.sql',
 'database/migrations/008_c05_terms_invitations.sql',
 'database/migrations/009_c05_pairing.down.sql',
 'database/migrations/009_c05_pairing.sql',
 'database/migrations/010_c05_security_notices.down.sql',
 'database/migrations/010_c05_security_notices.sql',
 'database/migrations/011_c05_platform_recovery.down.sql',
 'database/migrations/011_c05_platform_recovery.sql',
 'database/migrations/012_c05_scoped_profile.down.sql',
 'database/migrations/012_c05_scoped_profile.sql',
 'database/migrations/013_c05_abuse_signals.down.sql',
 'database/migrations/013_c05_abuse_signals.sql',
 'database/migrations/014_c05_operator_recovery.down.sql',
 'database/migrations/014_c05_operator_recovery.sql',
 'database/migrations/015_c05_session_devices.down.sql',
 'database/migrations/015_c05_session_devices.sql',
 'database/migrations/016_c05_existing_identity_join.down.sql',
 'database/migrations/016_c05_existing_identity_join.sql',
 'database/operations/c05_bootstrap_local_authority.sql',
 'database/operations/c05_local_runtime_role.sql',
 'docs/ESTADO_VIGENTE_CAPITULOS.md',
 'docs/c05/C05_INTEGRAL_OPERATIVO_2026_11_15.md',
 'frontend/package-lock.json',
 'frontend/package.json',
 'frontend/src/app/identity/browser-session-window.ts',
 'frontend/src/app/identity/canonical-photo-pixels.ts',
 'frontend/src/app/identity/identity-api.service.ts',
 'frontend/src/app/identity/platform-identity-api.service.ts',
 'frontend/src/app/pages/identity-portal.component.ts',
 'frontend/src/app/pages/platform-identity-panel.component.ts',
 'qa/c05_02_independent_smtp_audits.py',
 'qa/c05_03_password_audits.py',
 'qa/c05_assisted_browser_checks.mjs',
 'qa/c05_integral_scope.py',
 'qa/c05_invitation_checks.mjs',
 'qa/c05_operational_flow_e2e.mjs',
 'qa/c05_operator_recovery_checks.mjs',
 'qa/c05_pairing_browser_checks.mjs',
 'qa/c05_passkey_browser_checks.mjs',
 'qa/c05_profile_browser_checks.mjs'])

def validate_integral_diff(changed):
    paths = set(changed)
    assert paths and len(paths) == len(changed), "Empty or duplicate integral diff"
    assert REQUIRED <= paths, "Missing integral frontend/backend/database core"
    forbidden = [p for p in paths if p not in ALLOWED and not re.fullmatch(
        r"docs/c05/evidence/2026_11_15/[a-z0-9_-]+\.(?:log|json)", p)]
    assert not forbidden, "Out-of-scope integral files: " + repr(forbidden)

if __name__ == "__main__":
    root = Path(__file__).resolve().parents[1]
    paths = subprocess.check_output(["git", "diff", "--name-only",
        "9aea43a7b516778390e7620aeaaa22e28a463052", "HEAD"], cwd=root, text=True).splitlines()
    validate_integral_diff(paths)
    for forbidden in [".env", "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx",
        "docs/c05/ACTA_C05_01_CONTRATOS.md", ".github/workflows/deploy.yml",
        "frontend/src/app/unrelated.ts", "docs/c05/evidence/2026_11_15/payload.sh"]:
        try: validate_integral_diff(paths + [forbidden])
        except AssertionError: pass
        else: raise AssertionError("Scope accepted forbidden path: " + forbidden)
    try: validate_integral_diff([p for p in paths if p != CORE_FLOW])
    except AssertionError: pass
    else: raise AssertionError("Scope accepted a missing product core")
    print("PASS prospective C05 integral scope: actual diff accepted; seven forbidden/missing-core mutations rejected")
