#!/usr/bin/env python3
"""C01-04 HTTP security smoke tests against LOCAL-only real ASP.NET API."""
import http.client
import json
import sys

MODE = sys.argv[1] if len(sys.argv) > 1 else "baseline"
PORT = int(sys.argv[2]) if len(sys.argv) > 2 else 5184
PATH = "/api/v1/public/branding/platform"
checks = 0


def request(method="GET", path=PATH, headers=None):
    connection = http.client.HTTPConnection("127.0.0.1", PORT, timeout=5)
    connection.request(method, path, headers=headers or {})
    response = connection.getresponse()
    body = response.read().decode("utf-8", "replace")
    result = (response.status, {name.lower(): value for name, value in response.getheaders()}, body)
    connection.close()
    return result


def expect(label, code, method="GET", path=PATH, headers=None, verify=None):
    global checks
    actual, received, body = request(method, path, headers)
    if actual != code:
        raise AssertionError(f"{label}: expected HTTP {code}, got {actual}.")
    if "no-store" not in received.get("cache-control", ""):
        raise AssertionError(f"{label}: no-store cache policy absent")
    if received.get("x-content-type-options") != "nosniff":
        raise AssertionError(f"{label}: browser MIME protection absent")
    if received.get("referrer-policy") != "no-referrer":
        raise AssertionError(f"{label}: referrer protection absent")
    if received.get("access-control-allow-origin"):
        raise AssertionError(f"{label}: unexpected CORS access")
    if received.get("set-cookie"):
        raise AssertionError(f"{label}: API must not set a session cookie")
    if "PRIVATE_TENANT_B" in body or "password" in body.lower():
        raise AssertionError(f"{label}: private details leaked")
    if verify:
        verify(received, body)
    checks += 1
    print(f"PASS HTTP {code}: {label}")


def verify_published(headers, body):
    if not headers.get("content-type", "").startswith("application/json"):
        raise AssertionError("Successful brand needs JSON content type")
    result = json.loads(body)
    if result.get("name") != "CRECISOL" or result.get("revision") != 2:
        raise AssertionError("Not the published PLATFORM revision")
    if result.get("primaryColor") != "#062E65":
        raise AssertionError("Platform colors were not read from DB")
    if result.get("publishedAt", "")[-6:] != "+00:00" and not result.get(
        "publishedAt", "").endswith("Z"):
        raise AssertionError("Publication timestamp is not UTC")
    if not result.get("presentation") or result["presentation"].get("typographyToken") != "system":
        raise AssertionError("Published presentation absent")
    for forbidden in ("scope", "scopeId", "tenantId", "status", "draft", "connectionString"):
        if forbidden in result:
            raise AssertionError("Internal field exposed: " + forbidden)


def verify_empty(headers, body):
    if "<" in body or "PRIVATE_TENANT_B" in body or "at MySql" in body:
        raise AssertionError("Untrusted data leaked in denied response")


if MODE == "baseline":
    expect("published PLATFORM only", 200, verify=verify_published)
    expect("untrusted origin never opens CORS", 200,
           headers={"Origin": "https://attacker.invalid"}, verify=verify_published)
    expect("cookie ignored; no private session", 200,
           headers={"Cookie": "tenant_id=2222"}, verify=verify_published)
    expect("unapproved Host denied", 400,
           headers={"Host": "tenant.attacker.invalid"}, verify=verify_empty)
    expect("forwarded Host spoof denied", 400,
           headers={"X-Forwarded-Host": "tenant.attacker.invalid"}, verify=verify_empty)
    expect("standard Forwarded Host denied", 400,
           headers={"Forwarded": "host=tenant.attacker.invalid"}, verify=verify_empty)
    expect("tenant id header denied", 400,
           headers={"X-Tenant-Id": "22222222-2222-2222-2222-222222222222"}, verify=verify_empty)
    expect("query scope injection denied", 400,
           path=PATH + "?scope=tenant&tenant_id=2222", verify=verify_empty)
    expect("fake browser identity denied", 400,
           headers={"Authorization": "Bearer fake-token"}, verify=verify_empty)
    expect("tenant branding route not exposed", 404,
           path="/api/v1/tenant/branding", verify=verify_empty)
    expect("admin branding route not exposed", 404,
           path="/api/v1/admin/branding", verify=verify_empty)
    expect("preview route not exposed", 404,
           path="/api/v1/public/branding/platform/preview", verify=verify_empty)
    for method in ("POST", "PUT", "PATCH", "DELETE"):
        expect("write verb denied " + method, 405, method=method, verify=verify_empty)
    expect("CORS preflight not enabled", 405, method="OPTIONS",
           headers={"Origin": "https://attacker.invalid",
                    "Access-Control-Request-Method": "GET"}, verify=verify_empty)
elif MODE == "absent":
    expect("no platform publication means 404, no fallback", 404, verify=verify_empty)
elif MODE == "invalid":
    expect("invalid platform content rejected by Domain", 503, verify=verify_empty)
elif MODE == "unconfigured":
    expect("no server-side database configuration fails closed", 503, verify=verify_empty)
else:
    raise ValueError("Unknown smoke mode")

print(f"C01-04 HTTP {MODE} PASS: {checks} actual requests.")
