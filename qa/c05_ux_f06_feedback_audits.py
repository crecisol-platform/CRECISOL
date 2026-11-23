#!/usr/bin/env python3
"""C05 UX F06 — A/B/C static gates. Never equate static checks with runtime success."""
from pathlib import Path
import csv
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
UI = ROOT / "frontend/src/app/ui/feedback"
PAGES = ROOT / "frontend/src/app/pages"
MODAL = ROOT / "frontend/src/app/ui/institutional-modal"
BASE = "b576d6ff64aea7d417c59aa851618dbf4dced68a"

ALLOWED = {
    "frontend/src/app/ui/feedback/operation-feedback.model.ts",
    "frontend/src/app/ui/feedback/operation-feedback.component.ts",
    "frontend/src/app/ui/feedback/operation-feedback.component.html",
    "frontend/src/app/ui/feedback/operation-feedback.component.css",
    "frontend/src/app/ui/feedback/secure-field-feedback.directive.ts",
    "frontend/src/app/identity/identity-api.service.ts",
    "frontend/src/app/identity/platform-identity-api.service.ts",
    "frontend/src/app/ui/institutional-modal/institutional-modal.component.ts",
    "frontend/src/app/ui/institutional-modal/institutional-modal.component.html",
    "frontend/src/app/pages/identity-portal.component.ts",
    "frontend/src/app/pages/identity-portal.component.html",
    "frontend/src/app/pages/identity-flow.component.css",
    "frontend/src/app/pages/platform-operator-f04.component.css",
    "frontend/src/app/pages/identity-portal.component.css",
    "frontend/src/app/pages/identity-flow-views.component.ts",
    "frontend/src/app/pages/platform-identity-panel.component.ts",
    "frontend/src/app/pages/platform-identity-panel.component.html",
    "frontend/src/app/pages/platform-operator-views.component.ts",
    "frontend/src/app/pages/identity-guest.component.html",
    "frontend/src/app/pages/identity-mfa.component.html",
    "frontend/src/app/pages/identity-recovery.component.html",
    "frontend/src/app/pages/identity-workspace.component.html",
    "frontend/src/app/pages/identity-workspace.component.css",
    "frontend/src/app/pages/platform-operator-cases.component.html",
    "frontend/src/app/pages/platform-operator-monitor.component.html",
    "frontend/src/app/pages/platform-operator-profile.component.html",
    "frontend/src/app/pages/platform-operator-security.component.html",
    "frontend/src/app/pages/platform-operator-sessions.component.html",
    "frontend/src/styles.css",
    "frontend/tests/c05-f03-navigation.spec.ts",  # Exact F03 recovery keyboard regression forward-adjusted in F06.
    "frontend/tests/c05-f06-context-feedback.spec.ts",
    "frontend/playwright.c05-f06-feedback.config.ts",
    "qa/c05_ux_f05_modal_audits.py",
    "qa/c05_operational_flow_e2e.mjs",
    # Exact helpers used by the real F06 E2E (no unrestricted QA wildcard).
    "qa/c05_passkey_browser_checks.mjs",
    "qa/c05_pairing_browser_checks.mjs",
    "qa/c05_profile_browser_checks.mjs",
    "qa/c05_ux_f06_feedback_audits.py",
    "qa/c05_ux_f06_feedback_evidence.py",
    ".github/workflows/c05-ux-f06-feedback.yml",
    "docs/c05/ux/F06_MENSAJERIA_ESTADOS_MATRIZ_RIESGOS.md",
    "docs/c05/ux/F06_REVISION_PREMIUM_INTERFAZ_POR_INTERFAZ.md",
    "qa/c05_ux_f06_premium_evidence.py",
    # F01 inventory reconciliation: explicitly enumerated files only.
    "frontend/src/app/ui/institutional-modal/institutional-modal.component.css",
    "frontend/tests/c05-f05-secure-modals.spec.ts",
    "frontend/tests/c05-f06-inventory-65.spec.ts",
    "frontend/playwright.c05-f06-inventory65.config.ts",
    "qa/c05_ux_f06_65_coverage.py",
    ".github/workflows/c05-ux-f06-65-inventory.yml",
    "docs/c05/ux/F06_65_COBERTURA_Y_CRITERIOS.md",
    "docs/c05/ux/F06_65_SUPERFICIES_VALORACION.csv",
    # Deliberately provisional 65-ID buyer record; NEVER final grades or signoff.
    "docs/c05/ux/F06_65_REVISION_COMPRADOR_PRELIMINAR_2026_11_22.csv",
    # Post-integration F06 security-gate correction and isolated negative tests.
    "qa/c05_ux_f06_feedback_scope_tests.py",
    "docs/c05/ux/F06_GATE_ALCANCE_POSTMERGE_2026_11_23.md",
    # Exact test-only buyer grade gate: no wildcard or permission expansion.
    "qa/c05_ux_f06_commercial_rating_tests.py"
}

