/** Self-service intake only. The UI never decides legal basis, retention,
 * disclosure or erasure, and never stores request details outside memory. */
export const privacyRights=['access','rectify','erase','object'] as const;
export const privacyCategories=['profile','identity','security','case-records'] as const;
export type PrivacyRight=typeof privacyRights[number];
export type PrivacyCategory=typeof privacyCategories[number];
export interface PrivacyIntent {
  readonly requestKey:string; readonly right:PrivacyRight; readonly category:PrivacyCategory; readonly reason:string;
}
export interface PrivacyAssessment {
  readonly identity:'mfa-verified'; readonly purpose:'pending'; readonly legalBasis:'pending';
  readonly retention:'pending'; readonly legalHold:'pending'; readonly disposition:'pending';
}
export interface PrivacyReceipt {
  readonly requestId:string; readonly right:PrivacyRight; readonly category:PrivacyCategory;
  readonly submittedAtUtc:string; readonly status:'received'; readonly assessment:PrivacyAssessment;
}
export interface PrivacyPage {readonly items:readonly PrivacyReceipt[];readonly nextCursor:string|null}
export const privacyRightOptions:readonly {code:PrivacyRight;label:string;description:string}[]=[
  {code:'access',label:'Acceso a mis datos',description:'Solicita conocer los datos propios que se tratan y su finalidad.'},
  {code:'rectify',label:'Rectificación',description:'Solicita revisar datos personales incorrectos o desactualizados.'},
  {code:'erase',label:'Eliminación',description:'Solicita evaluar si procede retirar datos propios, sujeto a conservación y otras obligaciones.'},
  {code:'object',label:'Oposición',description:'Solicita evaluar tu oposición a un tratamiento de datos propio.'}
];
export const privacyCategoryOptions:readonly {code:PrivacyCategory;label:string}[]=[
  {code:'profile',label:'Perfil y preferencias'},{code:'identity',label:'Identidad y cuenta'},
  {code:'security',label:'Actividad de seguridad'},{code:'case-records',label:'Expedientes propios'}
];
export function privacyRightLabel(code:PrivacyRight):string{return privacyRightOptions.find(x=>x.code===code)?.label??'Solicitud de privacidad';}
export function privacyCategoryLabel(code:PrivacyCategory):string{return privacyCategoryOptions.find(x=>x.code===code)?.label??'Categoría no disponible';}
export function isPrivacyRight(v:unknown):v is PrivacyRight{return typeof v==='string'&&(privacyRights as readonly string[]).includes(v);}
export function isPrivacyCategory(v:unknown):v is PrivacyCategory{return typeof v==='string'&&(privacyCategories as readonly string[]).includes(v);}
function exact(value:unknown,keys:readonly string[]):Record<string,unknown>{
  if(!value||typeof value!=='object'||Array.isArray(value))throw new Error('privacy_invalid');
  const r=value as Record<string,unknown>;
  if(Object.keys(r).length!==keys.length||!keys.every(k=>Object.hasOwn(r,k)))throw new Error('privacy_invalid');
  return r;
}
export function decodePrivacyReceipt(value:unknown):PrivacyReceipt{
  const r=exact(value,['requestId','right','category','submittedAtUtc','status','assessment']);
  const id=r['requestId'],date=r['submittedAtUtc'];
  if(typeof id!=='string'||!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id)||
     id==='00000000-0000-0000-0000-000000000000'||!isPrivacyRight(r['right'])||!isPrivacyCategory(r['category'])||
     typeof date!=='string'||!/^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(?:\.\d{1,7})?(?:Z|\+00:00)$/.test(date)||
     !Number.isFinite(Date.parse(date))||Date.parse(date)>Date.now()+1000||r['status']!=='received')throw new Error('privacy_invalid');
  const assessment=exact(r['assessment'],['identity','purpose','legalBasis','retention','legalHold','disposition']);
  if(assessment['identity']!=='mfa-verified'||['purpose','legalBasis','retention','legalHold','disposition'].some(k=>assessment[k]!=='pending'))
    throw new Error('privacy_invalid');
  return Object.freeze({...r,assessment:Object.freeze({...assessment})}) as unknown as PrivacyReceipt;
}
export function decodePrivacyPage(value:unknown):PrivacyPage{
  const r=exact(value,['items','nextCursor']);
  if(!Array.isArray(r['items'])||r['items'].length>20||
     (r['nextCursor']!==null&&(typeof r['nextCursor']!=='string'||!/^[A-Za-z0-9/]{120,160}$/.test(r['nextCursor']))))throw new Error('privacy_invalid');
  const items=r['items'].map(decodePrivacyReceipt);
  if(new Set(items.map(x=>x.requestId.toLowerCase())).size!==items.length)throw new Error('privacy_invalid');
  return Object.freeze({items:Object.freeze(items),nextCursor:r['nextCursor'] as string|null});
}
