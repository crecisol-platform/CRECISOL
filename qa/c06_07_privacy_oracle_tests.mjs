import test from 'node:test';
import assert from 'node:assert/strict';
import { EventEmitter } from 'node:events';
import { inspectPrivacyResponse, attachPrivacyBrowser } from './c06_07_privacy_response_checks.mjs';

const op='mfa/enroll', cache='no-store, private';
const raw=JSON.stringify({secret:'SYNTHETIC',uri:'otpauth://totp/test'});
const marker='synthetic-sensitive-value';
const headers={'x-crecisol-browser':'1'};
function response(status,body,finished=async()=>null) {
  return {url:()=>'http://localhost/api/v1/identity/'+op,status:()=>status,
    finished,body,headers:()=>({'cache-control':cache}),request:()=>({headers:()=>headers})};
}
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
test('browser drain waits for actual capture before navigation',async()=>{
  const context=new EventEmitter(), audit=attachPrivacyBrowser(context);
  let release, completed=false;
  const gate=new Promise(resolve=>{release=resolve;});
  context.emit('response',response(200,async()=>{await gate;return Buffer.from(raw);}));
  const draining=audit.drain().then(()=>{completed=true;});
  await new Promise(resolve=>setImmediate(resolve));
  assert.equal(completed,false);
  release(); await draining; assert.equal(completed,true);
  await audit.finish(); assert.equal(context.listenerCount('response'),0);
});
test('browser capture failure is distinct and redacted for success and denial',async()=>{
  for(const status of [200,401]) {
    const context=new EventEmitter(), audit=attachPrivacyBrowser(context);
    context.emit('response',response(status,async()=>{throw new Error(marker);}));
    await assert.rejects(audit.finish(),redacted(/wire capture unavailable: mfa\/enroll \[browser HTTP/));
    assert.equal(context.listenerCount('response'),0);
  }
});
test('browser body that was captured but invalid fails as a wire contract',async()=>{
  const context=new EventEmitter(),audit=attachPrivacyBrowser(context);
  context.emit('response',response(200,async()=>Buffer.from(marker)));
  await assert.rejects(audit.finish(),redacted(/wire contract violation: mfa\/enroll \[browser HTTP 200\] invalid JSON/));
});
test('non-UTF8 payload cannot become a passing denial',async()=>{
  const context=new EventEmitter(),audit=attachPrivacyBrowser(context);
  context.emit('response',response(401,async()=>Buffer.from([0xff])));
  await assert.rejects(audit.finish(),/wire capture unavailable/);
});
