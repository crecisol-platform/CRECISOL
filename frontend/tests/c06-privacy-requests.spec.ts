/** Synthetic browser presentation only, not legal fulfilment or live authority. */
import {test,expect,type Page,type TestInfo} from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
const sizes=[{width:320,height:720},{width:390,height:844},{width:768,height:1024},{width:1024,height:768},{width:1440,height:900}];
const requestId='11111111-2222-4333-8444-555555555555';
const receipt={requestId,right:'erase',category:'profile',submittedAtUtc:new Date().toISOString(),status:'received',
  assessment:{identity:'mfa-verified',purpose:'pending',legalBasis:'pending',retention:'pending',legalHold:'pending',disposition:'pending'}};
async function setup(page:Page,denied=false,failed=false){
  let submitted=false;const writes:Record<string,string>[]=[];
  await page.route('**/api/v1/**',route=>route.fulfill({status:404,body:''}));
  await page.route('**/api/v1/identity/browser/restore',route=>route.fulfill({json:{csrfToken:'a'.repeat(64),
    expiresAtUtc:new Date(Date.now()+30*60_000).toISOString(),idleMinutes:30}}));
  await page.route('**/api/v1/identity/sessions/keepalive',route=>route.fulfill({json:{accepted:true}}));
  await page.route('**/api/v1/identity/privacy/requests/list',route=>denied?route.fulfill({status:401,body:''}):
    route.fulfill({json:{items:submitted?[receipt]:[],nextCursor:null}}));
  await page.route('**/api/v1/identity/privacy/requests/prepare',route=>{
    const body=route.request().postDataJSON();writes.push(body);
    expect(body.right).toBe('erase');expect(body.category).toBe('profile');
    expect(body).not.toHaveProperty('accountId');expect(body).not.toHaveProperty('tenantId');
    return route.fulfill({json:{challenge:'b'.repeat(64),expiresAtUtc:new Date(Date.now()+240_000).toISOString()}});
  });
  await page.route('**/api/v1/identity/privacy/requests/confirm',route=>{
    const body=route.request().postDataJSON();writes.push(body);
    expect(route.request().headers()['x-crecisol-csrf']).toBe('a'.repeat(64));
    expect(body.challenge).toBe('b'.repeat(64));expect(body.code).toBe('123456');
    if(failed)return route.fulfill({status:401,body:''});
    submitted=true;return route.fulfill({json:receipt});
  });
  await page.goto('/cuenta?vista=perfil');await expect(page.getByRole('navigation',{name:'Mi cuenta'})).toBeVisible();
  return writes;
}
async function capture(page:Page,info:TestInfo,name:string){
  expect(await page.evaluate(()=>document.documentElement.scrollWidth-document.documentElement.clientWidth)).toBeLessThanOrEqual(2);
  const axe=await new AxeBuilder({page}).analyze();expect(axe.violations.filter(x=>x.impact==='critical'||x.impact==='serious')).toEqual([]);
  await page.screenshot({path:info.outputPath(name+'.png'),fullPage:true,animations:'disabled'});
}
for(const viewport of sizes)test('C06 privacy portal receipt and secure review '+viewport.width,async({page},info)=>{
  await page.setViewportSize(viewport);const writes=await setup(page);
  const section=page.getByRole('region',{name:'Derechos de privacidad'});
  await expect(section).toBeVisible();await section.getByRole('button',{name:'Gestionar mi privacidad'}).click();
  await expect(section.getByRole('button',{name:/Acceso a mis datos/})).toBeVisible();
  await expect(section.getByRole('button',{name:/Rectificación/})).toBeVisible();
  await expect(section.getByRole('button',{name:/Oposición/})).toBeVisible();
  // No horizontal page overflow is insufficient: the two new panels must
  // actually span the account grid rather than occupy a narrow half-column.
  const widthRatio=await section.evaluate(el=>{
    const grid=el.closest('.identity-workspace');
    if(!grid)return 0;
    const style=getComputedStyle(grid);
    const available=grid.clientWidth-parseFloat(style.paddingLeft)-parseFloat(style.paddingRight);
    return available>0?el.getBoundingClientRect().width/available:0;
  });
  expect(widthRatio).toBeGreaterThanOrEqual(.95);
  await capture(page,info,'privacy-home-'+viewport.width);
  await section.getByRole('button',{name:/Eliminación/}).click();
  const dialog=page.getByRole('dialog',{name:'Nueva solicitud de privacidad'});
  await expect(dialog).toBeVisible();await capture(page,info,'privacy-review-'+viewport.width);
  await dialog.getByLabel('Categoría de datos').selectOption('profile');
  await dialog.getByLabel('Detalle de la solicitud').fill('Evaluación sintética de mis datos');
  await dialog.getByRole('button',{name:'Preparar verificación'}).click();
  await expect(dialog.getByLabel('Código de seis dígitos')).toBeVisible();await capture(page,info,'privacy-mfa-'+viewport.width);
  await dialog.getByLabel('Código de seis dígitos').fill('123456');
  await dialog.getByRole('button',{name:'Confirmar solicitud'}).click();
  await expect(dialog).not.toBeVisible();
  await expect(section.getByRole('status').filter({hasText:'Solicitud recibida'})).toBeVisible();
  await expect(section.getByRole('list',{name:'Mis recibos de privacidad'}).getByRole('listitem')).toHaveCount(1);
  expect(writes).toHaveLength(2);expect(writes[1]).toEqual({...writes[0],challenge:'b'.repeat(64),code:'123456'});
  await expect(section.getByText('Recibida · evaluación pendiente')).toBeVisible();
  expect(await page.evaluate(()=>JSON.stringify({...localStorage,...sessionStorage}))).not.toContain('Evaluación sintética');
  await capture(page,info,'privacy-receipt-'+viewport.width);
});
test('C06 absent feature does not expose private portal',async({page})=>{
  await setup(page,true);await expect(page.getByRole('region',{name:'Derechos de privacidad'})).toHaveCount(0);
});
test('C06 uncertain confirmation clears proof and does not imply receipt',async({page},info)=>{
  await page.setViewportSize({width:390,height:844});await setup(page,false,true);
  const section=page.getByRole('region',{name:'Derechos de privacidad'});await section.getByRole('button',{name:'Gestionar mi privacidad'}).click();
  await section.getByRole('button',{name:/Eliminación/}).click();const dialog=page.getByRole('dialog',{name:'Nueva solicitud de privacidad'});
  await dialog.getByLabel('Categoría de datos').selectOption('profile');await dialog.getByLabel('Detalle de la solicitud').fill('Evaluación sintética de mis datos');
  await dialog.getByRole('button',{name:'Preparar verificación'}).click();
  await dialog.getByLabel('Código de seis dígitos').fill('123456');await dialog.getByRole('button',{name:'Confirmar solicitud'}).click();
  await expect(dialog.getByText('No se pudo confirmar el resultado.',{exact:false})).toHaveCount(1);
  await expect(dialog.getByText('No se pudo confirmar el resultado.',{exact:false})).toBeVisible();
  await expect(dialog.getByLabel('Código de seis dígitos')).toHaveCount(0);await capture(page,info,'privacy-denial-390');
  await dialog.getByRole('button',{name:'Cerrar ventana'}).click();
  await expect(section.getByText('Consulta no disponible')).toBeVisible();await expect(section.getByText('Solicitud recibida',{exact:true})).toHaveCount(0);
});
