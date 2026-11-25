// Real HTTP, no intercepted responses, disposable fixtures only.
import { request } from 'node:http';
import { randomBytes, randomUUID } from 'node:crypto';
const hex=()=>randomUUID().replaceAll('-','');
function raw(port,path,method,headers={},body='') {
  return new Promise((resolve,reject)=>{
    const q=request({hostname:'127.0.0.1',port,path,method,agent:false,
      headers:{Host:'tenant-a.example.test',...(method==='POST'?{'Content-Type':'application/json','Content-Length':Buffer.byteLength(body)}:{}),...headers}},r=>{
      let data='';r.setEncoding('utf8');r.on('data',v=>data+=v);
      r.on('end',()=>resolve({status:r.statusCode,headers:r.headers,body:data}));
      r.on('error',()=>reject(new Error('C06-09 response unavailable')));
    });
    q.setTimeout(30000,()=>q.destroy(new Error('C06-09 HTTP deadline')));
    q.on('error',()=>reject(new Error('C06-09 isolated HTTP request failed')));q.end(body);
  });
}
function neutral(r,expected) {
  const cache=String(r.headers['cache-control']||'').toLowerCase().split(',').map(v=>v.trim());
  return r.status===expected && (r.body===''||r.body==='{}') && !r.headers.location &&
    !r.headers['content-disposition'] && cache.includes('private') && cache.includes('no-store') &&
    r.headers['cross-origin-resource-policy']==='same-origin';
}
export async function verifyDeliveryBoundary({api,check,sql,A,actor,token,apiPort,confidential,reserveTestWindow}) {
  await reserveTestWindow(15);
  const canary='C06_09_'+randomBytes(12).toString('hex');
  confidential.push(canary);
  const resource=randomUUID(), signed='?signature='+canary+'&token='+token;
  const credentials={Authorization:'Bearer '+token,'User-Agent':canary,'Referer':'https://example.test/'+canary};
  // A nonregistered action stays absent even with a real authenticated actor.
  // These are adversarial probes, not claims that a storage service exists.
  for(const target of [
    '/api/v1/private/cases/'+resource,
    '/api/v1/private/media/'+resource,
    '/api/v1/media/'+resource,
    '/api/v1/downloads/'+resource,
    '/api/v1/exports/'+resource,
    '/api/v1/logs/'+resource,
    '/api/v1/identity/exports/'+resource,
    '/api/v1/platform-identity/exports/'+resource
  ]) {
    check(neutral(await raw(apiPort,target+signed,'GET',credentials),404),'C06-09 unregistered delivery surface denies URL and bearer');
  }
  check(neutral(await raw(apiPort,'/api/v1/private/'+canary+signed,'GET',{...credentials,Host:'tenant-b.example.test'}),404),'C06-09 foreign host signed URL stays closed');
  check(neutral(await raw(apiPort,'/api/v1/private/'+canary+signed,'GET',{...credentials,Host:'evil.example.test'}),400),'C06-09 ingress denies invalid host without leaking URL');
  check(neutral(await raw(apiPort,'/api/v1/identity/profile/photo'+signed,'GET',credentials),405),'C06-09 own photo cannot be retrieved by a GET or signed link');
  check(neutral(await raw(apiPort,'/api/v1/identity/profile/photo'+signed,'POST',credentials,'{}'),400),'C06-09 photo request rejects query credentials');
  check(neutral(await raw(apiPort,'/api/v1/identity/security/events'+signed,'POST',credentials,'{}'),400),'C06-09 audit request rejects query credentials');
  for(const key of ['accountId','tenantId','resourceId','format','export','download','cursor'])
    check((await api('security/events',{[key]:canary},token)).status===400,'C06-09 audit rejects caller-controlled scope or export');
  // Root inserts a synthetic historical label to test output minimization;
  // immutable evidence is not modified or deleted by the runtime.
  const id=hex();
  sql(`INSERT INTO c05_identity_audit(tenant_id,audit_id,actor_account_id,action_code,outcome_code,context_hmac,created_at_utc)
    VALUES(UNHEX('${A}'),UNHEX('${id}'),UNHEX('${actor}'),'${canary}',1,UNHEX('${randomBytes(32).toString('hex')}'),UTC_TIMESTAMP(6));`);
  const events=await api('security/events',{},token);
  check(events.status===200&&events.data.events.some(e=>e.action==='security/event')&&!JSON.stringify(events.data).includes(canary),'C06-09 historical free-text audit label becomes neutral');
  check(sql(`SELECT COUNT(*) FROM c05_identity_audit WHERE tenant_id=UNHEX('${A}') AND audit_id=UNHEX('${id}') AND action_code='${canary}'`)==='1','C06-09 neutral projection preserves original audit evidence');
  await reserveTestWindow(30);
}
export async function verifyPrivateReference({api,check,sql,A,branch,resource,token,confidential,reserveTestWindow}) {
  await reserveTestWindow(10);
  const canary='C06_09_REF_'+randomBytes(12).toString('hex');confidential.push(canary);
  const where=`tenant_id=UNHEX('${A}') AND branch_id=UUID_TO_BIN('${branch}',0) AND resource_id=UUID_TO_BIN('${resource}',0)`;
  const original=sql('SELECT display_reference FROM c05_identity_private_resource WHERE '+where);
  check(original==='LOCAL-PRIVATE-REFERENCE','C06-09 original authorized reference fixture');
  try {
    for(const bad of ['https://example.test/'+canary+'?sig='+canary,'../'+canary,'file:///tmp/'+canary]){
      sql(`UPDATE c05_identity_private_resource SET display_reference='${bad}' WHERE ${where}`);
      const r=await api('private/read',{branchId:branch,resourceId:resource},token);
      check(r.status===401&&!r.data,'C06-09 persisted URL or path cannot become an authorized case receipt');
    }
  } finally {sql(`UPDATE c05_identity_private_resource SET display_reference='${original}' WHERE ${where}`);}
  const r=await api('private/read',{branchId:branch,resourceId:resource},token);
  check(r.status===200&&r.data.reference===original,'C06-09 valid private receipt remains available');
  await reserveTestWindow(25);
}