def text(path: str) -> str:
    file = ROOT / path
    assert file.is_file(), "F06 missing " + path
    return file.read_text("utf-8")

def need(condition: bool, reason: str) -> None:
    if not condition: raise AssertionError("F06 NO_GO: " + reason)

# Fixed signed-off candidate imported by squash PR #182 (F04) and #183 (F05).
# It is an immutable provenance boundary, NOT an allowlist for future commits.
# F06's original BASE is intentionally retained: its historical diff and checks
# cannot simply be erased to silence the legacy workflow.
INTEGRATED_F04 = "1d8a8e552b6eb7749d81aafaaa1e5201163b6c4e"
INTEGRATED_F05 = "196512a6b5747ce8c86a5d71c4e766af6ee4b047"


def git_paths(*args: str) -> set[str]:
    lines = subprocess.check_output(["git", *args], cwd=ROOT, text=True).splitlines()
    return {line.strip() for line in lines if line.strip()}


def authorize_delta(
    historical_all: set[str],
    approved_snapshot: set[str],
    current_diff: set[str],
    touched_commits: set[str]
) -> list[str]:
    """Fail closed for every new or transient file outside original F06 scope.

    Earlier imported F01-F05 files are accepted *only* if contained in the
    immutable, known-good integrated SHA and unchanged since that SHA.
    """
    need(bool(historical_all), "F06 original diff absent")
    need(bool(current_diff), "no F06 correction after the approved integration")
    need(current_diff <= ALLOWED,
         "new F06 out of scope: " + str(sorted(current_diff - ALLOWED)))
    # A commit which changes a forbidden file and later reverts it is blocked,
    # even if the final git diff no longer includes the forbidden change.
    need(touched_commits <= ALLOWED,
         "F06 commit history touched foreign scope: " +
         str(sorted(touched_commits - ALLOWED)))
    need((historical_all - ALLOWED) <= approved_snapshot,
         "unexpected inherited change outside approved integration: " +
         str(sorted((historical_all - ALLOWED) - approved_snapshot)))
    return sorted(historical_all & ALLOWED)


def scope() -> list[str]:
    # Accept F04/F05 only at their exact, audited ancestry, never by globbing
    # unknown merged files or silently resetting BASE to current HEAD.
    for parent, child in ((BASE, INTEGRATED_F04),
                          (INTEGRATED_F04, INTEGRATED_F05),
                          (INTEGRATED_F05, "HEAD")):
        subprocess.run(["git", "merge-base", "--is-ancestor", parent, child],
                       cwd=ROOT, check=True, stdout=subprocess.DEVNULL)
    historical_all = git_paths("diff", "--name-only", "--no-renames", BASE + "...HEAD")
    snapshot = git_paths("diff", "--name-only", "--no-renames",
                         BASE + "..." + INTEGRATED_F05)
    current = git_paths("diff", "--name-only", "--no-renames",
                        INTEGRATED_F05 + "...HEAD")
    touched = git_paths("log", "--format=", "--name-only", "--no-renames",
                        INTEGRATED_F05 + "..HEAD")
    changed = authorize_delta(historical_all, snapshot, current, touched)
    # Inspect full F06 file bodies, including previously merged changes on
    # these F06-owned surfaces. Do not scan unrelated, signed-off F01-F05 docs
    # as if they were newly edited by F06.
    for path in changed:
        if path == "qa/c05_ux_f06_feedback_audits.py":
            continue  # This validator intentionally embeds malformed-token fixtures.
        if not path.endswith((".html", ".css", ".ts", ".py", ".md", ".yml")):
            continue
        source = text(path)
        for malformed in ("\ufffd", "Ã¡", "Ã©", "Ã³", "Ã±", "Ãº", "Â¿", "Â¡",
                          "â€¦", "â€”", "â€œ", "â€"):
            need(malformed not in source,
                 "malformed Spanish/UTF-8 text in " + path)
    print(f"F06 scoped: {len(changed)} owned files; "
          f"{len(historical_all - ALLOWED)} unchanged inherited paths; "
          f"{len(current)} post-merge edits checked")
    return changed

