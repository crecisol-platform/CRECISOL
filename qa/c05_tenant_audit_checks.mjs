// Explicit TEN-008/009/010 negatives against the real operational API.
export async function verifyTenantAuditBoundary({ api, platform, check, token, sql, B }) {
  const own = await api('security/events', {}, token);
  check(own.status === 200 && Array.isArray(own.data.events), 'tenant identity can read only its own contextual security history');
  check((await api('security/events', {}, token, 'tenant-b.example.test')).status === 401,
    'TEN-008: tenant A session cannot read security history in tenant B');
  check((await platform('abuse-summary', {}, token)).status === 401,
    'TEN-010: a tenant session cannot read platform audit-monitor metadata');
  const before = sql(`SELECT COUNT(*) FROM c05_identity_audit WHERE tenant_id=UNHEX('${B}')`);
  check((await api('security/events', { tenantId: B }, token)).status === 400,
    'TEN-009: browser-selected audit tenant is rejected before any database operation');
  check(sql(`SELECT COUNT(*) FROM c05_identity_audit WHERE tenant_id=UNHEX('${B}')`) === before,
    'TEN-009: forged audit scope leaves tenant B history unchanged');
}
