// Independent wire oracle: never print response bodies, values or credentials.
import { readFileSync } from 'node:fs';
const contract=JSON.parse(readFileSync(new URL('../docs/c06/C06_07_CONTRATOS_RESPUESTA.json',import.meta.url),'utf8'));
let checked=0, browserChecked=0;
const seen=new Set();
function demand(ok,op) { if(!ok) throw new Error('C06-07 wire contract violation: '+op); }
function exact(value,keys,op) {
  demand(value && typeof value==='object' && !Array.isArray(value),op);
  demand(Object.keys(value).sort().join('|')===[...keys].sort().join('|'),op);
}
function subset(value,keys,op) {
  demand(value && typeof value==='object' && !Array.isArray(value),op);
  demand(Object.keys(value).every(k=>keys.includes(k)),op);
}
function items(value,keys,op) { demand(Array.isArray(value),op); for(const item of value) exact(item,keys,op); }
export function inspectPrivacyResponse(op,status,data,cacheControl,extra={}) {
  const rule=contract.operations[op]; if(!rule) return;
  demand(typeof cacheControl==='string' && cacheControl.includes('no-store'),op+' cache');
  const browser=Object.entries(extra).some(([key,value])=>key.toLowerCase()==='x-crecisol-browser'&&value==='1');
  if(status>=400) { demand(data===null || (typeof data==='object' && Object.keys(data).length===0),op+' denial'); checked++; if(browser) browserChecked++; return; }
  if(status!==200 && status!==202) return;
  const fields=browser&&rule.browser_fields?rule.browser_fields:(rule.alternative_fields&&data?.realm==='PLATFORM_OPERATOR'?rule.alternative_fields:rule.fields);
  exact(data,fields,op);
  if(browser) demand(!('sessionToken' in data),op+' browser credential');
  if(rule.shape==='sessionOnly') demand(data.recoveryCodes===null,op+' unexpected recovery codes');
  const n=contract.nested;
  if(rule.shape==='sessions'&&Array.isArray(data.sessions)) items(data.sessions,n.tenantSessions,op);
  if(rule.shape==='platformSessions') items(data.sessions,n.platformSessions,op);
  if(rule.shape==='pending') {
    items(data.requests,n.pending,op);
    for(const row of data.requests) demand(row.mailboxHint==='Correo verificado; identidad no disponible'||/^.{1,2}•••@[^@]+$/u.test(row.mailboxHint),op+' mask');
  }
  if(rule.shape==='passkeys') items(data.passkeys,n.passkeys,op);
  if(rule.shape==='events') items(data.events,n.events,op);
  if(rule.shape==='signals') items(data.signals,n.signals,op);
  if(rule.shape==='assertion'||rule.shape==='registration') {
    const w=n.webAuthn,p=data.publicKey;
    subset(p,w[rule.shape],op+' protocol');
    if(p.user) exact(p.user,w.user,op+' user');
    if(p.rp) exact(p.rp,w.rp,op+' rp');
    if(p.pubKeyCredParams) items(p.pubKeyCredParams,w.parameter,op+' parameters');
    for(const item of [...(p.allowCredentials||[]),...(p.excludeCredentials||[])]) subset(item,w.descriptor,op+' descriptor');
    if(p.authenticatorSelection) subset(p.authenticatorSelection,w.selection,op+' selection');
  }
  seen.add(op); checked++; if(browser) browserChecked++;
}
export function reportPrivacyResponses() {
  demand(checked>=60 && seen.size>=18 && browserChecked>=10,'coverage');
  console.log('PASS C06-07 HTTP response privacy checks='+checked+' successful operation shapes='+seen.size+' browser responses='+browserChecked);
}
export function attachPrivacyBrowser(context) {
  const pending=[]; let failure;
  const onResponse=response=>{
    const url=new URL(response.url());
    const prefix=url.pathname.startsWith('/api/v1/platform-identity/')?'/api/v1/platform-identity/':url.pathname.startsWith('/api/v1/identity/')?'/api/v1/identity/':null;
    if(!prefix) return;
    const op=(prefix.includes('platform-identity')?'platform/':'')+url.pathname.slice(prefix.length);
    if(!contract.operations[op]) return;
    pending.push((async()=>{
      const data=await response.json().catch(()=>null);
      inspectPrivacyResponse(op,response.status(),data,response.headers()['cache-control'],response.request().headers());
    })().catch(error=>{failure??=error;}));
  };
  context.on('response',onResponse);
  return async()=>{context.off('response',onResponse); await Promise.all(pending); if(failure) throw failure;};
}
