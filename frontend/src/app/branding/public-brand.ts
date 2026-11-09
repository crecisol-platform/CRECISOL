/**
 * C01-05: untrusted JSON at the same-origin C01-04 API boundary.
 * Public branding only; never accept tenant, status, preview or private fields.
 */
export interface PublicPresentation {
  readonly faviconUrl: string;
  readonly typographyToken: 'system' | 'humanist' | 'editorial';
  readonly iconSetToken: 'outline' | 'solid';
  readonly seoTitle: string;
  readonly seoDescription: string;
  readonly copyrightNotice: string | null;
}

export interface PublishedPlatformBrand {
  readonly revision: number;
  readonly name: string;
  readonly slogan: string;
  readonly descriptor: string;
  readonly primaryColor: string;
  readonly accentColor: string;
  readonly logoUrl: string;
  readonly websiteUrl: string | null;
  readonly publishedAt: string;
  readonly presentation: PublicPresentation | null;
}

export type BrandLoadState =
  | { readonly kind: 'loading' }
  | { readonly kind: 'ready'; readonly brand: PublishedPlatformBrand }
  | { readonly kind: 'unavailable' };

const colorPattern = /^#[0-9a-fA-F]{6}$/;
const assetPattern = /^\/assets\/[a-zA-Z0-9-]{1,80}\.(png|webp)$/;
const unsafeText = /[\u0000-\u001f\u007f<>\u200b\u202a-\u202e\u2066-\u2069]/;
const platformKeys = new Set([
  'revision', 'name', 'slogan', 'descriptor', 'primaryColor', 'accentColor',
  'logoUrl', 'websiteUrl', 'publishedAt', 'presentation'
]);
const presentationKeys = new Set([
  'faviconUrl', 'typographyToken', 'iconSetToken', 'seoTitle',
  'seoDescription', 'copyrightNotice'
]);

function record(value: unknown, allowed: ReadonlySet<string>): Record<string, unknown> {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    throw new Error('Invalid public identity object.');
  }
  const object = value as Record<string, unknown>;
  if (Object.keys(object).some(key => !allowed.has(key))) {
    throw new Error('Unexpected private branding property.');
  }
  return object;
}

function safeText(value: unknown, min: number, max: number): string {
  if (typeof value !== 'string' || value.length < min || value.length > max ||
      value.trim().length === 0 || unsafeText.test(value)) {
    throw new Error('Invalid public identity text.');
  }
  return value;
}

function safeAsset(value: unknown): string {
  if (typeof value !== 'string' || !assetPattern.test(value)) {
    throw new Error('Invalid local identity asset path.');
  }
  return value;
}

function safeColor(value: unknown): string {
  if (typeof value !== 'string' || !colorPattern.test(value)) {
    throw new Error('Invalid public identity color.');
  }
  return value;
}

function optionalOrigin(value: unknown): string | null {
  if (value === null) return null;
  if (typeof value !== 'string' || value.length > 255) {
    throw new Error('Invalid website origin.');
  }
  let url: URL;
  try {
    url = new URL(value);
  } catch {
    throw new Error('Invalid website origin.');
  }
  if (url.protocol !== 'https:' || url.username || url.password ||
      url.pathname !== '/' || url.search || url.hash ||
      url.hostname === 'localhost' || url.hostname.endsWith('.localhost') ||
      !url.hostname.includes('.') || url.port) {
    throw new Error('Unsupported website origin.');
  }
  // Ownership is NOT established here: no canonical or outbound website link.
  return value;
}

function safePresentation(value: unknown): PublicPresentation | null {
  if (value === null) return null;
  const obj = record(value, presentationKeys);
  const typographyToken = obj['typographyToken'];
  const iconSetToken = obj['iconSetToken'];
  if (typographyToken !== 'system' && typographyToken !== 'humanist' &&
      typographyToken !== 'editorial') {
    throw new Error('Unknown typography token.');
  }
  if (iconSetToken !== 'outline' && iconSetToken !== 'solid') {
    throw new Error('Unknown icon token.');
  }
  return {
    faviconUrl: safeAsset(obj['faviconUrl']),
    typographyToken,
    iconSetToken,
    seoTitle: safeText(obj['seoTitle'], 2, 120),
    seoDescription: safeText(obj['seoDescription'], 2, 260),
    copyrightNotice: obj['copyrightNotice'] === null
      ? null : safeText(obj['copyrightNotice'], 2, 220)
  };
}

export function parsePublishedPlatformBrand(payload: unknown): PublishedPlatformBrand {
  const obj = record(payload, platformKeys);
  if (typeof obj['revision'] !== 'number' || !Number.isSafeInteger(obj['revision']) ||
      obj['revision'] < 1) {
    throw new Error('Invalid published revision.');
  }
  const publishedAt = obj['publishedAt'];
  if (typeof publishedAt !== 'string' || publishedAt.length > 40 ||
      !/T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|\+00:00)$/.test(publishedAt) ||
      Number.isNaN(Date.parse(publishedAt))) {
    throw new Error('Invalid publication timestamp.');
  }
  return {
    revision: obj['revision'],
    name: safeText(obj['name'], 2, 100),
    slogan: safeText(obj['slogan'], 2, 160),
    descriptor: safeText(obj['descriptor'], 2, 180),
    primaryColor: safeColor(obj['primaryColor']),
    accentColor: safeColor(obj['accentColor']),
    logoUrl: safeAsset(obj['logoUrl']),
    websiteUrl: optionalOrigin(obj['websiteUrl']),
    publishedAt,
    presentation: safePresentation(obj['presentation'])
  };
}
