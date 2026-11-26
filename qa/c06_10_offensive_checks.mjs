// C06-10: authorized regression exclusively through the isolated C05 runner.
// No external target, no credentials or raw private response values in output.
import { readFileSync } from 'node:fs';
import { createHash, randomBytes, randomUUID } from 'node:crypto';
const matrix=JSON.parse(readFileSync(new URL('../docs/c06/C06_10_MATRIZ_REGRESION.json',import.meta.url),'utf8'));
const random=()=>randomBytes(32).toString('hex');
const uuid=hex=>hex.replace(/^(........)(....)(....)(....)(............)$/,'$1-$2-$3-$4-$5');
const hostA='tenant-a.example.test',hostB='tenant-b.example.test',hostP='platform-admin.example.test';
const neutral=r=>r.status===401&&(r.data===null||(r.data&&typeof r.data==='object'&&!Array.isArray(r.data)&&Object.keys(r.data).length===0));
function bodyFor(row) {
  const id=randomUUID(), h=random();
  const fields={accepted:'true',confirmed:'true',identityVerified:'true',branchId:id,resourceId:id,caseId:id,exceptionId:id,tenantId:id,sessionId:id,membershipId:id,factorId:id,
    challenge:h,token:h,code:h.slice(0,32),currentPassword:'Synthetic-invalid-password-01!',newPassword:'Synthetic-invalid-password-02!',
    cursor:'start',decision:'approve',displayName:'Synthetic boundary probe',evidenceHash:h,expectedRevision:'3',fingerprint:h,hash:h,
    language:'es',mailbox:'c06-10-probe@example.test',minutes:'30',pixels:Buffer.alloc(64*64*4).toString('base64'),realm:'TENANT',
    reason:'Synthetic authorization boundary regression',reviewReason:'Synthetic independent authorization regression',response:'{}',timeZone:'America/Tegucigalpa',version:'1'};
  return Object.fromEntries(row.fields.map(k=>{if(!Object.hasOwn(fields,k))throw Error('C06-10 missing fixture field '+k);return [k,fields[k]];}));
}
function auditCounts(sql) {
  const t=sql("SELECT CONCAT(HEX(tenant_id),':',action_code),COUNT(*) FROM c05_identity_audit WHERE outcome_code=0 AND actor_account_id IS NULL GROUP BY tenant_id,action_code");
  const p=sql("SELECT CONCAT('P:',action_code),COUNT(*) FROM c05_platform_identity_audit WHERE allowed=0 AND operator_id IS NULL GROUP BY action_code");
  return new Map([t,p].filter(Boolean).join('\n').split('\n').filter(Boolean).map(line=>{const [k,v]=line.split('\t');return [k,Number(v)];}));
}
function stableSnapshot(sql) {
  // Deliberately exclude legitimate changes to audit, quota and session
  // sliding timestamps. No credential hashes or protected contents are logged.
  const queries=[
    "SELECT HEX(tenant_id),HEX(membership_id),membership_state,role_code,approval_revision FROM c05_identity_membership ORDER BY tenant_id,membership_id",
    "SELECT HEX(account_id),account_state,security_revision FROM c05_identity_account ORDER BY account_id",
    "SELECT HEX(tenant_id),HEX(session_id),revoked_at_utc IS NOT NULL FROM c05_identity_session ORDER BY tenant_id,session_id",
    "SELECT HEX(operator_id),security_revision,active,recovery_pending,can_manage_session_policy FROM c05_platform_operator ORDER BY operator_id",
    "SELECT HEX(operator_id),HEX(session_id),revoked_at_utc IS NOT NULL FROM c05_platform_session ORDER BY operator_id,session_id",
    "SELECT HEX(tenant_id),HEX(grant_id),grant_revision,revoked_at_utc IS NOT NULL FROM c05_identity_resource_grant ORDER BY tenant_id,grant_id",
    "SELECT HEX(tenant_id),HEX(branch_id),HEX(resource_id),HEX(owner_account_id),resource_state,SHA2(display_reference,256) FROM c05_identity_private_resource ORDER BY tenant_id,branch_id,resource_id",
    "SELECT HEX(tenant_id),HEX(account_id),photo_state,SHA2(protected_profile,256),COALESCE(SHA2(photo_png,256),'NULL') FROM c05_identity_scoped_profile ORDER BY tenant_id,account_id",
    "SELECT HEX(operator_id),photo_state,SHA2(protected_profile,256),COALESCE(SHA2(photo_png,256),'NULL') FROM c05_platform_scoped_profile ORDER BY operator_id",
    "SELECT COUNT(*) FROM c06_exception_request",
    "SELECT COUNT(*) FROM c06_case_resource_assignment"
  ];
  const hash=createHash('sha256');for(const q of queries)hash.update(JSON.stringify(sql(q)));return hash.digest('hex');
}
export async function verifyAuthorizationMatrix({api,platform,sql,check,A,B,tenantToken,operatorToken,uiPort,reserveTestWindow,reservePlatformWindow,confidential}) {
  const rows=matrix.operations.filter(r=>r.mode!=='proof_regression');
  const done=new Set();
  check(rows.length===57&&new Set(rows.map(r=>r.operation)).size===57,'C06-10 exact 57 authenticated operation inventory');
  await reserveTestWindow(5);await reservePlatformWindow(5);
  const sentinel=async realm=>realm==='tenant'
    ? (await api('sessions',{},tenantToken)).status===200
    : (await platform('sessions',{},operatorToken)).status===200;
  check(await sentinel('tenant')&&await sentinel('platform'),'C06-10 genuine independent authenticated tenant and operator positive controls');
  const beforeState=stableSnapshot(sql);
  for(const realm of ['tenant','platform']) {
    const group=rows.filter(r=>r.realm===realm);
    // At most eight negative requests plus positive sentinel per batch;
    // verify the durable denied-audit delta so quotas/transport errors cannot
    // masquerade as successful authorization coverage.
    for(let start=0;start<group.length;start+=2) {
      const block=group.slice(start,start+2),reserve=realm==='tenant'?reserveTestWindow:reservePlatformWindow;
      await reserve(12);
      const before=auditCounts(sql),expected=new Map();
      for(const row of block) {
        const home=realm==='tenant'?hostA:hostP,wrongHost=realm==='tenant'?hostB:hostA;
        const own=realm==='tenant'?tenantToken:operatorToken,foreign=realm==='tenant'?operatorToken:tenantToken;
        const forged=random();confidential.push(forged);
        for(const [kind,token,host] of [['anonymous',undefined,home],['forged',forged,home],['wrong_realm',foreign,home],['wrong_host',own,wrongHost]]) {
          const key=row.operation+':'+kind;
          if(done.has(key))throw Error('C06-10 duplicate matrix cell');
          let r;
          if(row.mode==='browser_session') {
            const cookie=realm==='tenant'?'__Host-crecisol-c05':'__Host-crecisol-platform-c05';
            r=await api(row.operation,bodyFor(row),undefined,host,{'X-Crecisol-Browser':'1',Origin:'http://127.0.0.1:'+uiPort,...(token?{Cookie:cookie+'='+token}:{})});
          } else r=await api(row.operation,bodyFor(row),token,host);
          check(neutral(r),'C06-10 matrix '+row.operation+' '+kind+' denied with neutral receipt');
          done.add(key);
          // Platform operations on a tenant host are rejected before dispatch,
          // intentionally not entered into the platform audit table.
          if(!(realm==='platform'&&kind==='wrong_host')){
            const bucket=(realm==='platform'?'P':host===hostB?B.toUpperCase():A.toUpperCase())+':'+row.operation;
            expected.set(bucket,(expected.get(bucket)||0)+1);
          }
        }
      }
      const after=auditCounts(sql);
      for(const [key,n] of expected)
        check((after.get(key)||0)-(before.get(key)||0)===n,'C06-10 durable unverified-actor denial evidence');
      check(await sentinel(realm),'C06-10 positive same-realm session after negative batch');
    }
  }
  check(done.size===228,'C06-10 exact 228 authenticated boundary cells executed');
  check(stableSnapshot(sql)===beforeState,'C06-10 denied matrix preserves security and private resource state');
  console.log('PASS C06-10 MATRIX authenticated_operations=57 denied_cells=228 proof_operations=22 (covered by separate proof flows)');
  await reserveTestWindow(35);await reservePlatformWindow(20);
}
export async function verifyCrossTenantObjects({api,sql,check,A,B,token,foreignToken,foreignSession,sameTenantSession,foreignMember,nextProof,reserveTestWindow}) {
  await reserveTestWindow(15);
  const aSession=uuid(sameTenantSession),bSession=uuid(foreignSession),target=uuid(foreignMember);
  const readState=()=>sql("SELECT CONCAT(membership_state,':',approval_revision) FROM c05_identity_membership WHERE tenant_id=UNHEX('"+B+"') AND membership_id=UNHEX('"+foreignMember+"')");
  const before=readState();check(before.startsWith('0:'),'C06-10 actual foreign pending applicant fixture exists');
  const code=await nextProof();
  check(neutral(await api('memberships/decide',{membershipId:target,decision:'approve',reason:'Synthetic cross company denial',code},token,hostA)),'C06-10 approver with fresh factor cannot approve existing B applicant');
  check(readState()===before,'C06-10 foreign pending applicant state and revision unchanged');
  check(neutral(await api('sessions/revoke',{sessionId:bSession},token,hostA)),'C06-10 valid A actor cannot revoke actual B session of same account');
  check(neutral(await api('sessions/revoke',{sessionId:aSession},foreignToken,hostB)),'C06-10 valid B actor cannot revoke actual A session of same account');
  check(sql("SELECT COUNT(*) FROM c05_identity_session WHERE session_id IN(UNHEX('"+sameTenantSession+"'),UNHEX('"+foreignSession+"')) AND revoked_at_utc IS NULL")==='2','C06-10 both targeted real fixture rows remain active');
  check((await api('sessions',{},token,hostA)).data?.sessions.some(s=>s.id===aSession),'C06-10 A authorized inventory retains A session');
  check((await api('sessions',{},foreignToken,hostB)).data?.sessions.some(s=>s.id===bSession),'C06-10 B authorized inventory retains B session');
  check((await api('memberships/pending',{cursor:'start'},foreignToken,hostB)).status===200,'C06-10 independent B authorization still functional');
  await reserveTestWindow(30);
}
export async function verifySameTenantObjects({api,sql,check,A,memberToken,approverToken,reviewerToken,memberId,memberProof,branch,resource,reserveTestWindow,reservePlatformWindow}) {
  await reserveTestWindow(25);
  const member=await api('sessions',{},memberToken),approver=await api('sessions',{},approverToken);
  check(member.status===200&&approver.status===200&&member.data.sessions.length>0&&approver.data.sessions.length>0,'C06-10 two independently authenticated A actors');
  const x=member.data.sessions[0].id,y=approver.data.sessions[0].id;
  check(x!==y,'C06-10 distinct existing coworker session identifiers');
  check(neutral(await api('sessions/revoke',{sessionId:y},memberToken)),'C06-10 member cannot revoke actual coworker session');
  check(neutral(await api('sessions/revoke',{sessionId:x},approverToken)),'C06-10 approver capability is not global session authority');
  check(sql("SELECT COUNT(*) FROM c05_identity_session WHERE tenant_id=UNHEX('"+A+"') AND session_id IN(UUID_TO_BIN('"+x+"',0),UUID_TO_BIN('"+y+"',0)) AND revoked_at_utc IS NULL")==='2','C06-10 coworker sessions remain active');
  check((await api('memberships/pending',{cursor:'start'},approverToken)).data?.requests.some(r=>r.id===memberId),'C06-10 approver sees existing pending applicant');
  check(neutral(await api('memberships/pending',{cursor:'start'},memberToken)),'C06-10 ordinary member cannot list administrative applicants');
  const where="tenant_id=UNHEX('"+A+"') AND membership_id=UUID_TO_BIN('"+memberId+"',0)";
  const before=sql('SELECT CONCAT(membership_state,\':\',approval_revision) FROM c05_identity_membership WHERE '+where);
  check(before.startsWith('0:'),'C06-10 valid pending target before role test');
  check(neutral(await api('memberships/decide',{membershipId:memberId,decision:'approve',reason:'Synthetic member escalation denied',code:memberProof},memberToken)),'C06-10 ordinary member with fresh factor cannot approve existing applicant');
  check(sql('SELECT CONCAT(membership_state,\':\',approval_revision) FROM c05_identity_membership WHERE '+where)===before,'C06-10 forbidden approval leaves row unchanged');
  check(neutral(await api('private/read',{branchId:branch,resourceId:resource},approverToken)),'C06-10 tenant-wide approver cannot read member-owned private case');
  check((await api('private/read',{branchId:branch,resourceId:resource},memberToken)).data?.reference==='LOCAL-PRIVATE-REFERENCE','C06-10 actual owner retains correct private access');
  check(neutral(await api('private/read',{branchId:branch,resourceId:resource},reviewerToken)),'C06-10 actual platform operator cannot use tenant-private endpoint');
  for(const field of ['tenantId','accountId','role','permissions','ownerId','isAdmin'])
    check((await api('private/read',{branchId:branch,resourceId:resource,[field]:'forged'},memberToken)).status===400,'C06-10 forged authorization field '+field+' denied at transport');
  await reserveTestWindow(35);await reservePlatformWindow(20);
}
export async function verifyBrowserDenial({page,check,AxeBuilder,branch,otherBranch,resource,reserveTestWindow}) {
  await reserveTestWindow(8);
  const original=page.viewportSize();
  const modal=page.getByRole('dialog',{name:'Consulta segura de recurso'});
  const open=async()=>{
    const details=page.locator('details.identity-summary-extras');
    if(!(await details.evaluate(d=>d.open)))await details.locator('summary').click();
    await page.getByRole('button',{name:'Comprobar un acceso privado',exact:true}).click();
    await modal.waitFor({state:'visible'});
  };
  const submit=async b=>{
    await modal.getByLabel('Sucursal del recurso').fill(b);
    await modal.getByLabel('Identificador del recurso').fill(resource);
    const response=page.waitForResponse(r=>r.url().endsWith('/private/read')&&r.request().method()==='POST');
    await modal.getByRole('button',{name:'Consultar recurso autorizado'}).click();
    return (await response).status();
  };
  try {
    await open();check(await submit(otherBranch)===401,'C06-10 Angular actual wrong-branch denial');
    await page.getByLabel('Referencia privada').waitFor({state:'hidden'});
    await modal.getByRole('alert').waitFor({state:'visible'});
    check(!(await modal.innerText()).includes('LOCAL-PRIVATE-REFERENCE'),'C06-10 Angular clears stale private reference and renders neutral denial');
    for(const size of [{width:390,height:844},{width:1280,height:800}]){
      await page.setViewportSize(size);
      const b=await modal.boundingBox();
      check(Boolean(b)&&b.x>=-1&&b.y>=-1&&b.x+b.width<=size.width+1&&b.y+b.height<=size.height+1,'C06-10 denial modal fits viewport '+size.width);
      check(await modal.evaluate(d=>d.scrollWidth<=d.clientWidth+1),'C06-10 denial modal no horizontal overflow '+size.width);
      const axe=await new AxeBuilder({page}).include('dialog[open]').analyze();
      check(!axe.violations.some(v=>['serious','critical'].includes(v.impact)),'C06-10 denied modal no serious or critical accessibility findings '+size.width);
    }
    await page.keyboard.press('Escape');await modal.waitFor({state:'hidden'});
    await open();check(await submit(branch)===200,'C06-10 Angular authorized retry succeeds after denial');
    await page.getByLabel('Referencia privada').waitFor({state:'visible'});
    check(await page.getByLabel('Referencia privada').innerText()==='LOCAL-PRIVATE-REFERENCE','C06-10 authorized retry restores exact reference');
    if(await modal.isVisible())await page.keyboard.press('Escape');
  } finally {if(original)await page.setViewportSize(original);}
  await reserveTestWindow(20);
}
