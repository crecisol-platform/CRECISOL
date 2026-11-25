// Independent wire oracle: never print response bodies, values or credentials.
import { readFileSync } from 'node:fs';
const contract=JSON.parse(readFileSync(new URL('../docs/c06/C06_07_CONTRATOS_RESPUESTA.json',import.meta.url),'utf8'));
let checked=0, browserChecked=0, relayBrowserChecked=0, relayEnrollments=0;
const seen=new Set();
function demand(ok,op) { if(!ok) throw new Error('C06-07 wire contract violation: '+op); }
function object(value) { return value!==null && typeof value==='object' && !Array.isArray(value); }
function exact(value,keys,op) {
  demand(object(value),op+' expected object');
  const actual=Object.keys(value);
  demand(actual.length===keys.length && actual.every(k=>keys.includes(k)),op+' unexpected fields');
}
function subset(value,keys,op) {
  demand(object(value),op+' expected object');
  demand(Object.keys(value).every(k=>keys.includes(k)),op+' unexpected fields');
}
function items(value,keys,op) { demand(Array.isArray(value),op); for(const item of value) exact(item,keys,op); }
export function inspectPrivacyResponse(op,status,raw,cacheControl,extra={},source='direct') {
  if(!Object.hasOwn(contract.operations,op)) return;
  const rule=contract.operations[op];
  const label=op+' ['+source+' HTTP '+status+']';
  demand(typeof raw==='string',label+' body unavailable');
  let data=null;
  if(raw.length!==0) {
    try { data=JSON.parse(raw); } catch { demand(false,label+' invalid JSON'); }
  }
  const directives=typeof cacheControl==='string'?cacheControl.toLowerCase().split(',').map(x=>x.trim()):[];
  demand(directives.includes('no-store')&&directives.includes('private'),label+' cache');
  const browser=Object.entries(extra).some(([key,value])=>key.toLowerCase()==='x-crecisol-browser'&&value==='1');
  const counted=()=>{checked++; if(browser){browserChecked++; if(source==='relay') relayBrowserChecked++;}};
  if(status>=400) { demand(data===null || (object(data) && Object.keys(data).length===0),label+' denial'); counted(); return; }
  demand(status===200 || status===202,label+' unexpected status');
  const fields=browser&&rule.browser_fields?rule.browser_fields:(rule.alternative_fields&&data?.realm==='PLATFORM_OPERATOR'?rule.alternative_fields:rule.fields);
  exact(data,fields,label);
  if(browser) demand(!('sessionToken' in data),label+' browser credential');
  if(rule.shape==='sessionOnly') demand(data.recoveryCodes===null,label+' unexpected recovery codes');
  const n=contract.nested;
  if(rule.shape==='sessions'&&!(browser&&rule.browser_fields)) items(data.sessions,n.tenantSessions,label);
  if(rule.shape==='platformSessions') items(data.sessions,n.platformSessions,label);
  if(rule.shape==='pending') {
    items(data.requests,n.pending,label);
    for(const row of data.requests) demand(row.mailboxHint==='Correo verificado; identidad no disponible'||/^.{1,2}•••@[^@]+$/u.test(row.mailboxHint),label+' mask');
  }
  if(rule.shape==='passkeys') items(data.passkeys,n.passkeys,label);
  if(rule.shape==='events') items(data.events,n.events,label);
  if(rule.shape==='signals') items(data.signals,n.signals,label);
  if(rule.shape==='assertion'||rule.shape==='registration') {
    const w=n.webAuthn,p=data.publicKey;
    subset(p,w[rule.shape],label+' protocol');
    if(p.user) exact(p.user,w.user,label+' user');
    if(p.rp) exact(p.rp,w.rp,label+' rp');
    if(p.pubKeyCredParams) items(p.pubKeyCredParams,w.parameter,label+' parameters');
    for(const item of [...(p.allowCredentials||[]),...(p.excludeCredentials||[])]) subset(item,w.descriptor,label+' descriptor');
    if(p.authenticatorSelection) subset(p.authenticatorSelection,w.selection,label+' selection');
  }
  if(source==='relay'&&browser&&op==='mfa/enroll') relayEnrollments++;
  seen.add(op); counted();
}
export function reportPrivacyResponses() {
  demand(checked>=60 && seen.size>=18 && browserChecked>=10 && relayBrowserChecked>=10 && relayEnrollments>=2,'coverage');
  console.log('PASS C06-07 HTTP response privacy checks='+checked+' successful operation shapes='+seen.size+' browser-header responses='+browserChecked+' real browser-path relay responses='+relayBrowserChecked+' MFA enrollments='+relayEnrollments);
}
