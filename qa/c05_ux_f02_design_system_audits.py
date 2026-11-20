#!/usr/bin/env python3
"""CRECISOL C05 UX F02 source audits, distinct from executed Chromium QA."""
import json
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
SYSTEM = ROOT / "frontend/src/app/ui/institutional-system"
FILES = (
    "institutional-icon", "institutional-action", "institutional-card",
    "institutional-navigation", "institutional-field", "institutional-table",
    "institutional-notice", "institutional-state"
)


def check(condition: bool, reason: str) -> None:
    if not condition:
        raise AssertionError("C05 UX F02 NO-GO: " + reason)


def file(path: Path) -> str:
    check(path.is_file(), "missing file " + str(path))
    return path.read_text(encoding="utf-8")


def audit_a() -> None:
    j = json.loads(file(ROOT / "frontend/angular.json"))
    build = j["projects"]["crecisol-web"]["architect"]["build"]
    check(build["options"]["styles"] == [
        "src/app/ui/institutional-system/institutional-tokens.css",
        "src/styles.css"
    ], "design tokens not available to production and harness")
    check("institutional-harness" in build["configurations"],
          "missing isolated Angular harness build")
    check("institutional-harness" in
          j["projects"]["crecisol-web"]["architect"]["serve"]["configurations"],
          "missing synthetic-only serve target")
    for name in FILES:
        typescript = file(SYSTEM / (name + ".component.ts"))
        html = file(SYSTEM / (name + ".component.html"))
        css = file(SYSTEM / (name + ".component.css"))
        check("standalone: true" in typescript and "ChangeDetectionStrategy.OnPush" in typescript,
              "standalone OnPush component required: " + name)
        check("templateUrl:" in typescript and "styleUrl:" in typescript,
              "external component template/styles required: " + name)
        check(bool(html.strip()) and bool(css.strip()), "empty template/style: " + name)
    check(file(SYSTEM / "index.ts").count("export { Institutional") >= 8,
          "single module barrel incomplete")
    theme = file(SYSTEM / "institutional-theme.directive.ts")
    check("PublishedPlatformBrand" in theme and "validatedColor" in theme,
          "published theme contract or hex validator missing")
    check((ROOT / "frontend/tests/institutional-harness/main.ts").is_file(),
          "synthetic harness missing")
    print("F02/A static PASS: 8 OnPush primitives, semantic tokens, published theme, isolated build")


def rgb(color: str) -> tuple[int, int, int]:
    return tuple(int(color[i:i+2], 16) for i in (1, 3, 5))


def luminance(color: str) -> float:
    linear = []
    for value in rgb(color):
        v = value / 255
        linear.append(v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4)
    return .2126 * linear[0] + .7152 * linear[1] + .0722 * linear[2]


def contrast(a: str, b: str) -> float:
    hi, lo = sorted([luminance(a), luminance(b)], reverse=True)
    return (hi + .05) / (lo + .05)


def audit_b() -> None:
    token = file(SYSTEM / "institutional-tokens.css")
    palette = dict(re.findall(r'(--cs-color-[a-z-]+):\s*(#[0-9a-fA-F]{6})\s*;', token))
    pairs = (
        ("text", "surface"), ("muted", "surface"), ("text-strong", "surface"),
        ("action-text", "action"), ("success", "success-bg"),
        ("warning", "warning-bg"), ("danger", "danger-bg"),
        ("info", "info-bg"), ("accent-text", "accent-soft")
    )
    for fg, bg in pairs:
        check(fg in palette and bg in palette,
              "missing semantic color " + fg + "/" + bg)
        ratio = contrast(palette[fg], palette[bg])
        check(ratio >= 4.5,
              "WCAG AA normal-text contrast <4.5 for " + fg + "/" + bg +
              ": " + str(round(ratio, 2)))
    ui = "\n".join(file(p) for p in SYSTEM.glob("*.ts"))
    html = "\n".join(file(p) for p in SYSTEM.glob("*.html"))
    html = html.replace('xmlns="http://www.w3.org/2000/svg"', "")
    for bad in ("innerHTML", "bypassSecurityTrust", "localStorage", "sessionStorage",
                "document.cookie", "eval(", "fetch(", "XMLHttpRequest",
                "http://", "https://", "window.open("):
        check(bad not in ui and bad not in html,
              "presentation system contains forbidden executable or external behavior: " + bad)
    table = file(SYSTEM / "institutional-table.component.ts")
    check("this.scopeKey() === this.rowsScopeKey()" in table,
          "tenant-context stale row guard absent")
    check("visibleRows" in table and "validColumns" in table,
          "bounded, validated tabular display missing")
    theme = file(SYSTEM / "institutional-theme.directive.ts")
    check(r"/^#[0-9a-fA-F]{6}$/" in theme, "hex-only color sanitizer missing")
    check("decorative" in theme.lower() and "PublishedPlatformBrand" in theme,
          "color changes must remain decorative and published")
    print("F02/B static PASS: AA color pairs, no remote/decrypted data, scoped read-only table")


def audit_c() -> None:
    token = file(SYSTEM / "institutional-tokens.css")
    must = ("--cs-font-body", "--cs-text-heading", "--cs-space-8",
            "--cs-radius-lg", "--cs-shadow-card", "--cs-motion-fast",
            "--cs-color-focus", "--cs-color-action", "--cs-color-danger")
    for name in must:
        check(name in token, "missing design token " + name)
    check("@media (forced-colors: active)" in token and
          "@media (prefers-reduced-motion: reduce)" in token,
          "accessibility token variants missing")
    styles = "\n".join(file(SYSTEM / (name + ".component.css")) for name in FILES)
    check(":focus-visible" in styles and "@media (forced-colors: active)" in styles,
          "visible focus or forced-color states missing")
    test = file(ROOT / "frontend/tests/c05-f02-design-system.spec.ts")
    for width in ("320", "390", "768", "1024", "1440"):
        check("width: " + width in test, "missing viewport: " + width)
    for requirement in ("200%", "forcedColors", "reducedMotion",
                        "invalid published colors", "scope switch", "assertA11y"):
        check(requirement in test, "missing visual regression test: " + requirement)
    config = file(ROOT / "frontend/playwright.institutional.config.ts")
    check("4202" in config and "reuseExistingServer: false" in config,
          "independent Angular browser laboratory must not reuse a production server")
    routes = file(ROOT / "frontend/src/app/app.routes.ts")
    check("institutional-harness" not in routes, "synthetic lab exposed to production routing")
    print("F02/C static PASS: tokens, responsive paths, forced-colors, reduced motion and isolated QA")


if __name__ == "__main__":
    try:
        check(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"), "usage: A|B|C")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (AssertionError, KeyError, OSError, ValueError) as error:
        print("FAIL:", error, file=sys.stderr)
        raise SystemExit(1) from error
