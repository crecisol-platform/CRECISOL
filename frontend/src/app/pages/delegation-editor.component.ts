import { ChangeDetectionStrategy, Component, effect, inject, signal, untracked, viewChild, OnDestroy } from '@angular/core';
import { IdentityApiService } from '../identity/identity-api.service';
import { identityDateLabel, identityReferenceLabel } from '../identity/identity-presentation';
import { delegationExpiry, delegationKey, delegationRoleLabel, exactSelection, type DelegationCandidate,
  type DelegationIntent, type DelegationChallenge } from '../identity/delegation-client.model';
import { InstitutionalModalComponent } from '../ui/institutional-modal/institutional-modal.component';
import { InstitutionalStateComponent } from '../ui/institutional-system';

type EditorStatus='idle'|'loading'|'ready'|'error';
@Component({
  selector:'cs-delegation-editor',standalone:true,changeDetection:ChangeDetectionStrategy.OnPush,
  imports:[InstitutionalModalComponent,InstitutionalStateComponent],
  templateUrl:'./delegation-editor.component.html',styleUrl:'./delegation-editor.component.css'
})
export class DelegationEditorComponent implements OnDestroy {
  private readonly identity=inject(IdentityApiService);
  private readonly review=viewChild<InstitutionalModalComponent>('review');
  readonly available=signal(false);
  readonly opened=signal(false);
  readonly status=signal<EditorStatus>('idle');
  readonly items=signal<readonly DelegationCandidate[]>([]);
  readonly cursor=signal<string|null>(null);
  readonly selection=signal<DelegationCandidate|null>(null);
  readonly challenge=signal<DelegationChallenge|null>(null);
  readonly busy=signal(false);
  readonly message=signal('');
  readonly modalError=signal('');
  readonly step=signal(1);
  readonly roleLabel=delegationRoleLabel;
  readonly reference=identityReferenceLabel;
  readonly key=delegationKey;
  readonly steps=['Revisar alcance','Confirmar con MFA'];
  private intent: DelegationIntent|null=null;
  private generation=0;
  private expiryTimer: ReturnType<typeof setTimeout>|undefined;
  constructor(){
    effect(()=>{
      const state=this.identity.state();
      this.identity.delegationContext();
      untracked(()=>{
        this.reset();
        if(state==='authenticated') void this.probe();
      });
    });
  }
  ngOnDestroy():void {this.reset();}
  private reset():void {
    ++this.generation;
    this.available.set(false);this.opened.set(false);this.items.set([]);this.cursor.set(null);
    this.status.set('idle');this.busy.set(false);this.message.set('');this.dismiss();
  }
  private current(g:number):boolean {return g===this.generation&&this.identity.state()==='authenticated';}
  private async probe():Promise<void>{
    const g=this.generation;
    try{
      await this.identity.delegationCatalog('start');
      if(this.current(g))this.available.set(true);
    }catch{/* Disabled feature or denied authority: do not expose a capability. */}
  }
  async toggle():Promise<void>{
    if(this.busy()||!this.available())return;
    if(this.opened()){
      ++this.generation;this.opened.set(false);this.items.set([]);this.cursor.set(null);this.status.set('idle');
      this.message.set('');this.dismiss();return;
    }
    this.opened.set(true);await this.load('start');
  }
  async load(cursor:string='start'):Promise<void>{
    if(!this.opened()||this.busy())return;
    const g=++this.generation;
    // Never leave a previously authorized page visible while a new page is uncertain.
    this.items.set([]);this.cursor.set(null);this.status.set('loading');
    try{
      const page=await this.identity.delegationCatalog(cursor);
      if(!this.current(g))return;
      this.items.set(page.items);this.cursor.set(page.nextCursor);this.status.set('ready');
    }catch{
      if(this.current(g))this.status.set('error');
    }
  }
  date(value:string|null|undefined):string{
    return value ? identityDateLabel(value,this.identity.profile()?.timeZone) : 'No aplica';
  }
  async choose(row:DelegationCandidate):Promise<void>{
    if(this.busy()||this.status()!=='ready'||!this.items().includes(row))return;
    const g=++this.generation;
    this.dismiss();this.message.set('');this.busy.set(true);
    try{
      const current=await this.identity.delegationInspect(row);
      if(!this.current(g))return;
      this.selection.set({...exactSelection(row),inspection:current});
      this.busy.set(false);
      this.review()?.open();
    }catch{
      if(this.current(g)){
        this.items.set([]);this.cursor.set(null);this.status.set('error');
      }
    }finally{if(this.current(g))this.busy.set(false);}
  }
  dismiss():void{
    if(this.expiryTimer)clearTimeout(this.expiryTimer);
    this.expiryTimer=undefined;
    this.selection.set(null);this.challenge.set(null);this.intent=null;this.step.set(1);this.modalError.set('');
  }
  async prepare(event:Event):Promise<void>{
    event.preventDefault();
    const form=event.target;
    const selected=this.selection();
    if(!(form instanceof HTMLFormElement)||!selected||this.busy()||!form.reportValidity())return;
    const fields=new FormData(form);
    const reason=String(fields.get('reason')??'').trim();
    if(reason.length<8||reason.length>240||/[\u0000-\u001f\u007f]/.test(reason)||!/[\p{L}\p{N}]/u.test(reason)){
      this.modalError.set('Escribe un motivo de 8 a 240 caracteres, sin datos privados ni códigos.');return;
    }
    const s=selected.inspection;
    let expiresAtUtc='none';
    if(s.canAssign){
      const duration=Number(fields.get('duration'));
      if(![15,30,60].includes(duration)||!s.maxExpiresAtUtc)return;
      const expiry=Math.min(Date.now()+duration*60_000,Date.parse(s.maxExpiresAtUtc));
      if(expiry<=Date.now()+15_000){
        this.modalError.set('El límite autorizado está por vencer. Cierra y vuelve a consultar.');return;
      }
      expiresAtUtc=delegationExpiry(expiry);
    }
    const intent:DelegationIntent=Object.freeze({...exactSelection(selected),command:s.canAssign?'assign':'revoke',
      roleRevision:s.roleRevision,membershipRevision:s.membershipRevision,ceilingRevision:s.ceilingRevision,
      grantRevision:s.grantRevision,expiresAtUtc,reason});
    const g=this.generation;
    this.busy.set(true);this.modalError.set('');
    // The form is destroyed on transition; no reason remains in a DOM field.
    try{
      const challenge=await this.identity.delegationPrepare(intent);
      if(!this.current(g))return;
      this.intent=intent;this.challenge.set(challenge);this.step.set(2);
      this.expiryTimer=setTimeout(()=>{
        if(!this.current(g))return;
        this.intent=null;this.challenge.set(null);
        this.modalError.set('La confirmación caducó. Cierra y consulta de nuevo.');
      },Math.max(0,Date.parse(challenge.expiresAtUtc)-Date.now()));
    }catch{
      if(this.current(g)){
        this.intent=null;this.challenge.set(null);
        this.modalError.set('La operación ya no está disponible. Cierra y vuelve a consultar antes de intentarlo.');
        this.items.set([]);this.cursor.set(null);this.status.set('error');
      }
    }finally{if(this.current(g))this.busy.set(false);}
  }
  async confirm(event:Event):Promise<void>{
    event.preventDefault();
    const form=event.target, intent=this.intent, proof=this.challenge();
    if(!(form instanceof HTMLFormElement)||!intent||!proof||this.busy()||!form.reportValidity())return;
    const code=String(new FormData(form).get('code')??'');
    form.reset();
    if(!/^\d{6}$/.test(code)||Date.parse(proof.expiresAtUtc)<=Date.now()){
      this.modalError.set('La confirmación no es válida o ha caducado. Consulta de nuevo.');return;
    }
    const g=this.generation;
    this.busy.set(true);this.modalError.set('');
    // A response may be lost after a commit. Never retry the mutation
    // automatically and never retain or reuse a challenge after an attempt.
    this.intent=null;this.challenge.set(null);
    if(this.expiryTimer)clearTimeout(this.expiryTimer);
    this.expiryTimer=undefined;
    try{
      await this.identity.delegationConfirm(intent,proof.challenge,code);
      if(!this.current(g))return;
      this.message.set(intent.command==='assign'?'Permiso registrado por el servidor.':'Revocación registrada por el servidor.');
      this.busy.set(false);this.review()?.close();this.dismiss();await this.load('start');
    }catch{
      if(this.current(g)){
        this.items.set([]);this.cursor.set(null);this.status.set('error');
        this.modalError.set('No se pudo confirmar el resultado. Cierra y consulta el catálogo antes de repetir una operación.');
      }
    }finally{if(this.current(g))this.busy.set(false);}
  }
}
