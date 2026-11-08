/** PUBLIC platform response only: no tenant IDs, draft data or private fields. */
export interface PublicPlatformBrand {
  readonly revision: number;
  readonly name: string;
  readonly slogan: string;
  readonly descriptor: string;
  readonly primaryColor: string;
  readonly accentColor: string;
  readonly logoUrl: string;
  readonly copyrightText: string;
}

const HEX = /^#[0-9a-fA-F]{6}$/;
const LOCAL_PNG = /^\/assets\/[a-zA-Z0-9-]+\.png$/;

export function isPublicPlatformBrand(value: unknown): value is PublicPlatformBrand {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) return false;
  const brand = value as Record<string, unknown>;
  return Number.isSafeInteger(brand['revision']) && (brand['revision'] as number) > 0
    && validText(brand['name'], 2, 100)
    && validText(brand['slogan'], 2, 160)
    && validText(brand['descriptor'], 2, 180)
    && validText(brand['copyrightText'], 3, 180)
    && typeof brand['primaryColor'] === 'string' && HEX.test(brand['primaryColor'])
    && typeof brand['accentColor'] === 'string' && HEX.test(brand['accentColor'])
    && typeof brand['logoUrl'] === 'string' && LOCAL_PNG.test(brand['logoUrl']);
}

function validText(value: unknown, min: number, max: number): boolean {
  return typeof value === 'string' && value.trim().length >= min
    && value.length <= max && !/[<>\u0000-\u001f\u202d\u202e]/u.test(value);
}
