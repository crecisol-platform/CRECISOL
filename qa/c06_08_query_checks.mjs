// Adversarial real HTTP over disposable MySQL fixtures. No new product routes.
import { randomBytes, randomUUID, createHmac } from 'node:crypto';
export async function verifyScopedQueries({api,check,sql,A,B,actor,token,key,memberId,reserveTestWindow,confidential}) {
  const hex = () => randomUUID().replaceAll('-','');
  const digest = value => createHmac('sha256',Buffer.from(key,'hex')).update('C05/session:'+value).digest('hex');
  const hash = digest(token);
  const synthetic = Array.from({length:27},(_,n)=>({account:hex(),member:hex(),tenant:n<13?A:B,login:randomBytes(32).toString('hex')}));
  const idList = synthetic.map(x=>"UNHEX('"+x.account+"')").join(',');
  const ownMembers = synthetic.filter(x=>x.tenant===A).map(x=>x.member.toLowerCase());
  const foreignMembers = new Set(synthetic.filter(x=>x.tenant===B).map(x=>x.member.toLowerCase()));
  const sameTenantSession=hex(), foreignSession=hex(), foreignMembership=hex(), foreignGrant=hex();
  const secondToken=randomBytes(32).toString('hex'), foreignToken=randomBytes(32).toString('hex');
  confidential.push(secondToken,foreignToken);
  const cursorOf = row => row.data?.nextCursor;
  const norm = id => id.replaceAll('-','').toLowerCase();
  const inbox = (cursor,t=token,h='tenant-a.example.test') => api('memberships/pending',{cursor},t,h);
  const sessions = (t,h='tenant-a.example.test') => api('sessions',{},t,h);
  await reserveTestWindow(45);
  try {
    // Equal creation timestamps force the binary-id tie breaker. No contacts,
    // credentials or real users; cleanup before the pre-existing UI regression.
    sql(synthetic.map(x=>`INSERT INTO c05_identity_account(account_id,login_lookup_hmac,mailbox_ciphertext,mailbox_nonce,mailbox_tag,mailbox_key_version,password_phc,mailbox_verified_at_utc,account_state,created_at_utc,updated_at_utc)
      VALUES(UNHEX('${x.account}'),UNHEX('${x.login}'),X'00',REPEAT(X'00',12),REPEAT(X'00',16),1,'synthetic-not-a-login',UTC_TIMESTAMP(6),0,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6));
      INSERT INTO c05_identity_membership(tenant_id,membership_id,account_id,membership_state,role_code,created_at_utc)
      VALUES(UNHEX('${x.tenant}'),UNHEX('${x.member}'),UNHEX('${x.account}'),0,'MEMBER','2020-01-01 00:00:00.123456');`).join('\n'));
    // One real verified actor with independent valid memberships in A/B.
    // The extra sessions are explicitly synthetic clones of the real MFA-bound
    // session in the disposable test DB, not an authentication bypass in product.
    sql(`INSERT INTO c05_identity_membership(tenant_id,membership_id,account_id,profile_kind,membership_state,role_code,approval_revision,created_at_utc,granted_at_utc,expires_at_utc)
      SELECT UNHEX('${B}'),UNHEX('${foreignMembership}'),account_id,profile_kind,1,role_code,approval_revision,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)-INTERVAL 1 MINUTE,UTC_TIMESTAMP(6)+INTERVAL 1 DAY
      FROM c05_identity_membership WHERE tenant_id=UNHEX('${A}') AND account_id=UNHEX('${actor}');
      INSERT INTO c05_identity_resource_grant(tenant_id,grant_id,account_id,branch_id,resource_id,scope_kind,permission_code,granted_by,granted_at_utc,expires_at_utc,grant_revision)
      SELECT UNHEX('${B}'),UNHEX('${foreignGrant}'),account_id,UNHEX('${B}'),NULL,2,'MEMBERSHIP_APPROVE',granted_by,UTC_TIMESTAMP(6)-INTERVAL 1 MINUTE,UTC_TIMESTAMP(6)+INTERVAL 1 DAY,1
      FROM c05_identity_resource_grant WHERE tenant_id=UNHEX('${A}') AND account_id=UNHEX('${actor}') AND permission_code='MEMBERSHIP_APPROVE' AND revoked_at_utc IS NULL;
      INSERT INTO c05_identity_terms_acceptance(tenant_id,account_id,terms_version,content_hash,accepted_at_utc)
      SELECT UNHEX('${B}'),UNHEX('${actor}'),terms_version,content_hash,UTC_TIMESTAMP(6) FROM c05_identity_terms WHERE tenant_id=UNHEX('${B}') AND active=1;`);
    const clone=(tenant,session,member,tokenHash)=>`INSERT INTO c05_identity_session(tenant_id,session_id,account_id,membership_id,token_hmac,session_family,mfa_factor_id,mfa_verified_at_utc,session_revision,issued_at_utc,expires_at_utc,account_revision,membership_revision,last_seen_at_utc,device_label,auth_method,peer_hmac)
      SELECT UNHEX('${tenant}'),UNHEX('${session}'),account_id,${member},UNHEX('${tokenHash}'),UNHEX('${session}'),mfa_factor_id,mfa_verified_at_utc,session_revision,issued_at_utc,expires_at_utc,account_revision,membership_revision,last_seen_at_utc,device_label,auth_method,peer_hmac
      FROM c05_identity_session WHERE tenant_id=UNHEX('${A}') AND token_hmac=UNHEX('${hash}');`;
    sql(clone(A,sameTenantSession,'membership_id',digest(secondToken))+clone(B,foreignSession,`UNHEX('${foreignMembership}')`,digest(foreignToken)));
    const first=await inbox('start');
    check(first.status===200 && first.data.requests.length===12 && typeof cursorOf(first)==='string','C06-08 page A has twelve authorized rows and opaque continuation');
    const cursor=cursorOf(first);
    confidential.push(cursor);
    const ids1=first.data.requests.map(x=>norm(x.id));
    check(ids1.join(',')===ownMembers.slice().sort().slice(0,12).join(','),'C06-08 exact stable ordering for equal timestamps');
    const second=await inbox(cursor);
    check(second.status===200 && second.data.requests.length>=1,'C06-08 valid same-scope continuation');
    let all=[...ids1,...second.data.requests.map(x=>norm(x.id))], next=cursorOf(second), pages=2;
    while(next && pages<5){confidential.push(next);const r=await inbox(next);check(r.status===200,'C06-08 continuation remains valid');all.push(...r.data.requests.map(x=>norm(x.id)));next=cursorOf(r);pages++;}
    check(!next && new Set(all).size===all.length && ownMembers.every(x=>all.includes(x)) && all.includes(norm(memberId)),'C06-08 exhaustive A pagination without duplicates or omitted synthetic applicants');
    check(all.every(id=>!foreignMembers.has(id)),'C06-08 no B rows in A pages or has-more decision');
    const b=await inbox('start',foreignToken,'tenant-b.example.test');
    check(b.status===200 && b.data.requests.length===12 && b.data.requests.every(x=>foreignMembers.has(norm(x.id))),'C06-08 independent authorized tenant B sees only its own pending applicants');
    const bCursor=cursorOf(b);check(typeof bCursor==='string','C06-08 B continuation exists');confidential.push(bCursor);
    const b2=await inbox(bCursor,foreignToken,'tenant-b.example.test');
    check(b2.status===200 && b2.data.requests.length===2 && b2.data.nextCursor===null && new Set([...b.data.requests,...b2.data.requests].map(x=>norm(x.id))).size===14,'C06-08 B total inferred only from its authorized pages');
    check((await inbox(cursor,foreignToken,'tenant-b.example.test')).status===401,'C06-08 signed cursor A rejected in authenticated B');
    check((await inbox(bCursor)).status===401,'C06-08 signed cursor B rejected in authenticated A');
    check((await inbox(cursor,secondToken)).status===401,'C06-08 cursor cannot transfer between two valid sessions of same actor');
    for(const modified of [cursor.slice(0,-1)+(cursor.at(-1)==='0'?'1':'0'),cursor.replace('v1/','v2/'),'20200101000000123456/'+memberId,cursor+'/extra']) {
      check((await inbox(modified)).status===401,'C06-08 tampered or legacy cursor denied');
    }
    for(const field of ['tenantId','branchId','accountId','offset','pageSize','search','total']) {
      check((await api('memberships/pending',{cursor:'start',[field]:'1'},token)).status===400,'C06-08 caller-selected '+field+' denied by closed request');
    }
    sql(`UPDATE c05_identity_resource_grant SET grant_revision=grant_revision+1 WHERE tenant_id=UNHEX('${A}') AND account_id=UNHEX('${actor}') AND permission_code='MEMBERSHIP_APPROVE';`);
    check((await inbox(cursor)).status===401,'C06-08 grant revision invalidates old continuation');
    const fresh=await inbox('start');
    check(fresh.status===200,'C06-08 new authorized page after grant revision');
    const sa=await sessions(token),sb=await sessions(foreignToken,'tenant-b.example.test');
    check(sa.status===200 && sa.data.sessions.some(s=>norm(s.id)===sameTenantSession) && sa.data.sessions.every(s=>norm(s.id)!==foreignSession),'C06-08 A inventory isolates same actor across memberships');
    check(sb.status===200 && sb.data.sessions.length===1 && norm(sb.data.sessions[0].id)===foreignSession,'C06-08 B inventory isolates exact membership');
    check((await sessions(token,'tenant-b.example.test')).status===401,'C06-08 A session cannot enumerate B');
  } finally {
    // Remove only exact synthetic rows; immutable audit events remain for the
    // normal full rollback. Never disable foreign keys or clear real quotas.
    sql(`DELETE FROM c05_identity_session WHERE session_id IN(UNHEX('${sameTenantSession}'),UNHEX('${foreignSession}'));
      DELETE FROM c05_identity_resource_grant WHERE tenant_id=UNHEX('${B}') AND grant_id=UNHEX('${foreignGrant}');
      DELETE FROM c05_identity_terms_acceptance WHERE tenant_id=UNHEX('${B}') AND account_id=UNHEX('${actor}');
      DELETE FROM c05_identity_membership WHERE tenant_id=UNHEX('${B}') AND membership_id=UNHEX('${foreignMembership}');
      DELETE FROM c05_identity_membership WHERE account_id IN(${idList});
      DELETE FROM c05_identity_account WHERE account_id IN(${idList});`);
  }
  await reserveTestWindow(45);
  console.log('PASS C06-08 HTTP scoped keyset, isolated totals and independent sessions');
}
