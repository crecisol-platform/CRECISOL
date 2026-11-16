export async function verifyInvitations({ api, check, register, mailFor, password, approver, terms, sql, A, B, accountId }) {
  const mailbox = 'invited@example.test';
  check((await api('invitations/issue', { mailbox, code: approver.backups.pop() }, approver.token)).status === 200,
    'MFA tenant approver issues an expiring invitation without browser-selected role');
  const token = await mailFor(mailbox);
  await register(mailbox);
  const fields = { mailbox, password, code: 'enroll', token, version: terms.version, hash: terms.hash, accepted: 'true' };
  check((await api('invitations/accept', fields, undefined, 'tenant-b.example.test')).status === 401,
    'invitation bound to A cannot activate a membership in B');
  check((await api('invitations/accept', { ...fields, mailbox: 'other@example.test' })).status === 401,
    'invitation cannot be transferred to another mailbox');
  check((await api('invitations/accept', { ...fields, accepted: 'false' })).status === 401,
    'invitation does not activate access without explicit terms acceptance');
  check((await api('invitations/accept', { ...fields, hash: '00'.repeat(32) })).status === 401,
    'terms acceptance is bound to the exact published content hash');
  const race = await Promise.all(Array.from({ length: 4 }, () => api('invitations/accept', fields)));
  check(race.filter(r => r.status === 200).length === 1, 'concurrent invitation acceptance has exactly one winner');
  check(sql(`SELECT CONCAT(account_state,':',membership_state,':',role_code) FROM c05_identity_account JOIN c05_identity_membership USING(account_id) WHERE account_id=UNHEX('${accountId(mailbox)}') AND tenant_id=UNHEX('${A}')`) === '1:1:MEMBER',
    'accepted invitation activates only its fixed basic role');
  check(sql(`SELECT COUNT(*) FROM c05_identity_membership WHERE account_id=UNHEX('${accountId(mailbox)}') AND tenant_id=UNHEX('${B}')`) === '0',
    'invitation acceptance creates no relationship with other tenants');
  check(sql(`SELECT COUNT(*) FROM c05_identity_terms_acceptance WHERE account_id=UNHEX('${accountId(mailbox)}') AND tenant_id=UNHEX('${A}')`) === '1',
    'invitation and append-only terms evidence commit together');
  const login = await api('login', { mailbox, password });
  check(login.status === 200 && login.data.enrollmentRequired === true && !login.data.sessionToken,
    'invitation never bypasses MFA enrollment or issues a password-only session');
}
