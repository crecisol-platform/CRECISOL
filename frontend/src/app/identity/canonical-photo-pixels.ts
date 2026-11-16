/** Decode locally, send only bounded RGBA8 pixels. Original containers never reach storage. */
export async function canonicalPhotoPixels(file: File): Promise<string> {
    if (file.size < 1 || file.size > 2 * 1024 * 1024 || !['image/png', 'image/jpeg', 'image/webp'].includes(file.type))
      throw new Error('identity_unavailable');
    const signature = new Uint8Array(await file.arrayBuffer());
    const png = [137, 80, 78, 71, 13, 10, 26, 10].every((b, i) => signature[i] === b);
    const jpeg = signature[0] === 255 && signature[1] === 216 && signature[2] === 255;
    const webp = String.fromCharCode(...signature.slice(0, 4)) === 'RIFF' && String.fromCharCode(...signature.slice(8, 12)) === 'WEBP';
    if ((!png && !jpeg && !webp) || (png && file.type !== 'image/png') || (jpeg && file.type !== 'image/jpeg') ||
        (webp && file.type !== 'image/webp')) throw new Error('identity_unavailable');
    const dimensions = rasterDimensions(signature, png ? 'png' : jpeg ? 'jpeg' : 'webp');
    if (!dimensions || dimensions.some(n => n < 1 || n > 4096)) throw new Error('identity_unavailable');
    const bitmap = await createImageBitmap(file);
    try {
      if (bitmap.width > 4096 || bitmap.height > 4096 || bitmap.width < 1 || bitmap.height < 1) throw new Error('identity_unavailable');
      const canvas = document.createElement('canvas'); canvas.width = 64; canvas.height = 64;
      const context = canvas.getContext('2d'); if (!context) throw new Error('identity_unavailable');
      const side = Math.min(bitmap.width, bitmap.height);
      context.drawImage(bitmap, (bitmap.width - side) / 2, (bitmap.height - side) / 2, side, side, 0, 0, 64, 64);
      const pixels = btoa(Array.from(context.getImageData(0, 0, 64, 64).data, b => String.fromCharCode(b)).join(''));
      return pixels;
    } finally { bitmap.close(); }
}

/** Bound allocation before invoking the browser decoder, including the inner
 * VP8/VP8L frame dimensions of an extended WebP container. */
function rasterDimensions(bytes: Uint8Array, kind: string): [number, number] | null {
  const data = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
  if (kind === 'png') return bytes.length >= 24 ? [data.getUint32(16), data.getUint32(20)] : null;
  if (kind === 'jpeg') {
    let position = 2;
    while (position + 4 <= bytes.length) {
      if (bytes[position++] !== 255) return null;
      while (bytes[position] === 255) position++;
      const marker = bytes[position++];
      if (marker === 0xd9 || marker === 0xda) return null;
      if (marker === 0x01 || (marker >= 0xd0 && marker <= 0xd8)) continue;
      if (position + 2 > bytes.length) return null;
      const length = data.getUint16(position);
      if (length < 2 || position + length > bytes.length) return null;
      if ([0xc0, 0xc1, 0xc2, 0xc3, 0xc5, 0xc6, 0xc7, 0xc9, 0xca, 0xcb, 0xcd, 0xce, 0xcf].includes(marker))
        return length >= 8 ? [data.getUint16(position + 5), data.getUint16(position + 3)] : null;
      position += length;
    }
    return null;
  }
  let frame: [number, number] | null = null;
  for (let p = 12; p + 8 <= bytes.length;) {
    const tag = String.fromCharCode(...bytes.slice(p, p + 4)), size = data.getUint32(p + 4, true), start = p + 8;
    if (size > bytes.length - start || tag === 'ANIM' || tag === 'ANMF') return null;
    if (tag === 'VP8X') {
      if (size < 10 || (bytes[start] & 2)) return null;
      const width = 1 + bytes[start + 4] + (bytes[start + 5] << 8) + (bytes[start + 6] << 16);
      const height = 1 + bytes[start + 7] + (bytes[start + 8] << 8) + (bytes[start + 9] << 16);
      if (width > 4096 || height > 4096) return null;
    }
    if (tag === 'VP8L' && size >= 5 && bytes[start] === 0x2f) {
      const packed = data.getUint32(start + 1, true); frame = [(packed & 0x3fff) + 1, ((packed >>> 14) & 0x3fff) + 1];
    }
    if (tag === 'VP8 ' && size >= 10 && bytes[start + 3] === 0x9d && bytes[start + 4] === 1 && bytes[start + 5] === 0x2a)
      frame = [data.getUint16(start + 6, true) & 0x3fff, data.getUint16(start + 8, true) & 0x3fff];
    if (frame && frame.some(n => n < 1 || n > 4096)) return null;
    p = start + size + (size & 1);
  }
  return frame;
}
