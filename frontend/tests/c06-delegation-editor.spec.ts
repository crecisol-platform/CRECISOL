/** C06-11 browser presentation tests; synthetic wire, not live authorization.
 * Real MySQL/MFA HTTP is independently exercised by the .NET E2 suite.
 */
import { test, expect, type Page, type TestInfo } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
const sizes=[{width:320,height:720},{width:390,height:844},{width:768,height:1024},{width:1024,height:768},{width:1440,height:900}];
const selector={branchId:'11111111-2222-4333-8444-555555555555',resourceId:'22222222-3333-4444-8555-666666666666',
  membershipId:'33333333-4444-4555-8666-777777777777',roleId:'44444444-5555-4666-8777-888888888888'};
const other={...selector,membershipId:'55555555-6666-4777-8888-999999999999'};
const rev=(canAssign:boolean,generation=canAssign?'0':'1')=>({roleCode:'tenant-staff',roleRevision:'1',membershipRevision:'9007199254740993',
  ceilingRevision:'1',grantRevision:generation,canAssign,canRevoke:!canAssign,
  maxExpiresAtUtc:canAssign?new Date(Date.now()+60*60_000).toISOString():null});
async function setup(page:Page,denied=false,confirmDenied=false,reissue=false){
  let phase=0;const writes: Array<Record<string,string>>=[];
  await page.route('**/api/v1/**',route=>route.fulfill({status:404,body:''}));
  await page.route('**/api/v1/identity/browser/restore',route=>route.fulfill({json:{
    csrfToken:'a'.repeat(64),expiresAtUtc:new Date(Date.now()+30*60_000).toISOString(),idleMinutes:30
  }}));
  await page.route('**/api/v1/identity/sessions/keepalive',route=>route.fulfill({json:{accepted:true}}));
  await page.route('**/api/v1/identity/delegation/catalog',route=>{
    if(denied)return route.fulfill({status:401,body:''});
    const body=route.request().postDataJSON();
    if(body.cursor==='start')return route.fulfill({json:{items:phase===0?
      [{...selector,inspection:rev(true,reissue?'2':'0')},{...other,inspection:rev(false,reissue?'3':'1')}]:[{...other,inspection:rev(false)}],nextCursor:null}});
    return route.fulfill({status:400,body:''});
  });
  await page.route('**/api/v1/identity/delegation/inspect',route=>{
    const body=route.request().postDataJSON();
    const assigning=body.membershipId===selector.membershipId;
    return route.fulfill({json:rev(assigning,reissue?(assigning?'2':'3'):(assigning?'0':'1'))});
  });
  await page.route('**/api/v1/identity/delegation/prepare',route=>{
    const body=route.request().postDataJSON();writes.push(body);
    expect(route.request().headers()['x-crecisol-csrf']).toBe('a'.repeat(64));
    expect(body.membershipRevision).toBe('9007199254740993');
    expect(body).not.toHaveProperty('tenantId');expect(body).not.toHaveProperty('code');
    return route.fulfill({json:{challenge:'b'.repeat(64),expiresAtUtc:new Date(Date.now()+240_000).toISOString()}});
  });
  await page.route('**/api/v1/identity/delegation/confirm',route=>{
    const body=route.request().postDataJSON();writes.push(body);
    expect(body.challenge).toBe('b'.repeat(64));expect(body.code).toBe('123456');
    if(confirmDenied)return route.fulfill({status:401,body:''});
    phase=1;return route.fulfill({json:{accepted:true}});
  });
  await page.goto('/cuenta?vista=administracion');
  await expect(page.getByRole('navigation',{name:'Mi cuenta'})).toBeVisible();
  return writes;
}
async function capture(page:Page,info:TestInfo,name:string){
  const overflow=await page.evaluate(()=>document.documentElement.scrollWidth-document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(2);
  const axe=await new AxeBuilder({page}).analyze();
  expect(axe.violations.filter(v=>v.impact==='critical'||v.impact==='serious')).toEqual([]);
  await page.screenshot({path:info.outputPath(name+'.png'),fullPage:true,animations:'disabled'});
}
for(const viewport of sizes)test('C06 editor visual and secure interaction '+viewport.width,async({page},info)=>{
  await page.setViewportSize(viewport);const writes=await setup(page);
  const section=page.getByRole('region',{name:'Administración de permisos'});
  await expect(section).toBeVisible();await section.getByRole('button',{name:'Gestionar permisos'}).click();
  await expect(section.getByRole('list',{name:'Candidatos autorizados'}).getByRole('listitem')).toHaveCount(2);
  await capture(page,info,'catalog-'+viewport.width);
  await section.getByRole('button',{name:/Revisar concesión/}).click();
  const dialog=page.getByRole('dialog',{name:'Revisión de permiso'});
  await expect(dialog).toBeVisible();await expect(dialog.getByText(selector.membershipId,{exact:true})).toBeVisible();
  await capture(page,info,'review-'+viewport.width);
  await dialog.getByLabel('Motivo de la operación').fill('Finalidad sintética autorizada');
  await dialog.getByRole('button',{name:'Preparar confirmación'}).click();
  await expect(dialog.getByLabel('Código de seis dígitos')).toBeVisible();
  await capture(page,info,'mfa-'+viewport.width);
  await dialog.getByLabel('Código de seis dígitos').fill('123456');
  await dialog.getByRole('button',{name:'Confirmar concesión'}).click();
  await expect(dialog).not.toBeVisible();
  await expect(section.getByRole('status').filter({hasText:'Permiso registrado por el servidor.'})).toBeVisible();
  expect(writes).toHaveLength(2);expect(writes[1]).toEqual({...writes[0],challenge:'b'.repeat(64),code:'123456'});
  expect(await page.evaluate(()=>JSON.stringify({...localStorage,...sessionStorage}))).not.toContain('b'.repeat(64));
  await capture(page,info,'success-'+viewport.width);
});
test('C06 denied feature exposes no catalog or actions',async({page})=>{
  await setup(page,true);
  await expect(page.getByRole('region',{name:'Administración de permisos'})).toHaveCount(0);
});
test('C06 failed MFA clears challenge and prior authorized candidates',async({page},info)=>{
  await page.setViewportSize({width:390,height:844});await setup(page,false,true);
  const section=page.getByRole('region',{name:'Administración de permisos'});
  await section.getByRole('button',{name:'Gestionar permisos'}).click();
  await section.getByRole('button',{name:/Revisar revocación/}).click();
  const dialog=page.getByRole('dialog',{name:'Revisión de permiso'});
  await dialog.getByLabel('Motivo de la operación').fill('Revocación sintética autorizada');
  await dialog.getByRole('button',{name:'Preparar confirmación'}).click();
  await dialog.getByLabel('Código de seis dígitos').fill('123456');
  await dialog.getByRole('button',{name:'Confirmar revocación'}).click();
  await expect(dialog.getByText('No se pudo confirmar el resultado.',{exact:false}).first()).toBeVisible();
  await expect(dialog.getByLabel('Código de seis dígitos')).toHaveCount(0);
  await capture(page,info,'denial-390');
  await dialog.getByRole('button',{name:'Cerrar ventana'}).click();
  await expect(section.getByRole('list',{name:'Candidatos autorizados'})).toHaveCount(0);
  await expect(section.getByText('Catálogo no disponible')).toBeVisible();
});

test('C06 new grant after revocation keeps exact terminal revision and distinct user explanation',async({page},info)=>{
  await page.setViewportSize({width:390,height:844});const writes=await setup(page,false,false,true);
  const section=page.getByRole('region',{name:'Administración de permisos'});
  await section.getByRole('button',{name:'Gestionar permisos'}).click();
  await expect(section.getByText('Nuevo otorgamiento disponible')).toBeVisible();
  await section.getByRole('button',{name:/Revisar concesión/}).click();
  const dialog=page.getByRole('dialog',{name:'Revisión de permiso'});
  await expect(dialog.getByText('Se registrará un permiso nuevo.',{exact:false})).toBeVisible();
  await capture(page,info,'reissue-review-390');
  await dialog.getByLabel('Motivo de la operación').fill('Nuevo otorgamiento sintético');
  await dialog.getByRole('button',{name:'Preparar confirmación'}).click();
  await dialog.getByLabel('Código de seis dígitos').fill('123456');
  await dialog.getByRole('button',{name:'Confirmar concesión'}).click();
  await expect(dialog).not.toBeVisible();
  expect(writes).toHaveLength(2);
  expect(writes[0].grantRevision).toBe('2');expect(writes[0].command).toBe('assign');
  expect(writes[1]).toEqual({...writes[0],challenge:'b'.repeat(64),code:'123456'});
});
test('C06 later revocation preserves revision three in both requests',async({page})=>{
  const writes=await setup(page,false,false,true);
  const section=page.getByRole('region',{name:'Administración de permisos'});
  await section.getByRole('button',{name:'Gestionar permisos'}).click();
  await section.getByRole('button',{name:/Revisar revocación/}).click();
  const dialog=page.getByRole('dialog',{name:'Revisión de permiso'});
  await dialog.getByLabel('Motivo de la operación').fill('Revocación de la nueva generación');
  await dialog.getByRole('button',{name:'Preparar confirmación'}).click();
  await dialog.getByLabel('Código de seis dígitos').fill('123456');
  await dialog.getByRole('button',{name:'Confirmar revocación'}).click();
  await expect(dialog).not.toBeVisible();
  expect(writes).toHaveLength(2);expect(writes[0].grantRevision).toBe('3');
  expect(writes[0].command).toBe('revoke');expect(writes[1].grantRevision).toBe('3');
});
