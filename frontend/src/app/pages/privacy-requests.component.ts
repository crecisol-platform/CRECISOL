import { afterNextRender, ChangeDetectionStrategy, Component, effect, inject, Injector, OnDestroy, signal, untracked, viewChild } from '@angular/core';
import { IdentityApiService } from '../identity/identity-api.service';
import { identityDateLabel, identityReferenceLabel } from '../identity/identity-presentation';
import { privacyRightOptions,privacyCategoryOptions,privacyRightLabel,privacyCategoryLabel,isPrivacyRight,isPrivacyCategory,
  type PrivacyIntent,type PrivacyReceipt,type PrivacyRight } from '../identity/privacy-request-client.model';
import type { DelegationChallenge } from '../identity/delegation-client.model';
import { InstitutionalModalComponent } from '../ui/institutional-modal/institutional-modal.component';
import { InstitutionalStateComponent } from '../ui/institutional-system';

@Component({
  selector:'cs-privacy-requests',standalone:true,changeDetection:ChangeDetectionStrategy.OnPush,
  imports:[InstitutionalModalComponent,InstitutionalStateComponent],
  templateUrl:'./privacy-requests.component.html',styleUrl:'./privacy-requests.component.css'
})
export class PrivacyRequestsComponent implements OnDestroy {
  private readonly identity=inject(IdentityApiService);
  private readonly injector=inject(Injector);
  private readonly review=viewChild<InstitutionalModalComponent>('review');
  readonly available=signal(false);
  readonly opened=signal(false);
  readonly status=signal<'idle'|'loading'|'ready'|'error'>('idle');
  readonly items=signal<readonly PrivacyReceipt[]>([]);
  readonly cursor=signal<string|null>(null);
  readonly chosen=signal<PrivacyRight|null>(null);
  readonly challenge=signal<DelegationChallenge|null>(null);
  readonly busy=signal(false);
  readonly step=signal(1);
  readonly error=signal('');
  readonly receipt=signal<PrivacyReceipt|null>(null);
  readonly rights=privacyRightOptions;readonly categories=privacyCategoryOptions;
  readonly rightLabel=privacyRightLabel;readonly categoryLabel=privacyCategoryLabel;readonly reference=identityReferenceLabel;
  readonly steps=['Detalle de solicitud','Verificar identidad'];
  private intent:PrivacyIntent|null=null;
  private generation=0;
  private expiryTimer:ReturnType<typeof setTimeout>|undefined;
  constructor(){
    effect(()=>{
      const state=this.identity.state();this.identity.delegationContext();
      untracked(()=>{
        this.reset();
        if(state==='authenticated')void this.probe();
      });
    });
  }
  ngOnDestroy():void{this.reset();}
  private current(g:number):boolean{return g===this.generation&&this.identity.state()==='authenticated';}
  private reset():void{
    ++this.generation;this.available.set(false);this.opened.set(false);this.status.set('idle');
    this.items.set([]);this.cursor.set(null);this.receipt.set(null);this.busy.set(false);this.dismiss();
  }
  private async probe():Promise<void>{
    const g=this.generation;
    try{await this.identity.privacyRequests('start');if(this.current(g))this.available.set(true);}
    catch{/* Feature absent or no verified identity: no private surface. */}
  }
  async toggle():Promise<void>{
    if(this.busy()||!this.available())return;
    if(this.opened()){
      ++this.generation;this.opened.set(false);this.items.set([]);this.cursor.set(null);this.receipt.set(null);
      this.status.set('idle');this.dismiss();return;
    }
    this.opened.set(true);await this.load();
  }
  async load(cursor='start'):Promise<void>{
    if(!this.opened()||this.busy())return;
    const g=++this.generation;
    this.items.set([]);this.cursor.set(null);this.status.set('loading');
    try{
      const page=await this.identity.privacyRequests(cursor);
      if(!this.current(g))return;
      this.items.set(page.items);this.cursor.set(page.nextCursor);this.status.set('ready');
    }catch{if(this.current(g)){this.receipt.set(null);this.status.set('error');}}
  }
  date(v:string):string{return identityDateLabel(v,this.identity.profile()?.timeZone);}
  choose(right:PrivacyRight):void{
    if(this.busy()||this.status()!=='ready'||!isPrivacyRight(right))return;
    this.dismiss();this.receipt.set(null);this.chosen.set(right);this.review()?.open();
  }
  dismiss():void{
    if(this.expiryTimer)clearTimeout(this.expiryTimer);
    this.expiryTimer=undefined;this.intent=null;this.chosen.set(null);this.challenge.set(null);this.step.set(1);this.error.set('');
  }
  async prepare(event:Event):Promise<void>{
    event.preventDefault();
    const form=event.target,right=this.chosen();
    if(!(form instanceof HTMLFormElement)||!right||this.busy()||this.status()!=='ready'||!form.reportValidity())return;
    const fields=new FormData(form),category=fields.get('category'),reason=String(fields.get('reason')??'').trim();
    if(!isPrivacyCategory(category)||reason.length<8||reason.length>240||
       /[\u0000-\u001f\u007f]/.test(reason)||!/[\p{L}\p{N}]/u.test(reason)){
      this.error.set('Selecciona una categoría y escribe un detalle breve, sin códigos ni documentos sensibles.');return;
    }
    const intent:PrivacyIntent=Object.freeze({requestKey:crypto.randomUUID(),right,category,reason});
    const g=this.generation;
    this.busy.set(true);this.error.set('');
    try{
      const proof=await this.identity.privacyPrepare(intent);
      if(!this.current(g))return;
      this.intent=intent;this.challenge.set(proof);this.step.set(2);
      this.expiryTimer=setTimeout(()=>{
        if(!this.current(g))return;
        this.intent=null;this.challenge.set(null);this.error.set('La verificación caducó. Vuelve a preparar la solicitud.');
      },Math.max(0,Date.parse(proof.expiresAtUtc)-Date.now()));
    }catch{
      if(this.current(g)){this.intent=null;this.challenge.set(null);this.items.set([]);this.cursor.set(null);
        this.status.set('error');this.error.set('No se pudo preparar la solicitud. Cierra y actualiza tus solicitudes.');}
    }finally{if(this.current(g))this.busy.set(false);}
  }
  async confirm(event:Event):Promise<void>{
    event.preventDefault();
    const form=event.target,intent=this.intent,proof=this.challenge();
    if(!(form instanceof HTMLFormElement)||!intent||!proof||this.busy()||!form.reportValidity())return;
    const code=String(new FormData(form).get('code')??'');form.reset();
    if(!/^\d{6}$/.test(code)||Date.parse(proof.expiresAtUtc)<=Date.now()){
      this.error.set('La verificación no es válida o ha caducado.');return;
    }
    const g=this.generation;
    this.busy.set(true);this.error.set('');this.intent=null;this.challenge.set(null);
    if(this.expiryTimer)clearTimeout(this.expiryTimer);this.expiryTimer=undefined;
    try{
      const result=await this.identity.privacyConfirm(intent,proof.challenge,code);
      if(!this.current(g))return;
      if(result.right!==intent.right||result.category!==intent.category)throw new Error('privacy_invalid');
      this.receipt.set(result);this.busy.set(false);
      afterNextRender(()=>this.review()?.close(),{injector:this.injector});
      await this.load();
    }catch{
      if(this.current(g)){this.items.set([]);this.cursor.set(null);this.receipt.set(null);this.status.set('error');
        this.error.set('No se pudo confirmar el resultado. Consulta tus solicitudes antes de presentar otra.');}
    }finally{if(this.current(g))this.busy.set(false);}
  }
}
