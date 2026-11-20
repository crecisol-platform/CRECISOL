import { crc32, inflateSync } from 'node:zlib';
export async function verifyProfile({ browser, api, check, confidential, owner, sql, A, B, accountId, uiPort, mailFor, approver, password }) {
  const context = await browser.newContext(); const page = await context.newPage(); let dialogs = 0;
  page.on('dialog', async d => { dialogs++; await d.dismiss(); });
  await page.goto(`http://127.0.0.1:${uiPort}/cuenta`);
  await page.getByRole('region', { name: 'Iniciar sesión', exact: true }).getByLabel('Correo electrónico', { exact: true }).fill(owner.mailbox);
  await page.getByLabel('Contraseña', { exact: true }).fill(owner.password);
  await page.getByRole('button', { name: 'Continuar', exact: true }).click();
  await page.getByLabel('Código de tu aplicación de autenticación').fill(owner.backups.pop());
  await page.getByRole('button', { name: 'Verificar', exact: true }).click();
  await page.getByRole('heading', { name: 'Estado de la sesión', exact: true }).waitFor();
  const token = (await context.cookies()).find(c => c.name === '__Host-crecisol-c05').value; confidential.push(token);
  await page.getByRole('link', { name: 'Mi perfil', exact: true }).click();
  await page.getByRole('button', { name: 'Editar perfil de mi empresa', exact: true }).click();
  const hostileName = '<img src=x onerror=alert("profile-xss")>';
  await page.getByLabel('Nombre visible en esta empresa', { exact: true }).fill(hostileName);
  const saved = page.waitForResponse(r => r.url().endsWith('/profile/save'));
  await page.getByRole('button', { name: 'Guardar mi perfil', exact: true }).click();
  check((await saved).status() === 200, 'Angular saves only the authenticated owner profile in the verified tenant');
  await page.getByLabel('Nombre de mi perfil', { exact: true }).waitFor();
  check(await page.getByLabel('Nombre de mi perfil', { exact: true }).innerText() === hostileName && dialogs === 0 &&
    await page.getByLabel('Nombre de mi perfil', { exact: true }).locator('img').count() === 0,
    'hostile profile markup renders as literal text and cannot execute script');
  await page.getByRole('dialog', { name: 'Editar perfil de mi empresa', exact: true })
    .getByRole('button', { name: 'Cerrar ventana', exact: true }).click();
  await page.locator('dialog.cs-modal:modal').waitFor({ state: 'hidden' });
  await page.getByRole('button', { name: 'Añadir foto de perfil', exact: true }).click();
  const pngBase64 = await page.evaluate(() => {
    const canvas = document.createElement('canvas'); canvas.width = 8; canvas.height = 8;
    const ctx = canvas.getContext('2d'); ctx.fillStyle = '#3366aa'; ctx.fillRect(0, 0, 8, 8);
    return canvas.toDataURL('image/png').split(',')[1];
  });
  const source = Buffer.from(pngBase64, 'base64'); const meta = Buffer.from('Comment\0PRIVATE-EXIF-METADATA-MUST-NOT-SURVIVE');
  const chunk = Buffer.alloc(meta.length + 12); chunk.writeUInt32BE(meta.length); chunk.write('tEXt', 4); meta.copy(chunk, 8);
  chunk.writeUInt32BE(crc32(chunk.subarray(4, -4)), chunk.length - 4);
  const input = Buffer.concat([source.subarray(0, -12), chunk, source.subarray(-12)]);
  await page.getByLabel('Foto de perfil (PNG, JPEG o WebP, hasta 2 MB)', { exact: true }).setInputFiles({ name: 'synthetic-with-metadata.png', mimeType: 'image/png', buffer: input });
  const uploaded = page.waitForResponse(r => r.url().endsWith('/profile/photo-upload'));
  await page.getByRole('button', { name: 'Guardar mi foto de perfil', exact: true }).click();
  check((await uploaded).status() === 200, 'browser rasterizes an uploaded image and server reconstructs a bounded canonical PNG');
  await page.getByRole('dialog', { name: 'Actualizar foto de perfil', exact: true })
    .getByRole('button', { name: 'Cerrar ventana', exact: true }).click();
  await page.locator('dialog.cs-modal:modal').waitFor({ state: 'hidden' });
  const image = page.getByRole('img', { name: 'Mi foto de perfil en esta empresa', exact: true });
  await image.waitFor();
  check(await image.evaluate(async img => { await img.decode(); return img.naturalWidth === 64 && img.naturalHeight === 64; }),
    'actual browser decodes the stored canonical avatar as exactly 64 by 64 pixels');
  const photo = await api('profile/photo', {}, token); const bytes = Buffer.from(photo.data.png, 'base64');
  let pos = 8; const names = [], compressed = [];
  while (pos < bytes.length) {
    const length = bytes.readUInt32BE(pos), name = bytes.toString('ascii', pos + 4, pos + 8);
    check(crc32(bytes.subarray(pos + 4, pos + 8 + length)) === bytes.readUInt32BE(pos + 8 + length), 'canonical PNG chunk checksum verified by independent Node zlib');
    names.push(name); if (name === 'IDAT') compressed.push(bytes.subarray(pos + 8, pos + 8 + length)); pos += length + 12;
  }
  check(names.join(',') === 'IHDR,IDAT,IEND' && inflateSync(Buffer.concat(compressed)).length === 64 * 257,
    'stored avatar contains only canonical raster chunks, fixed pixel budget, and no original EXIF or text metadata');
  check((await api('profile/photo-upload', { pixels: Buffer.from('<svg onload=alert(1)>').toString('base64') }, token)).status === 401,
    'SVG/polyglot containers cannot enter the raw fixed-size raster API');
  check((await api('profile/read', { accountId: 'forged-owner' }, token)).status === 400,
    'browser cannot select another profile owner');
  check((await api('profile/photo', {}, token, 'tenant-b.example.test')).status === 401, 'tenant A cookie token cannot download a profile image through tenant B');
  check(!sql(`SELECT HEX(protected_profile) FROM c05_identity_scoped_profile WHERE tenant_id=UNHEX('${A}') AND account_id=UNHEX('${accountId(owner.mailbox)}')`).includes(Buffer.from(hostileName).toString('hex').toUpperCase()),
    'profile text is authenticated-encrypted with tenant and owner AAD in the actual database');
  check((await api('registration-intents', { mailbox: owner.mailbox }, undefined, 'tenant-b.example.test')).status === 202,
    'an existing identity requests a new independently verified contextual mailbox proof in B');
  const proofCode = await mailFor(owner.mailbox); confidential.push(proofCode);
  const proofId = sql(`SELECT BIN_TO_UUID(proof_id,0) FROM c05_identity_mailbox_proof WHERE tenant_id=UNHEX('${B}') ORDER BY created_at_utc DESC LIMIT 1`);
  const termsB = (await api('terms', {}, undefined, 'tenant-b.example.test')).data;
  const join = { mailbox: owner.mailbox, password: owner.password, code: owner.backups.pop(), proofId, proofCode,
    version: termsB.version, hash: termsB.hash, accepted: 'true' };
  const joined = await Promise.all(Array.from({ length: 4 }, () => api('memberships/request', join, undefined, 'tenant-b.example.test')));
  check(joined.every(r => r.status === 202) && sql(`SELECT CONCAT(COUNT(*),':',MAX(membership_state)) FROM c05_identity_membership WHERE tenant_id=UNHEX('${B}') AND account_id=UNHEX('${accountId(owner.mailbox)}')`) === '1:0',
    'explicit password, MFA, contextual email proof and terms create exactly one pending B relationship without automatic access');
  check((await api('login', { mailbox: owner.mailbox, password: owner.password }, undefined, 'tenant-b.example.test')).status === 401,
    'an existing identity still cannot log in to B before independent membership approval');
  // A privileged fixture establishes only the second tenant's approver; the
  // consumer relationship above and approval below are actual product calls.
  const approverId = accountId('approver@example.test');
  sql(`INSERT INTO c05_identity_membership(tenant_id,membership_id,account_id,membership_state,role_code,approved_at_utc,granted_at_utc,expires_at_utc,created_at_utc)
    VALUES(UNHEX('${B}'),UUID_TO_BIN(UUID(),0),UNHEX('${approverId}'),1,'LOCAL_APPROVER',UTC_TIMESTAMP(6),UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 1 DAY,UTC_TIMESTAMP(6));
    INSERT INTO c05_identity_resource_grant VALUES(UNHEX('${B}'),UUID_TO_BIN(UUID(),0),UNHEX('${approverId}'),UNHEX('${B}'),NULL,2,'MEMBERSHIP_APPROVE',UNHEX('${approverId}'),UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 1 HOUR,NULL,1)`);
  const bApproverLogin = await api('login', { mailbox: 'approver@example.test', password }, undefined, 'tenant-b.example.test');
  const bApprover = await api('mfa/verify', { challenge: bApproverLogin.data.challenge, code: approver.backups.pop() }, undefined, 'tenant-b.example.test');
  check(bApprover.status === 200, 'independent B approver proves their own active membership and MFA'); confidential.push(bApprover.data.sessionToken);
  check((await api('terms/accept', { version: termsB.version, hash: termsB.hash, accepted: 'true' }, bApprover.data.sessionToken, 'tenant-b.example.test')).status === 200,
    'approval authority explicitly accepts the terms of B independently of A');
  const membershipB = sql(`SELECT BIN_TO_UUID(membership_id,0) FROM c05_identity_membership WHERE tenant_id=UNHEX('${B}') AND account_id=UNHEX('${accountId(owner.mailbox)}')`);
  check((await api('memberships/decide', { membershipId: membershipB, decision: 'approve', reason: 'Independent fictional B approval', code: approver.backups.pop() }, bApprover.data.sessionToken, 'tenant-b.example.test')).status === 200,
    'only the real B approval endpoint activates the independently requested consumer relationship');
  const loginB = await api('login', { mailbox: owner.mailbox, password: owner.password }, undefined, 'tenant-b.example.test');
  const sessionB = await api('mfa/verify', { challenge: loginB.data.challenge, code: owner.backups.pop() }, undefined, 'tenant-b.example.test');
  check(sessionB.status === 200, 'same identity authenticates through a separately authorized tenant B membership without a tenant selector');
  confidential.push(sessionB.data.sessionToken);
  const profileB = await api('profile/read', {}, sessionB.data.sessionToken, 'tenant-b.example.test');
  check(profileB.status === 200 && profileB.data.displayName === '' && !profileB.data.hasPhoto,
    'a second membership inherits neither the first tenant profile nor its image');
  sql(`INSERT INTO c05_identity_scoped_profile(tenant_id,account_id,protected_profile,photo_state,updated_at_utc)
    SELECT UNHEX('${B}'),account_id,protected_profile,0,UTC_TIMESTAMP(6) FROM c05_identity_scoped_profile WHERE tenant_id=UNHEX('${A}') AND account_id=UNHEX('${accountId(owner.mailbox)}')`);
  check((await api('profile/read', {}, sessionB.data.sessionToken, 'tenant-b.example.test')).status === 401,
    'even privileged copying of encrypted profile A into B fails authentication of tenant-bound AAD');
  sql(`DELETE FROM c05_identity_scoped_profile WHERE tenant_id=UNHEX('${B}') AND account_id=UNHEX('${accountId(owner.mailbox)}')`);
  const removed = page.waitForResponse(r => r.url().endsWith('/profile/photo-remove'));
  await page.getByRole('button', { name: 'Eliminar foto de perfil', exact: true }).click();
  await page.getByRole('button', { name: 'Confirmar eliminación de mi foto', exact: true }).click();
  check((await removed).status() === 200, 'Angular completes own-avatar removal on the server');
  check((await api('profile/photo', {}, token)).status === 401, 'removing an own avatar removes subsequent authorized reads');
  await context.close();
}