def audit_a() -> None:
    changes = scope()
    provisional = "docs/c05/ux/F06_65_REVISION_COMPRADOR_PRELIMINAR_2026_11_22.csv"
    if provisional in changes:
        with (ROOT / provisional).open(newline="", encoding="utf-8-sig") as stream:
            buyer_rows = list(csv.DictReader(stream))
        expected = {f"C05-F01-{index:03}" for index in range(1, 66)}
        need(len(buyer_rows) == 65 and {r["id"] for r in buyer_rows} == expected,
             "comprador preliminar: 65 IDs únicos obligatorios")
        for row in buyer_rows:
            need(row["calificacion_final_institucional"] == "PENDIENTE"
                 and row["dictamen_compraria_ahora"] == "NO_COMPRA_PROVISIONAL"
                 and 0 <= float(row["nota_exploratoria_0_10"]) < 9.5,
                 "comprador preliminar: no atribuir aprobación comercial ni nota final " + row["id"])
    model = text("frontend/src/app/ui/feedback/operation-feedback.model.ts")
    portal = text("frontend/src/app/pages/identity-portal.component.ts")
    operator = text("frontend/src/app/pages/platform-identity-panel.component.ts")
    modal = text("frontend/src/app/ui/institutional-modal/institutional-modal.component.html")
    for key in ("contextualFeedback", "origin !== current", "activityLabel", "readonly kind"):
        need(key in model, "missing feedback contract: " + key)
    for code in (portal, operator):
        need("visibleFeedback = computed(" in code and "feedbackScope.set(origin)" in code,
             "not all realms have contextual feedback")
        need("feedbackGeneration" in code and "generation !== this.feedbackGeneration" in code,
             "stale asynchronous response not invalidated")
        need("IdentityApiService.publicError(caught)" in code,
             "raw API failure may appear in notifications")
    need("cs-operation-feedback" in modal, "dialogs are not using semantic feedback")
    # The F03 regression must prove secondary recovery routes remain reachable
    # after the initial menu is collapsed, without concealing recovery methods.
    f03_navigation = text("frontend/tests/c05-f03-navigation.spec.ts")
    need("const disclosure = page.locator('details.account-more')" in f03_navigation and
         "await page.keyboard.press('Enter')" in f03_navigation,
         "F03 recovery keyboard access regression was weakened")
    need("secondaryRoutesOpen.set(false)" in portal and
         "secondaryRoutesToggled(event: Event)" in portal,
         "secondary account menu may persist open after a route change")
    inventory = text("frontend/tests/c05-f06-inventory-65.spec.ts")
    for marker in ("section.operator-workspace:visible > header.operator-section-head h2",
                   "wrong operator tab before screenshot",
                   "operator tab changed during screenshot capture"):
        need(marker in inventory, "65-surface inventory permits mislabeled operator captures: " + marker)
    guest_markup = text("frontend/src/app/pages/identity-portal.component.html")
    for task in ("invitacion", "acceso-existente", "confirmar", "recuperar"):
        need("guestView() === '" + task + "'" in guest_markup,
             "focused guest workflow may regress to unrelated login tabs: " + task)
    for template in ("frontend/src/app/pages/identity-portal.component.html",
                     "frontend/src/app/pages/platform-identity-panel.component.html"):
        markup = text(template)
        for expected in ("sessionEndedNotice()", "restoring()", 'kind="pending"', 'kind="info"'):
            need(expected in markup, "lost authentic session feedback in " + template + ": " + expected)
    # This regression previously escaped F06 when only shared E2E helpers
    # changed. Their visual selectors must remain aligned with the UI.
    for helper in ("qa/c05_passkey_browser_checks.mjs",
                   "qa/c05_pairing_browser_checks.mjs",
                   "qa/c05_profile_browser_checks.mjs"):
        source = text(helper)
        need("getByRole('heading', { name: 'Estado de la sesión'" not in source,
             "outdated account heading in real E2E helper: " + helper)
        need("getByRole('heading', { name: 'Tu sesión actual'" in source,
             "real E2E helper lacks current account heading: " + helper)
    passkeys = text("qa/c05_passkey_browser_checks.mjs")
    need("getByRole('heading', { name: 'Llaves disponibles'" in passkeys and
         "getByRole('heading', { name: 'Cambios protegidos'" in passkeys,
         "passkey E2E navigation must match current visible sections")
    need(len(changes) >= 15, "partial integration only")
    print("F06/A PASS: contextual async states, C05-only scope and Unicode text integrity")

