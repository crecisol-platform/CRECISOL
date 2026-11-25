// Synthetic only. Exercise real HTTP and MySQL; no fixture credential output.
export async function verifyExceptionalAccess({platform,api,sql,check,provisionPlatformOperator,reservePlatformWindow,A,B,branch,otherBranch,resource,tenantToken,confidential,randomUUID,nextCode}) {
  const requester=provisionPlatformOperator('exception.requester@example.test');
  const reviewer=provisionPlatformOperator('exception.reviewer@example.test');
  const issuer=provisionPlatformOperator('exception.authority@example.test');
  const reason='Synthetic incident approved for exact private reference';
  const reviewReason='Independent review of synthetic support incident';
  const grantR=randomUUID(), grantA=randomUUID();
  const tenantId=sql(`SELECT BIN_TO_UUID(UNHEX('${A}'),0)`);
  const otherTenant=sql(`SELECT BIN_TO_UUID(UNHEX('${B}'),0)`);
  const fields={tenantId,branchId:branch,resourceId:resource,reason};
  async function login(op) {
    const start=await platform('login',{mailbox:op.mailbox,password:op.password});
    check(start.status===200,'C06-06 isolated operator password verified');
    const proof=await platform('mfa',{challenge:start.data.challenge,code:op.backups.pop()});
    check(proof.status===200 && typeof proof.data?.sessionToken==='string','C06-06 independent real factor login');
    confidential.push(proof.data.sessionToken);
    return proof.data.sessionToken;
  }
  await reservePlatformWindow(35);
  const req=await login(requester), rev=await login(reviewer), outsider=await login(issuer);
  const fake=randomUUID();
  check((await platform('breakglass/open',{...fields,code:requester.backups.at(-1)})).status===401,'C06-06 anonymous cannot request');
  check((await platform('breakglass/open',{...fields,code:issuer.backups.at(-1)},outsider)).status===401,'C06-06 operator role alone is not an entitlement');
  check((await platform('breakglass/read',{exceptionId:fake,code:requester.backups.at(-1)},req)).status===401,'C06-06 random identifier is not authority');
  check((await api('platform/breakglass/open',{...fields,code:requester.backups.at(-1)},tenantToken,'tenant-a.example.test')).status!==200,'C06-06 tenant session and host cannot become platform');
  sql(`INSERT INTO c06_exception_entitlement
    (entitlement_id,operator_id,issued_by,security_revision,tenant_id,branch_id,resource_id,capability,issued_at_utc,expires_at_utc)
    VALUES(UUID_TO_BIN('${grantR}',0),UNHEX('${requester.id}'),UNHEX('${issuer.id}'),1,UNHEX('${A}'),UUID_TO_BIN('${branch}',0),UUID_TO_BIN('${resource}',0),1,UTC_TIMESTAMP(6)-INTERVAL 1 MINUTE,UTC_TIMESTAMP(6)+INTERVAL 1 HOUR),
    (UUID_TO_BIN('${grantA}',0),UNHEX('${reviewer.id}'),UNHEX('${issuer.id}'),1,UNHEX('${A}'),UUID_TO_BIN('${branch}',0),UUID_TO_BIN('${resource}',0),2,UTC_TIMESTAMP(6)-INTERVAL 1 MINUTE,UTC_TIMESTAMP(6)+INTERVAL 1 HOUR);`);
  check((await platform('breakglass/open',{...fields,tenantId:otherTenant,code:requester.backups.at(-1)},req)).status===401,'C06-06 wrong tenant denied');
  check((await platform('breakglass/open',{...fields,branchId:otherBranch,code:requester.backups.at(-1)},req)).status===401,'C06-06 wrong branch denied');
  check((await platform('breakglass/open',{...fields,resourceId:fake,code:requester.backups.at(-1)},req)).status===401,'C06-06 wrong resource denied');
  check((await platform('breakglass/open',{...fields,reason:'------------',code:requester.backups.at(-1)},req)).status===401,'C06-06 unjustified purpose denied');
  check((await platform('breakglass/open',{...fields},req)).status===400,'C06-06 session alone cannot request');
  check((await platform('breakglass/open',{...fields,code:'0'.repeat(32)},req)).status===401,'C06-06 shaped invalid MFA denied');
  // Requester quota is independently committed, so allow a fresh real window
  // before exercising all transitions; no clock or quota injection.
  await reservePlatformWindow(40);
  const freshOpen=requester.backups.pop();
  const opened=await platform('breakglass/open',{...fields,code:freshOpen},req);
  check(opened.status===200 && opened.data?.state==='pending' && !('reference' in opened.data),'C06-06 valid request returns no private data');
  const id=opened.data.exceptionId;
  check((await platform('breakglass/read',{exceptionId:id,code:requester.backups.at(-1)},req)).status===401,'C06-06 pending request cannot read');
  check((await platform('breakglass/approve',{exceptionId:id,tenantId,branchId:branch,resourceId:resource,reason,reviewReason,code:requester.backups.at(-1)},req)).status===401,'C06-06 self review denied');
  check((await platform('breakglass/approve',{exceptionId:id,tenantId,branchId:branch,resourceId:resource,reason,reviewReason,code:issuer.backups.at(-1)},outsider)).status===401,'C06-06 unentitled reviewer denied');
  check((await platform('breakglass/approve',{exceptionId:id,tenantId,branchId:branch,resourceId:resource,reason:reason+' altered',reviewReason,code:reviewer.backups.at(-1)},rev)).status===401,'C06-06 independent reviewer must match original purpose');
  check((await platform('breakglass/approve',{exceptionId:id,tenantId,branchId:branch,resourceId:resource,reason,reviewReason},rev)).status===400,'C06-06 reviewer fresh MFA required');
  check((await platform('breakglass/approve',{exceptionId:id,tenantId,branchId:otherBranch,resourceId:resource,reason,reviewReason,code:reviewer.backups.at(-1)},rev)).status===401,'C06-06 reviewer confirms exact target independently');
  const approved=await platform('breakglass/approve',{exceptionId:id,tenantId,branchId:branch,resourceId:resource,reason,reviewReason,code:reviewer.backups.pop()},rev);
  check(approved.status===200&&approved.data?.state==='approved'&&!('reference' in approved.data),'C06-06 independent approval is scoped not disclosure');
  check((await platform('breakglass/approve',{exceptionId:id,tenantId,branchId:branch,resourceId:resource,reason,reviewReason,code:reviewer.backups.at(-1)},rev)).status===401,'C06-06 immutable approval cannot be duplicated or extended');
  check((await platform('breakglass/read',{exceptionId:id,code:reviewer.backups.at(-1)},rev)).status===401,'C06-06 reviewer cannot consume requester authority');
  const differentSession=await login(requester);
  check((await platform('breakglass/read',{exceptionId:id,code:requester.backups.at(-1)},differentSession)).status===401,'C06-06 other session of same operator denied');
  check((await platform('breakglass/read',{exceptionId:id,resourceId:fake,code:requester.backups.at(-1)},req)).status===400,'C06-06 read cannot replace resource selector');
  await reservePlatformWindow(40);
  check((await platform('breakglass/read',{exceptionId:id,code:freshOpen},req)).status===401,'C06-06 consumed opening proof cannot read');
  const freshRead=await nextCode(requester.seed); // Genuine RFC6238 SHA1, not only one-use backup.
  // Audit insert failure must roll back factor consumption and prohibit disclosure.
  sql("REVOKE INSERT ON crecisol_c05.c06_exception_audit FROM 'c05_identity_local_runtime';");
  try {
    check((await platform('breakglass/read',{exceptionId:id,code:freshRead},req)).status===401,'C06-06 audit failure denies private response');
  } finally {
    sql("GRANT INSERT ON crecisol_c05.c06_exception_audit TO 'c05_identity_local_runtime';");
  }
  const result=await platform('breakglass/read',{exceptionId:id,code:freshRead},req);
  check(result.status===200 && result.data?.reference==='LOCAL-PRIVATE-REFERENCE' && Object.keys(result.data).length===1,'C06-06 real approved read returns only exact reference');
  check(result.cacheControl?.includes('no-store') && result.cacheControl?.includes('private'),'C06-06 private projection is never cacheable');
  check(sql(`SELECT COUNT(*) FROM c05_platform_operator WHERE operator_id=UNHEX('${requester.id}') AND last_totp_step IS NOT NULL`)==='1','C06-06 real TOTP step consumed');
  check((await platform('breakglass/read',{exceptionId:id,code:freshRead},req)).status===401,'C06-06 consumed read proof cannot replay');
  check((await platform('breakglass/close',{exceptionId:id},differentSession)).status===401,'C06-06 unrelated session cannot close');
  check((await platform('breakglass/close',{exceptionId:id},req)).status===200,'C06-06 requester closes immediately');
  check((await platform('breakglass/read',{exceptionId:id,code:requester.backups.at(-1)},req)).status===401,'C06-06 closed exception denies read');
  check(sql(`SELECT COUNT(*) FROM c06_exception_audit WHERE exception_id=UUID_TO_BIN('${id}',0) AND allowed=1`)==='4','C06-06 open approve read close each audited');
  check(Number(sql(`SELECT COUNT(*) FROM c06_exception_audit WHERE allowed=0`))>=8,'C06-06 denied authenticated attempts audited');
  check(sql(`SELECT COUNT(*) FROM c06_exception_request q JOIN c06_exception_approval a USING(exception_id)
    WHERE q.exception_id=UUID_TO_BIN('${id}',0) AND q.expires_at_utc<=q.requested_at_utc+INTERVAL 10 MINUTE
      AND a.expires_at_utc<=q.expires_at_utc AND a.reviewer_id<>q.requester_id AND LENGTH(q.purpose_hmac)=32 AND LENGTH(a.review_hmac)=32`)==='1','C06-06 exact bounded deadlines and separate purpose digests');
  const reqId=sql(`SELECT BIN_TO_UUID(session_id,0) FROM c06_exception_request WHERE exception_id=UUID_TO_BIN('${id}',0)`);
  const revId=sql(`SELECT BIN_TO_UUID(review_session_id,0) FROM c06_exception_approval WHERE exception_id=UUID_TO_BIN('${id}',0)`);
  // One DB UTC instant for both inserts. Independent UTC_TIMESTAMP(6)
  // statements otherwise differ by microseconds and legitimately trigger the
  // non-extension guard (run 45551835457). Never weaken the trigger.
  const expired=randomUUID();
  sql(`SET @historical_clock=UTC_TIMESTAMP(6);
  SET @historical_expiry=@historical_clock-INTERVAL 10 SECOND;
  INSERT INTO c06_exception_request VALUES(UUID_TO_BIN('${expired}',0),UNHEX('${requester.id}'),UUID_TO_BIN('${reqId}',0),1,UUID_TO_BIN('${grantR}',0),UNHEX('${A}'),UUID_TO_BIN('${branch}',0),UUID_TO_BIN('${resource}',0),UNHEX(REPEAT('ab',32)),@historical_clock-INTERVAL 50 SECOND,@historical_expiry);
  INSERT INTO c06_exception_approval VALUES(UUID_TO_BIN('${expired}',0),UNHEX('${reviewer.id}'),UUID_TO_BIN('${revId}',0),1,UUID_TO_BIN('${grantA}',0),UNHEX(REPEAT('cd',32)),@historical_clock-INTERVAL 40 SECOND,@historical_expiry);`);
  check(sql(`SELECT COUNT(*) FROM c06_exception_request q JOIN c06_exception_approval a USING(exception_id)
    WHERE q.exception_id=UUID_TO_BIN('${expired}',0) AND q.expires_at_utc=a.expires_at_utc AND a.expires_at_utc<UTC_TIMESTAMP(6)
    AND NOT EXISTS(SELECT 1 FROM c06_exception_close c WHERE c.exception_id=q.exception_id)`)==='1','C06-06 expired approval fixture is real and not closed');
  check((await platform('breakglass/read',{exceptionId:expired,code:requester.backups.at(-1)},req)).status===401,'C06-06 expired historical exception denied');
  await reservePlatformWindow(40);
  const second=await platform('breakglass/open',{...fields,code:requester.backups.pop()},req);
  check(second.status===200,'C06-06 second separately authorized request');
  const secondId=second.data.exceptionId;
  check((await platform('breakglass/approve',{exceptionId:secondId,tenantId,branchId:branch,resourceId:resource,reason,reviewReason,code:reviewer.backups.pop()},rev)).status===200,'C06-06 second independent approval');
  // Revoke the reviewer's session, then prove an otherwise valid approval cannot be read.
  const third=await platform('breakglass/open',{...fields,code:requester.backups.pop()},req);
  check(third.status===200,'C06-06 distinct exception before session revocation');
  const thirdId=third.data.exceptionId;
  const temporaryReviewerSession=await login(reviewer);
  check((await platform('breakglass/approve',{exceptionId:thirdId,tenantId,branchId:branch,resourceId:resource,reason,reviewReason,code:reviewer.backups.pop()},temporaryReviewerSession)).status===200,'C06-06 approval bound to separate reviewer session');
  check((await platform('logout',{},temporaryReviewerSession)).status===200,'C06-06 reviewer session explicitly revoked');
  check((await platform('breakglass/read',{exceptionId:thirdId,code:requester.backups.at(-1)},req)).status===401,'C06-06 reviewer session revocation cancels approval');
  sql(`UPDATE c06_exception_entitlement SET revoked_at_utc=UTC_TIMESTAMP(6) WHERE entitlement_id=UUID_TO_BIN('${grantA}',0)`);
  check((await platform('breakglass/read',{exceptionId:secondId,code:requester.backups.at(-1)},req)).status===401,'C06-06 reviewer entitlement revocation immediately denies');
  // An operator cannot open a new exception after explicit requester grant revocation.
  sql(`UPDATE c06_exception_entitlement SET revoked_at_utc=UTC_TIMESTAMP(6) WHERE entitlement_id=UUID_TO_BIN('${grantR}',0)`);
  check((await platform('breakglass/open',{...fields,code:requester.backups.at(-1)},req)).status===401,'C06-06 requester eligibility revocation denies opening');
  for (const [statement,label] of [
    [`UPDATE c06_exception_request SET expires_at_utc=expires_at_utc+INTERVAL 1 MINUTE WHERE exception_id=UUID_TO_BIN('${id}',0)`,'deadline cannot extend'],
    [`DELETE FROM c06_exception_approval WHERE exception_id=UUID_TO_BIN('${id}',0)`,'approval immutable'],
    [`DELETE FROM c06_exception_close WHERE exception_id=UUID_TO_BIN('${id}',0)`,'close cannot reopen'],
    [`UPDATE c06_exception_audit SET allowed=1 WHERE allowed=0`,'audit immutable'],
    [`UPDATE c06_exception_entitlement SET revoked_at_utc=NULL WHERE entitlement_id=UUID_TO_BIN('${grantA}',0)`,'grant revocation irreversible']]) {
    let denied=false; try { sql(statement); } catch { denied=true; }
    check(denied,'C06-06 SQL '+label);
  }
  for (const [statement,label] of [
    [`UPDATE c06_exception_entitlement SET revoked_at_utc=UTC_TIMESTAMP(6) WHERE entitlement_id=UUID_TO_BIN('${grantR}',0)`,'runtime cannot revoke or alter eligibility'],
    [`DELETE FROM c06_exception_audit WHERE allowed=0`,'runtime cannot delete ledger'],
    [`SELECT * FROM c06_exception_audit`,'runtime cannot enumerate ledger']]) {
    let denied=false; try { sql(statement,true); } catch { denied=true; }
    check(denied,'C06-06 SQL '+label);
  }
}
