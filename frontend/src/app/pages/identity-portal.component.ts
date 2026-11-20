import { ChangeDetectionStrategy, Component, ElementRef, computed, inject, signal, viewChild } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { IdentityApiService } from '../identity/identity-api.service';
import { PublicBrandService } from '../branding/public-brand.service';
import { PlatformIdentityPanelComponent } from './platform-identity-panel.component';
import { InstitutionalThemeDirective } from '../ui/institutional-system';
import {
  guestLabels, guestViewFor, isSecondaryGuestView,
  workspaceLabels, workspaceViewFor
} from './c05-navigation.model';
import { IdentityGuestComponent, IdentityMfaComponent, IdentityRecoveryComponent, IdentityWorkspaceComponent } from './identity-flow-views.component';

@Component({
  selector: 'cs-identity-portal', standalone: true,
  imports: [RouterLink, InstitutionalThemeDirective, PlatformIdentityPanelComponent, IdentityGuestComponent, IdentityMfaComponent, IdentityRecoveryComponent, IdentityWorkspaceComponent],
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
    return ['ingresar', 'registro', 'confirmar', 'recuperar', 'invitacion', 'acceso-existente', 'operadores', 'resumen', 'perfil', 'llaves', 'seguridad', 'historial', 'administracion'].includes(selected) ? selected : 'ingresar';
  });
  readonly incomingPairToken = /^#pair=([A-F0-9]{64})\.([A-F0-9]{12})$/.exec(location.hash)?.[1] ?? '';
  readonly incomingPairFingerprint = /^#pair=([A-F0-9]{64})\.([A-F0-9]{12})$/.exec(location.hash)?.[2] ?? '';
  readonly identity = inject(IdentityApiService);
  private readonly contentTarget = viewChild<ElementRef<HTMLElement>>('accountContent');

  // Native '#fragment' links are resolved against <base href="/"> in Angular,
  // which can navigate away from /cuenta. Focus the content in place instead:
  // preserve queryParams, browser history and any temporary pairing fragment.
  focusContent(event: Event): void {
    event.preventDefault();
    this.contentTarget()?.nativeElement.focus();
  }
  // The URL is kept intact: display normalization never grants access
  // or calls an API, and deep links remain shareable/back-button friendly.
  readonly guestView = computed(() => guestViewFor(this.view()));
  readonly workspaceView = computed(() => workspaceViewFor(this.view()));
  readonly supplementaryGuestView = computed(() => isSecondaryGuestView(this.guestView()));
  readonly sectionName = computed(() => {
    if (this.view() === 'operadores') return 'Personal autorizado';
    if (this.identity.state() === 'challenge') return 'Verificación en dos pasos';
    if (this.identity.state() === 'authenticated') return workspaceLabels[this.workspaceView()];
    return guestLabels[this.guestView()];
  });
  readonly publishedTheme = computed(() => {
    const state = this.brandState();
    return state.kind === 'ready' ? state.brand : null;
  });
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
      const acknowledgements: Readonly<Record<string, string>> = {
        'membership-request': 'La solicitud de relación empresarial se registró para su revisión. No concede acceso automáticamente.',
        profile: 'Tus datos de perfil se actualizaron en el contexto autenticado.',
        'assisted-request': 'Si la cuenta cumple los requisitos, el procedimiento de recuperación continuará por el canal verificado.',
        'assisted-complete': 'El procedimiento de recuperación terminó. Comprueba el nuevo acceso antes de continuar.',
        'assisted-handoff': 'La entrega asistida terminó. Comprueba el nuevo acceso antes de continuar.',
        'passkey-recovery': 'Se completó la recuperación con llave. Configura de nuevo los factores de verificación.',
        incident: 'Se recibió el reporte de seguridad de tu cuenta.',
        'sessions-all': 'Se solicitó el cierre de todas tus sesiones verificadas.',
        'pairing-approve': 'Se confirmó la autorización del navegador que verificaste.',
        terms: 'La aceptación de las condiciones vigentes quedó registrada.',
        'invitation-issue': 'La invitación se emitió para la dirección autorizada.',
        'invitation-accept': 'Se recibió la aceptación; la membresía continúa su proceso de autorización.',
        'passkey-add': 'Tu nueva llave de acceso quedó registrada.',
        'passkey-revoke': 'La llave de acceso seleccionada quedó revocada.',
        private: 'La consulta privada se completó únicamente con los permisos comprobados por el servidor.',
        password: 'La contraseña se actualizó. Es posible que debas iniciar sesión nuevamente.',
        factor: 'Se inició el reemplazo protegido de tu autenticador. Completa su nueva configuración.',
        'mailbox-request': 'Se envió la solicitud para verificar el nuevo correo electrónico.',
        'mailbox-complete': 'El cambio de correo fue confirmado.',
        membership: 'La decisión administrativa se registró después de la verificación del servidor.',
        'recovery-request': 'Si la cuenta cumple los requisitos, recibirás instrucciones en el correo verificado.',
        'recovery-complete': 'La recuperación terminó. Puedes comprobar tu nuevo acceso.'
      };
      this.message.set(acknowledgements[action] ?? 'La operación terminó. Verifica el estado actualizado de tu cuenta.');
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


