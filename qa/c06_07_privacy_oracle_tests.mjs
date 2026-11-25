import test from 'node:test';
import assert from 'node:assert/strict';
import { inspectPrivacyResponse } from './c06_07_privacy_response_checks.mjs';

const op='mfa/enroll', cache='no-store, private';
const raw=JSON.stringify({secret:'SYNTHETIC',uri:'otpauth://totp/test'});
const marker='synthetic-sensitive-value';
const headers={'x-crecisol-browser':'1'};
function redacted(pattern) {
  return error=>pattern.test(error.message)&&!error.message.includes(marker);
}

test('valid successful response and empty denial',()=>{
  assert.doesNotThrow(()=>inspectPrivacyResponse(op,200,raw,cache));
  assert.doesNotThrow(()=>inspectPrivacyResponse(op,401,'',cache));
  assert.doesNotThrow(()=>inspectPrivacyResponse(op,401,'{}',cache));
});
test('missing response body never becomes a passing denial',()=>{
  assert.throws(()=>inspectPrivacyResponse(op,401,null,cache),/body unavailable/);
});
test('malformed JSON is rejected and never appears in diagnostics',()=>{
  for(const status of [200,401]) assert.throws(()=>inspectPrivacyResponse(op,status,marker,cache),redacted(/invalid JSON/));
});
test('denial cannot expose content or be an array',()=>{
  for(const body of ['[]',JSON.stringify({value:marker})]) assert.throws(()=>inspectPrivacyResponse(op,401,body,cache),redacted(/denial/));
});
test('extra fields and missing fields remain fail closed',()=>{
  for(const body of ['{}',JSON.stringify({...JSON.parse(raw),value:marker}),'{"secret|uri":"synthetic"}']) assert.throws(()=>inspectPrivacyResponse(op,200,body,cache),redacted(/unexpected fields/));
});
test('both cache directives are mandatory',()=>{
  for(const directive of ['private','no-store','x-no-store, private']) assert.throws(()=>inspectPrivacyResponse(op,200,raw,directive),/cache/);
});

test('browser restore accepts only its separate closed receipt contract',()=>{
  const good=JSON.stringify({csrfToken:'synthetic',expiresAtUtc:'2026-11-25T14:00:00Z',idleMinutes:30});
  assert.doesNotThrow(()=>inspectPrivacyResponse('browser/restore',200,good,cache,headers,'relay'));
  assert.throws(()=>inspectPrivacyResponse('browser/restore',200,JSON.stringify({...JSON.parse(good),sessions:[]}),cache,headers,'relay'),/unexpected fields/);
  assert.throws(()=>inspectPrivacyResponse('sessions',200,JSON.stringify({expiresAtUtc:'2026-11-25T14:00:00Z'}),cache,headers,'relay'),/unexpected fields/);
});
test('redirects and uncontracted successful statuses cannot pass',()=>{
  for(const status of [201,204,301,302]) assert.throws(()=>inspectPrivacyResponse(op,status,raw,cache,headers,'relay'),/unexpected status/);
});
