/** C06-11: strict presentation-only decoding. No result is a grant.
 * Keep all selectors, revisions, challenges and reasons only in JS memory.
 */
export interface DelegationSelection {
  readonly branchId: string; readonly resourceId: string;
  readonly membershipId: string; readonly roleId: string;
}
export interface DelegationInspection {
  readonly roleCode: 'tenant-staff' | 'tenant-viewer' | 'tenant-branch-manager';
  readonly roleRevision: string; readonly membershipRevision: string;
  readonly ceilingRevision: string; readonly grantRevision: '0' | '1';
  readonly canAssign: boolean; readonly canRevoke: boolean;
  readonly maxExpiresAtUtc: string | null;
}
export interface DelegationCandidate extends DelegationSelection {
  readonly inspection: DelegationInspection;
}
export interface DelegationPage {
  readonly items: readonly DelegationCandidate[]; readonly nextCursor: string | null;
}
export interface DelegationIntent extends DelegationSelection {
  readonly command: 'assign' | 'revoke';
  readonly roleRevision: string; readonly membershipRevision: string;
  readonly ceilingRevision: string; readonly grantRevision: string;
  readonly expiresAtUtc: string; readonly reason: string;
}
export interface DelegationChallenge {
  readonly challenge: string; readonly expiresAtUtc: string;
}
const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const zero = '00000000-0000-0000-0000-000000000000';
const guid = (v: unknown): v is string => typeof v === 'string' && guidPattern.test(v) && v !== zero;
const revision = (v: unknown): v is string => typeof v === 'string' && /^[1-9][0-9]{0,18}$/.test(v) && BigInt(v) < 9223372036854775807n;
const utc = (v: unknown): v is string => typeof v === 'string' &&
  /^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(?:\.\d{1,7})?(?:Z|\+00:00)$/.test(v) &&
  Number.isFinite(Date.parse(v));
const cursor = (v: unknown): v is string => typeof v === 'string' && /^[A-Za-z0-9+/]{134}==$/.test(v);
function exact(value: unknown, keys: readonly string[]): Record<string, unknown> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new Error('delegation_invalid');
  const r = value as Record<string, unknown>;
  if (Object.keys(r).length !== keys.length || !keys.every(k => Object.hasOwn(r, k))) throw new Error('delegation_invalid');
  return r;
}
export function decodeInspection(value: unknown): DelegationInspection {
  const r = exact(value, ['roleCode','roleRevision','membershipRevision','ceilingRevision','grantRevision','canAssign','canRevoke','maxExpiresAtUtc']);
  if (!['tenant-staff','tenant-viewer','tenant-branch-manager'].includes(String(r['roleCode'])) ||
      !revision(r['roleRevision']) || !revision(r['membershipRevision']) || !revision(r['ceilingRevision']) ||
      typeof r['canAssign'] !== 'boolean' || typeof r['canRevoke'] !== 'boolean' || r['canAssign'] === r['canRevoke'] ||
      (r['canAssign'] ? r['grantRevision'] !== '0' || !utc(r['maxExpiresAtUtc']) || Date.parse(r['maxExpiresAtUtc']) <= Date.now()
        : r['grantRevision'] !== '1' || r['maxExpiresAtUtc'] !== null)) throw new Error('delegation_invalid');
  return Object.freeze({ ...r }) as unknown as DelegationInspection;
}
export function decodeDelegationPage(value: unknown): DelegationPage {
  const r = exact(value, ['items','nextCursor']);
  if (!Array.isArray(r['items']) || r['items'].length > 8 ||
      (r['nextCursor'] !== null && !cursor(r['nextCursor']))) throw new Error('delegation_invalid');
  const seen = new Set<string>();
  const items = r['items'].map(item => {
    const row = exact(item, ['branchId','resourceId','membershipId','roleId','inspection']);
    const b=row['branchId'], x=row['resourceId'], m=row['membershipId'], role=row['roleId'];
    if (!guid(b) || !guid(x) || !guid(m) || !guid(role)) throw new Error('delegation_invalid');
    const key = [b,x,m,role].join('/').toLowerCase();
    if (seen.has(key)) throw new Error('delegation_invalid');
    seen.add(key);
    return Object.freeze({branchId:b,resourceId:x,membershipId:m,roleId:role,inspection:decodeInspection(row['inspection'])});
  });
  return Object.freeze({items:Object.freeze(items),nextCursor:r['nextCursor'] as string | null});
}
export function decodeDelegationChallenge(value: unknown): DelegationChallenge {
  const r = exact(value,['challenge','expiresAtUtc']);
  const token=r['challenge'], expires=r['expiresAtUtc'];
  if (typeof token !== 'string' || !/^[0-9a-f]{64}$/i.test(token) || !utc(expires) ||
      Date.parse(expires) <= Date.now() || Date.parse(expires) > Date.now()+300_000) throw new Error('delegation_invalid');
  return Object.freeze({challenge:token,expiresAtUtc:expires});
}
export function decodeDelegationAccepted(value: unknown): void {
  const r=exact(value,['accepted']);
  if (r['accepted'] !== true) throw new Error('delegation_invalid');
}
export function exactSelection(row: DelegationSelection): DelegationSelection {
  return {branchId:row.branchId,resourceId:row.resourceId,membershipId:row.membershipId,roleId:row.roleId};
}
export function delegationRoleLabel(role: DelegationInspection['roleCode']): string {
  switch(role) {
    case 'tenant-staff': return 'Colaborador';
    case 'tenant-viewer': return 'Consulta';
    case 'tenant-branch-manager': return 'Responsable de sucursal';
  }
}
export function delegationKey(row: DelegationSelection): string {
  return [row.branchId,row.resourceId,row.membershipId,row.roleId].join('/');
}
/** .NET round-trip UTC format, preserving the required microsecond bound. */
export function delegationExpiry(value: number): string {
  return new Date(value).toISOString().replace(/Z$/, '0000+00:00');
}
