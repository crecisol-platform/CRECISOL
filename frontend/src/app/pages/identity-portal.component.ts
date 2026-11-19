import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { IdentityApiService } from '../identity/identity-api.service';
import { PublicBrandService } from '../branding/public-brand.service';
import { PlatformIdentityPanelComponent } from './platform-identity-panel.component';
import { IdentityGuestComponent, IdentityMfaComponent, IdentityRecoveryComponent, IdentityWorkspaceComponent } from './identity-flow-views.component';

@Component({
  selector: 'cs-identity-portal', standalone: true,
  imports: [RouterLink, PlatformIdentityPanelComponent, IdentityGuestComponent, IdentityMfaComponent, IdentityRecoveryComponent, IdentityWorkspaceComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './identity-portal.component.html',
  styleUrl: './identity-portal.component.css'
})
export class IdentityPortalComponent {
  private readonly publishedBrand = inject(PublicBrandService);
  readonly brandState = this.publishedBrand.state;
  readonly brandImageFailed = signal(false);
  private readonly route = inject(ActivatedRoute);
  private readonly params = toSignal(this.route.queryParamMap, { initialValue: this.route.snapshot.queryParamMap });
  readonly view = computed(() => {
    const selected = this.params().get('vista') ?? 'ingresar';
    return ['ingresar', 'registro', 'confirmar', 'recuperar', 'invitacion', 'acceso-existente', 'operadores', 'resumen', 'perfil', 'llaves', 'seguridad', 'administracion'].includes(selected) ? selected : 'ingresar';
  });
  readonly incomingPairToken = /^#pair=([A-F0-9]{64})\.([A-F0-9]{12})$/.exec(location.hash)?.[1] ?? '';
  readonly incomingPairFingerprint = /^#pair=([A-F0-9]{64})\.([A-F0-9]{12})$/.exec(location.hash)?.[2] ?? '';
  readonly identity = inject(IdentityApiService);
  readonly busy = signal(false);
  readonly message = signal('');
  readonly error = signal('');

  constructor() {
    // Read only validated published PLATFORM branding. Identity never consumes
    // a tenant picker, preview, candidate asset, or browser-stored brand.
    if (this.brandState().kind === 'loading') void this.publishedBrand.load();
  }

  async login(event: Event): Promise<void> {
    event.preventDefault();
    await this.run(async () => {
      const form = event.currentTarget as HTMLFormElement;
      const values = new FormData(form);
      const mailbox = String(values.get('mailbox') ?? '');
      const password = String(values.get('password') ?? '');
      await this.identity.beginLogin(mailbox, password);
      form.reset();
      this.message.set('Ingresa el código de verificación.');
    });
  }

  async register(event: Event): Promise<void> {
    event.preventDefault();
    await this.run(async () => {
      const form = event.currentTarget as HTMLFormElement;
      const mailbox = String(new FormData(form).get('mailbox') ?? '');
      await this.identity.requestRegistration(mailbox);
      form.reset();
      this.message.set('Solicitud recibida. Esto todavía no crea una cuenta ni concede acceso.');
    });
  }

  async confirmRegistration(event: Event): Promise<void> {
    event.preventDefault();
    await this.run(async () => {
      const form = event.currentTarget as HTMLFormElement;
      const values = new FormData(form);
      await this.identity.completeRegistration(
        String(values.get('proofId') ?? ''), String(values.get('proofCode') ?? ''),
        String(values.get('completionMailbox') ?? ''), String(values.get('completionPassword') ?? ''));
      form.reset();
      this.message.set('Solicitud recibida. La cuenta queda pendiente de aprobación y todavía no concede acceso.');
    });
  }

  async verify(event: Event): Promise<void> {
    event.preventDefault();
    await this.run(async () => {
      const form = event.currentTarget as HTMLFormElement;
      const code = String(new FormData(form).get('mfaCode') ?? '');
      await this.identity.verifyMfa(code);
      form.reset();
      this.message.set('Verificación completada.');
    });
  }

  async logout(): Promise<void> {
    await this.run(async () => {
      await this.identity.logout();
      this.message.set('La solicitud de cierre de sesión terminó.');
    });
  }

  async enroll(): Promise<void> { await this.run(() => this.identity.enrollMfa()); }
  async passkeyLogin(): Promise<void> { await this.run(() => this.identity.loginPasskey()); }
  async passkeyList(): Promise<void> { await this.run(() => this.identity.refreshPasskeys()); }
  async loadTerms(): Promise<void> { await this.run(() => this.identity.loadTerms()); }
  async startPairing(): Promise<void> { await this.run(() => this.identity.startPairing()); }
  async completePairing(): Promise<void> { await this.run(() => this.identity.completePairing()); }
  async securityEvents(): Promise<void> { await this.run(() => this.identity.loadSecurityEvents()); }
  async loadProfile(): Promise<void> { await this.run(() => this.identity.loadProfile()); }
  async removePhoto(): Promise<void> { await this.run(() => this.identity.removePhoto()); }
  async uploadPhoto(event: Event): Promise<void> {
    event.preventDefault(); const form = event.currentTarget as HTMLFormElement;
    const file = new FormData(form).get('photo');
    await this.run(async () => {
      if (!(file instanceof File)) throw new Error('identity_unavailable');
      await this.identity.uploadPhoto(file); form.reset();
    });
  }
  async sessionAction(action: 'list' | 'rotate'): Promise<void> {
    await this.run(() => action === 'list' ? this.identity.refreshSessions() : this.identity.rotateSession());
  }
  async revoke(id: string): Promise<void> { await this.run(() => this.identity.revokeSession(id)); }

  async securityAction(event: Event, action: string): Promise<void> {
    event.preventDefault();
    const form = event.currentTarget as HTMLFormElement;
    const data = new FormData(form);
    const field = (name: string): string => String(data.get(name) ?? '');
    await this.run(async () => {
      switch (action) {
        case 'membership-request':
          if (field('accepted') !== 'on') throw new Error('identity_unavailable');
          await this.identity.requestMembership(field('mailbox'), field('password'), field('code'), field('proofId'), field('proofCode')); break;
        case 'profile': await this.identity.saveProfile(field('displayName'), field('language'), field('timeZone')); break;
        case 'assisted-request':
          if (field('unavailable') !== 'on') throw new Error('identity_unavailable');
          await this.identity.requestAssisted(field('mailbox')); break;
        case 'assisted-complete': await this.identity.completeAssisted(field('caseId'), field('token'), field('newPassword')); break;
        case 'assisted-handoff': await this.identity.completeAssistedHandoff(field('caseId'), field('shareA'), field('shareB'), field('newPassword')); break;
        case 'passkey-recovery': await this.identity.recoverWithPasskey(field('token'), field('newPassword')); break;
        case 'incident': await this.identity.reportIncident(field('token')); break;
        case 'sessions-all': await this.identity.revokeAllSessions(field('code')); break;
        case 'pairing-approve':
          if (field('confirmed') !== 'on') throw new Error('identity_unavailable');
          await this.identity.approvePairing(field('token'), field('fingerprint'), field('code')); break;
        case 'terms':
          if (field('accepted') !== 'on') throw new Error('identity_unavailable');
          await this.identity.acceptTerms(); break;
        case 'invitation-issue': await this.identity.issueInvitation(field('mailbox'), field('code')); break;
        case 'invitation-accept':
          if (field('accepted') !== 'on') throw new Error('identity_unavailable');
          await this.identity.acceptInvitation(field('mailbox'), field('password'), field('code'), field('token')); break;
        case 'passkey-add': await this.identity.registerPasskey(field('code')); break;
        case 'passkey-revoke': await this.identity.revokePasskey(field('factorId'), field('code')); break;
        case 'private': await this.identity.readPrivate(field('branchId'), field('resourceId')); break;
        case 'password': await this.identity.changePassword(field('currentPassword'), field('newPassword'), field('code')); break;
        case 'factor': await this.identity.replaceFactor(field('currentPassword'), field('code')); break;
        case 'mailbox-request': await this.identity.requestMailbox(field('mailbox'), field('code')); break;
        case 'mailbox-complete': await this.identity.completeMailbox(field('token'), field('code')); break;
        case 'membership': await this.identity.decideMembership(field('membershipId'), field('decision'), field('reason'), field('code')); break;
        case 'recovery-request': await this.identity.requestRecovery(field('mailbox')); break;
        case 'recovery-complete': await this.identity.completeRecovery(field('token'), field('code'), field('newPassword')); break;
        default: throw new Error('identity_unavailable');
      }
      form.reset();
      const confirmation: Record<string, string> = {
        'membership-request': 'Solicitud de membresía recibida. El acceso depende de una aprobación posterior.',
        profile: 'Los datos de tu perfil se actualizaron en esta empresa.',
        'assisted-request': 'Si se cumplen las condiciones, se comunicarán los siguientes pasos por el canal verificado.',
        'assisted-complete': 'Solicitud de recuperación completada. Inicia sesión con tus credenciales actualizadas.',
        'assisted-handoff': 'La recuperación asistida ha finalizado. Vuelve a iniciar sesión.',
        'passkey-recovery': 'Se completó la recuperación del segundo factor. Configura un autenticador nuevo antes de continuar.',
        incident: 'El aviso de seguridad se registró.',
        'sessions-all': 'Se cerraron las sesiones anteriores. Vuelve a iniciar sesión.',
        'pairing-approve': 'Se confirmó la solicitud de vinculación del navegador.',
        terms: 'Aceptaste la versión vigente de las condiciones de esta cuenta.',
        'invitation-issue': 'La invitación se registró para la empresa actual.',
        'invitation-accept': 'La invitación fue aceptada. El acceso queda sujeto a las autorizaciones de la empresa.',
        'passkey-add': 'La llave de acceso se registró. Puedes consultar el listado actualizado.',
        'passkey-revoke': 'La llave de acceso fue revocada. Por seguridad cerramos la sesión: vuelve a iniciar sesión.',
        private: 'La comprobación de acceso privado finalizó.',
        password: 'La contraseña se actualizó. Por seguridad se cerró tu sesión: inicia sesión de nuevo.',
        factor: 'El factor anterior se invalidó. Configura ahora un nuevo autenticador para terminar la verificación.',
        'mailbox-request': 'La solicitud de cambio de correo se registró. Revisa el nuevo buzón para continuar.',
        'mailbox-complete': 'El correo se actualizó. Por seguridad, inicia sesión de nuevo.',
        membership: 'La decisión de membresía quedó registrada.',
        'recovery-request': 'Si la cuenta cumple los requisitos, recibirás instrucciones en el correo verificado.',
        'recovery-complete': 'La recuperación de acceso se completó. Inicia sesión de nuevo.'
      };
      this.message.set(confirmation[action] ?? 'La operación finalizó correctamente.');
    });
  }

  private async run(operation: () => Promise<void>): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    this.message.set('');
    try {
      await operation();
    } catch (caught) {
      this.error.set(IdentityApiService.publicError(caught));
    } finally {
      this.busy.set(false);
    }
  }
}


