// CRECISOL C05 — only RFC6238 SHA1 enrollment; QR never leaves this browser.
// The backend's URI and one-time seed are trusted only after contract validation.
export async function createTotpQr(secret: string, uri: string): Promise<string> {
  if (!/^[A-Z2-7]{32}$/.test(secret) || uri.length > 512)
    throw new Error('identity_unavailable');
  let parsed: URL;
  try { parsed = new URL(uri); }
  catch { throw new Error('identity_unavailable'); }
  if (parsed.protocol !== 'otpauth:' || parsed.hostname !== 'totp' ||
      !decodeURIComponent(parsed.pathname).startsWith('/CRECISOL:') ||
      parsed.searchParams.get('secret') !== secret ||
      parsed.searchParams.get('issuer') !== 'CRECISOL' ||
      parsed.searchParams.get('algorithm') !== 'SHA1' ||
      parsed.searchParams.get('digits') !== '6' ||
      parsed.searchParams.get('period') !== '30' ||
      [...parsed.searchParams.keys()].length !== 5 ||
      parsed.hash !== '' || parsed.username !== '' || parsed.password !== '')
    throw new Error('identity_unavailable');
  const { default: QRCode } = await import('qrcode');
  const image = await QRCode.toDataURL(uri, {
    width: 256, margin: 2, errorCorrectionLevel: 'M'
  });
  if (!image.startsWith('data:image/png;base64,') || image.length > 100000)
    throw new Error('identity_unavailable');
  return image;
}
