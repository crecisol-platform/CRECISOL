#!/usr/bin/env python3
"""C03-05 exact DTO wire contract and fail-closed HTTP against real disposable MySQL/.NET."""
import http.client
import json
import sys

MODE = sys.argv[1] if len(sys.argv) > 1 else "baseline"
PORT = int(sys.argv[2]) if len(sys.argv) > 2 else 5184
PATH = "/api/v1/public/branding/platform"
TOP = {
    "revision", "name", "slogan", "descriptor", "primaryColor", "accentColor",
    "logoUrl", "websiteUrl", "publishedAt", "presentation"
}
PRESENTATION = {
    "faviconUrl", "typographyToken", "iconSetToken",
    "seoTitle", "seoDescription", "copyrightNotice"
}
checks = 0


def call(path=PATH, method="GET", headers=None, body=None):
    connection = http.client.HTTPConnection("127.0.0.1", PORT, timeout=6)
    connection.request(method, path, body=body, headers=headers or {})
    response = connection.getresponse()
    payload = response.read().decode("utf-8", "replace")
    meta = {name.lower(): value for name, value in response.getheaders()}
    code = response.status
    connection.close()
    return code, meta, payload


def expect(label, code, path=PATH, method="GET", headers=None, body=None):
    global checks
    status, meta, payload = call(path, method, headers, body)
    if status != code:
        raise AssertionError(f"{label}: expected {code}, got {status} ({payload[:120]})")
    if "no-store" not in meta.get("cache-control", ""):
        raise AssertionError(f"{label}: missing no-store")
    if meta.get("x-content-type-options") != "nosniff":
        raise AssertionError(f"{label}: missing nosniff")
    if meta.get("access-control-allow-origin") or meta.get("set-cookie"):
        raise AssertionError(f"{label}: unapproved CORS/cookie response")
    if code != 200:
        if payload or "PRIVATE_TENANT_B" in payload or "MySql" in payload:
            raise AssertionError(f"{label}: denied response must be empty")
    else:
        if not meta.get("content-type", "").startswith("application/json"):
            raise AssertionError(f"{label}: missing JSON media type")
        item = json.loads(payload)
        if set(item) != TOP:
            raise AssertionError(f"{label}: public DTO key mismatch: {set(item) ^ TOP}")
        if item["name"] != "CRECISOL" or item["revision"] != 2:
            raise AssertionError(f"{label}: incorrect published platform data")
        if item["primaryColor"] != "#062E65" or item["websiteUrl"] is not None:
            raise AssertionError(f"{label}: incorrect mapped values")
        if not (item["publishedAt"].endswith("Z") or
                item["publishedAt"].endswith("+00:00")):
            raise AssertionError(f"{label}: timestamp is not UTC")
        if not isinstance(item["presentation"], dict) or set(item["presentation"]) != PRESENTATION:
            raise AssertionError(f"{label}: presentation DTO mismatch")
        if item["presentation"]["copyrightNotice"] is not None:
            raise AssertionError(f"{label}: optional field must remain null")
        if "PRIVATE_TENANT_B" in payload or "password" in payload.lower():
            raise AssertionError(f"{label}: tenant data/secret leak")
    checks += 1
    print(f"PASS C03-05 HTTP {status}: {label}")


if MODE == "baseline":
    expect("exact published PLATFORM DTO", 200)
    expect("untrusted cookie gives no tenant context", 200, headers={"Cookie": "tenant=22"})
    expect("no CORS even on untrusted Origin", 200, headers={"Origin": "https://attacker.invalid"})
    expect("scope query rejected", 400, path=PATH + "?scope=tenant")
    expect("empty scope value rejected", 400, path=PATH + "?scope=")
    expect("tenant header rejected", 400, headers={"X-Tenant-Id": "22"})
    expect("authorization header rejected", 400, headers={"Authorization": "Bearer fake"})
    expect("untrusted Host rejected", 400, headers={"Host": "tenant.attacker.invalid"})
    expect("forwarded host rejected", 400, headers={"X-Forwarded-Host": "tenant.invalid"})
    expect("standard forwarded host rejected", 400, headers={"Forwarded": "host=tenant.invalid"})
    expect("GET request body rejected", 400, body="probe")
    expect("explicit empty body marker rejected", 400, headers={"Content-Length": "0"})
    expect("method override rejected", 400, headers={"X-HTTP-Method-Override": "POST"})
    expect("original URL header rejected", 400, headers={"X-Original-URL": "/api/v1/admin"})
    expect("rewrite URL header rejected", 400, headers={"X-Rewrite-URL": "/api/v1/tenant"})
    for method in ("POST", "PUT", "PATCH", "DELETE", "OPTIONS", "HEAD"):
        expect("no private/write verb " + method, 405, method=method)
    expect("no tenant branding route", 404, path="/api/v1/tenant/branding")
    expect("no admin branding route", 404, path="/api/v1/admin/branding")
    expect("no preview route", 404, path=PATH + "/preview")
elif MODE == "absent":
    expect("missing PLATFORM fails with 404 without tenant fallback", 404)
elif MODE == "invalid":
    expect("invalid Domain model fails closed with 503", 503)
elif MODE == "unconfigured":
    expect("missing server DSN fails closed with 503", 503)
else:
    raise SystemExit("Unsupported mode")
print(f"PASS C03-05 HTTP {MODE}: {checks} real requests")