def audit_b() -> None:
    scope()
    directive = text("frontend/src/app/ui/feedback/secure-field-feedback.directive.ts")
    modal = text("frontend/src/app/ui/institutional-modal/institutional-modal.component.ts")
    portal = text("frontend/src/app/pages/identity-portal.component.ts")
    operator = text("frontend/src/app/pages/platform-identity-panel.component.ts")
    for token in ("input.type !== 'password'", "one-time-code", "input.value = ''",
                  "invalid", "aria-invalid", "aria-describedby", "fieldValidationCopy",
                  "ngOnDestroy", "previousDescription", "this.form.noValidate = true",
                  "stopImmediatePropagation"):
        need(token in directive, "field privacy or accessibility missing: " + token)
    for code in (portal, operator):
        need("clearSensitiveInputValues(form);" in code and "this.feedbackScope.set('');" in code,
             "field cleanup or stale-message revocation missing")
    service = text("frontend/src/app/identity/identity-api.service.ts")
    platform_service = text("frontend/src/app/identity/platform-identity-api.service.ts")
    for source in (service, platform_service):
        for expected in ("readonly sessionEndedNotice = signal(false)",
                         "this.sessionEndedNotice.set(false);",
                         "this.clear(); this.sessionEndedNotice.set(true);"):
            need(expected in source, "session-ended notice not bound to real expiry/revocation: " + expected)
    need("this.identity.hideRecoveryCodes()" in portal and
         "showRecoveryCodes(result.recoveryCodes ?? [])" in service and
         "this.backupCodesTimer = setTimeout" in service and
         "this.hideRecoveryCodes();" in service,
         "new backup codes must remain visible in Resumen until saved, 5 minutes, or logout")
    need("this.platform.recoveryCodes.set([])" not in operator,
         "operator navigation must not destroy freshly issued backup codes")
    for path in ["frontend/src/app/ui/feedback/operation-feedback.model.ts",
                 "frontend/src/app/ui/feedback/operation-feedback.component.ts",
                 "frontend/src/app/ui/feedback/secure-field-feedback.directive.ts"]:
        source = text(path)
        for forbidden in ("localStorage", "sessionStorage", "innerHTML", "eval(", "fetch("):
            need(forbidden not in source, "unsafe feedback primitive: " + forbidden)
    need("this.busy()) return" in modal and "actionStartedHere()" in modal,
         "modal bypass or false progress")
    print("F06/B PASS: no raw server errors, form secret scrubbing, stale/realm guards")

def audit_c() -> None:
    scope()
    css = text("frontend/src/app/ui/feedback/operation-feedback.component.css")
    html = text("frontend/src/app/ui/feedback/operation-feedback.component.html")
    global_css = text("frontend/src/styles.css")
    test = text("frontend/tests/c05-f06-context-feedback.spec.ts")
    for needle in ("prefers-reduced-motion: reduce", "forced-colors: active",
                   "grid-template-columns", "@keyframes feedback-enter", "overflow-wrap"):
        need(needle in css, "missing UI/motion system: " + needle)
    for needle in ("aria-live", "aria-busy", "aria-atomic", "kind() === 'error'"):
        need(needle in html, "missing live-region contract: " + needle)
    need("cs-field-feedback" in global_css and "aria-invalid" in global_css, "field validation not styled")
    need("clip-path: inset(50%)" in text("frontend/src/app/pages/identity-portal.component.css"),
         "skip link must be visually clipped when not focused without losing keyboard access")
    for width in (320,390,768,1024,1440):
        need("width: " + str(width) in test, "visual viewport missing " + str(width))
    for premium_surface in ("guest-login", "guest-register", "guest-recovery",
                            "account-resumen", "account-perfil", "account-llaves",
                            "account-seguridad", "account-historial", "account-administracion",
                            "operator-casos", "operator-sesiones", "operator-perfil",
                            "operator-monitor", "operator-seguridad"):
        need(premium_surface in test, "premium screen not individually audited: " + premium_surface)
    for need_test in ("AxeBuilder", "reducedMotion: 'reduce'", "forcedColors: 'active'",
                      "font-size: 200%", "noHorizontalClip", "late response",
                      "scrubs password", "backup-code secrets"):
        need(need_test in test, "missing F06 regression: " + need_test)
    need("reuseExistingServer: false" in text("frontend/playwright.c05-f06-feedback.config.ts"),
         "test harness not isolated")
    # Editorial IA grade declarations must not be confused with a green
    # screenshot run. Every buyer row has an individual rationale and 5-width
    # evidence, and explicitly excludes human and backend-by-image claims.
    import csv as csv_module
    from c05_ux_f06_65_coverage import validate_commercial_ia_ratings
    with (ROOT / "docs/c05/ux/F06_65_SUPERFICIES_VALORACION.csv").open(
        newline="", encoding="utf-8") as buyer_stream:
        final_reviews = list(csv_module.DictReader(buyer_stream))
    buyer = validate_commercial_ia_ratings(final_reviews)
    need(buyer["accepted"] == 65 and buyer["minimum"] >= 9.5,
         "final 65 buyer grades not individually accepted")
    print("F06/C PASS: accessible validation, editorial consistency and reduced motion")

if __name__ == "__main__":
    try:
        need(len(sys.argv) == 2 and sys.argv[1] in ("A","B","C"), "select audit A/B/C")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (OSError, AssertionError, subprocess.CalledProcessError) as error:
        print(error, file=sys.stderr)
        raise SystemExit(1) from error
