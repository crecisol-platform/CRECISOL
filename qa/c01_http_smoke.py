#!/usr/bin/env python3
"""Local-only HTTP contract checks for C01; no external host or production access."""
import json
import time
import urllib.error
import urllib.parse
import urllib.request

BASE = "http://127.0.0.1:5180"
assert urllib.parse.urlparse(BASE).hostname == "127.0.0.1"
PATH = "/api/v1/public/branding/platform"

def request(path: str, method: str = "GET", headers: dict | None = None):
    query = urllib.request.Request(BASE + path, method=method, headers=headers or {})
    try:
        with urllib.request.urlopen(query, timeout=2) as response:
            return response.status, response.headers, response.read()
    except urllib.error.HTTPError as error:
        return error.code, error.headers, error.read()

for attempt in range(48):
    try:
        status, headers, body = request(PATH)
        break
    except urllib.error.URLError:
        if attempt == 47:
            raise
        time.sleep(0.25)
else:
    raise AssertionError("C01 API did not start on 127.0.0.1")

assert status == 200, f"Expected 200, got {status}"
payload = json.loads(body.decode("utf-8"))
fields = {
    "revision", "name", "slogan", "descriptor", "primaryColor",
    "accentColor", "logoUrl", "copyrightText"
}
assert set(payload) == fields, f"Unexpected public DTO fields: {sorted(set(payload) ^ fields)}"
assert payload["name"] == "CRECISOL"
assert payload["slogan"] == "Tu negocio, a tu manera."
assert payload["revision"] >= 1
assert payload["logoUrl"] == "/assets/crecisol-logo-oficial.png"
assert headers.get("Cache-Control") == "no-store"
assert headers.get("X-Content-Type-Options") == "nosniff"
assert "Set-Cookie" not in headers

status, injected_headers, injected_body = request(
    PATH + "?tenant_id=00000000-0000-0000-0000-000000000001")
assert status == 200 and json.loads(injected_body) == payload, (
    "Browser-controlled tenant_id must not alter public PLATFORM identity")
assert injected_headers.get("Cache-Control") == "no-store"

status, headers, body = request(PATH, "POST")
assert status == 405, "Anonymous requests cannot change published branding"
status, headers, body = request("/api/v1/admin/branding/platform")
assert status == 404, "Administrative branding endpoint must not be exposed"

status, headers, body = request(
    PATH, headers={"Origin": "https://untrusted.example.invalid"})
assert status == 200, "Published platform info is public"
assert headers.get("Access-Control-Allow-Origin") is None, (
    "Untrusted web origins must not receive CORS grant")
status, headers, body = request(
    PATH, headers={"Origin": "http://localhost:4200"})
assert status == 200
assert headers.get("Access-Control-Allow-Origin") == "http://localhost:4200", (
    "Only configured local development UI should receive CORS grant")

print("C01 HTTP smoke passed: public DTO, no-store, no cookies,")
print("tenant query isolation of platform identity, method/route denial and CORS.")
print("This does NOT demonstrate authenticated tenant isolation.")
