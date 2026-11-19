// Actual pixels are decoded from the UI; the same seed must be embedded in
// the QR and accepted only under RFC6238 SHA1. Never print URI/seed/codes.
import { createRequire } from 'node:module';
const require = createRequire(new URL('../frontend/package.json', import.meta.url));
const jsQR = require('jsqr');

export async function verifySha1EnrollmentQr(page, imageName, seed, check) {
  const img = page.getByRole('img', { name: imageName, exact: true });
  const pixels = await img.evaluate(async image => {
    await image.decode();
    const canvas = document.createElement('canvas');
    canvas.width = image.naturalWidth;
    canvas.height = image.naturalHeight;
    const ctx = canvas.getContext('2d', { willReadFrequently: true });
    ctx.drawImage(image, 0, 0);
    return { width: canvas.width, height: canvas.height,
      rgba: [...ctx.getImageData(0, 0, canvas.width, canvas.height).data] };
  });
  const result = jsQR(new Uint8ClampedArray(pixels.rgba), pixels.width, pixels.height);
  check(Boolean(result?.data?.startsWith('otpauth://totp/')), 'QR pixels decode to TOTP otpauth URI');
  if (!result?.data) return;
  const url = new URL(result.data);
  check(url.protocol === 'otpauth:' && url.hostname === 'totp' &&
    decodeURIComponent(url.pathname).startsWith('/CRECISOL:') &&
    url.searchParams.get('secret') === seed &&
    url.searchParams.get('issuer') === 'CRECISOL' &&
    url.searchParams.get('algorithm') === 'SHA1' &&
    url.searchParams.get('digits') === '6' &&
    url.searchParams.get('period') === '30' &&
    [...url.searchParams].length === 5,
    'scannable QR matches one-time factor and SHA1-only contract');
}
