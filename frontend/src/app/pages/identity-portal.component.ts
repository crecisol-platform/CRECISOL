import { ChangeDetectionStrategy, Component, ElementRef, afterRenderEffect, computed, effect, inject, signal, untracked, viewChild } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { IdentityApiService } from '../identity/identity-api.service';
import { OperationFeedbackComponent } from '../ui/feedback/operation-feedback.component';
import { SecureFieldFeedbackDirective, clearSensitiveInputValues } from '../ui/feedback/secure-field-feedback.directive';
import { activityLabel, contextualFeedback } from '../ui/feedback/operation-feedback.model';
import { PublicBrandService } from '../branding/public-brand.service';
import { PlatformIdentityPanelComponent } from './platform-identity-panel.component';
import { InstitutionalThemeDirective } from '../ui/institutional-system';
import {
  guestLabels, guestViewFor,
  workspaceLabels, workspaceViewFor
} from './c05-navigation.model';
import { IdentityGuestComponent, IdentityMfaComponent, IdentityRecoveryComponent, IdentityWorkspaceComponent } from './identity-flow-views.component';

@Component({
  selector: 'cs-identity-portal', standalone: true,
  imports: [RouterLink, InstitutionalThemeDirective, PlatformIdentityPanelComponent, IdentityGuestComponent, IdentityMfaComponent, IdentityRecoveryComponent, IdentityWorkspaceComponent, OperationFeedbackComponent, SecureFieldFeedbackDirective],
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
  private readonly workspaceTabRail = viewChild<ElementRef<HTMLElement>>('workspaceTabRail');

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
  readonly workspaceTitle = computed(() => {
    const titles: Record<string, string> = {
      resumen: 'Sesiones y dispositivos',
      perfil: 'Mi perfil',
      llaves: 'Llaves de acceso',
      seguridad: 'Seguridad de cuenta',
      historial: 'Mi historial',
      administracion: 'Aprobaciones e invitaciones'
    };
    return titles[this.workspaceView()] ?? 'Sesiones y dispositivos';
  });
  readonly workspaceDescription = computed(() => {
    const descriptions: Record<string, string> = {
      resumen: 'Comprueba qué dispositivos tienen acceso y administra tus sesiones con confianza.',
      perfil: 'Tu información visible y tus preferencias en esta empresa.',
      llaves: 'Protege el inicio de sesión con métodos registrados en tus dispositivos.',
      seguridad: 'Credenciales, segundo factor y operaciones de recuperación verificadas.',
      historial: 'Una cronología comprensible de accesos y cambios de seguridad.',
      administracion: 'Gestiones administrativas sujetas a autorización y verificación adicional.'
    };
    return descriptions[this.workspaceView()] ?? descriptions.resumen;
  });
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
  readonly activity = signal('');
  readonly feedbackScope = signal('');
  readonly visibleFeedback = computed(() => contextualFeedback(
    this.feedbackScope(), this.view(), this.busy(), this.message(), this.error(), this.activity()));
  private feedbackGeneration = 0;
  private lastFeedbackView = this.view();

  constructor() {
    // Never replay a result in another account section or expose old backup codes.
    effect(() => {
      const next = this.view();
      untracked(() => {
        if (next === this.lastFeedbackView) return;
        this.lastFeedbackView = next;
        this.feedbackGeneration++;
        this.feedbackScope.set('');
        this.message.set('');
        this.error.set('');
        // Moving between account tabs must not destroy freshly issued backups:
        // they are displayed in Resumen until acknowledgement or a 5-minute limit.
        if (next === 'operadores') this.identity.hideRecoveryCodes();
      });
    });
    // Keep the current account tab visible when opening a deep link on narrow
    // screens; scrolling only the tab rail must never move the whole document.
    // DOM layout reads and the subsequent write run after Angular renders.
    afterRenderEffect({
      earlyRead: () => {
        const selected = this.workspaceView();
        if (this.identity.state() !== 'authenticated' || !selected) return null;
        const rail = this.workspaceTabRail()?.nativeElement;
        if (!rail || rail.scrollWidth <= rail.clientWidth + 1) return null;
        const active = rail.querySelector<HTMLElement>('a[aria-current="page"]');
        if (!active) return null;
        const container = rail.getBoundingClientRect();
        const item = active.getBoundingClientRect();
        return { rail, left: rail.scrollLeft + item.left - container.left -
          (rail.clientWidth - item.width) / 2 };
      },
      write: position => {
        const target = position();
        if (target && Math.abs(target.rail.scrollLeft - target.left) > 2)
          target.rail.scrollLeft = Math.max(0, target.left);
      }
    });
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
    }, 'login', event.currentTarget as HTMLFormElement);
  }

  async register(event: Event): Promise<void> {
    event.preventDefault();
    await this.run(async () => {
      const form = event.currentTarget as HTMLFormElement;
      const mailbox = String(new FormData(form).get('mailbox') ?? '');
      await this.identity.requestRegistration(mailbox);
      form.reset();
      this.message.set('Solicitud recibida. Esto todavía no crea una cuenta ni concede acceso.');
    }, 'register', event.currentTarget as HTMLFormElement);
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
    }, 'confirmation', event.currentTarget as HTMLFormElement);
  }

  async verify(event: Event): Promise<void> {
    event.preventDefault();
    await this.run(async () => {
      const form = event.currentTarget as HTMLFormElement;
      const code = String(new FormData(form).get('mfaCode') ?? '');
      await this.identity.verifyMfa(code);
      form.reset();
      this.message.set('Verificación completada.');
    }, 'verify', event.currentTarget as HTMLFormElement);
  }

  async logout(): Promise<void> {
    await this.run(async () => {
      await this.identity.logout();
      this.message.set('La solicitud de cierre de sesión terminó.');
    }, 'logout');
  }

  async enroll(): Promise<void> { await this.run(() => this.identity.enrollMfa(), 'enroll'); }
  async passkeyLogin(): Promise<void> { await this.run(() => this.identity.loginPasskey(), 'passkey-login'); }
  async passkeyList(): Promise<void> { await this.run(() => this.identity.refreshPasskeys(), 'passkey-list'); }
  async pendingMemberships(next: boolean): Promise<void> {
    await this.run(() => this.identity.loadPendingMemberships(next), 'pending-memberships');
  }
  async loadTerms(): Promise<void> { await this.run(() => this.identity.loadTerms(), 'terms-read'); }
  async startPairing(): Promise<void> { await this.run(() => this.identity.startPairing(), 'pair-start'); }
  async completePairing(): Promise<void> { await this.run(() => this.identity.completePairing(), 'pair-complete'); }
  async securityEvents(): Promise<void> { await this.run(() => this.identity.loadSecurityEvents(), 'security-events'); }
  async loadProfile(): Promise<void> { await this.run(() => this.identity.loadProfile(), 'profile-read'); }
  async removePhoto(): Promise<void> { await this.run(() => this.identity.removePhoto(), 'profile-photo-remove'); }
  async uploadPhoto(event: Event): Promise<void> {
    event.preventDefault(); const form = event.currentTarget as HTMLFormElement;
    const file = new FormData(form).get('photo');
    await this.run(async () => {
      if (!(file instanceof File)) throw new Error('identity_unavailable');
      await this.identity.uploadPhoto(file); form.reset();
    }, 'profile-photo', form);
  }
  async sessionAction(action: 'list' | 'rotate'): Promise<void> {
    await this.run(() => action === 'list' ? this.identity.refreshSessions() : this.identity.rotateSession(),
      action === 'list' ? 'sessions-list' : 'sessions-rotate');
  }
  async revoke(id: string): Promise<void> { await this.run(() => this.identity.revokeSession(id), 'sessions-revoke'); }

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
        case 'membership':
          if (field('confirmed') !== 'on' || !this.identity.pendingMemberships().some(item => item.id === field('membershipId')))
            throw new Error('identity_unavailable');
          await this.identity.decideMembership(field('membershipId'), field('decision'), field('reason'), field('code')); break;
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
    }, action, form);
  }

  private async run(
    operation: () => Promise<void>, command = 'operation', form?: HTMLFormElement
  ): Promise<void> {
    if (this.busy()) return;
    const generation = ++this.feedbackGeneration;
    const origin = this.view();
    // A successful read is not a completed security mutation. Only actual
    // protected commands may produce the generic completion receipt.
    const passiveRead = ['terms-read', 'passkey-list', 'pending-memberships',
      'security-events', 'profile-read', 'sessions-list'].includes(command);
    this.feedbackScope.set(origin);
    this.activity.set(activityLabel(command));
    this.busy.set(true);
    this.error.set('');
    this.message.set('');
    try {
      await operation();
      if (generation === this.feedbackGeneration && this.view() === origin &&
          !this.message() && !this.error() && !passiveRead) {
        this.message.set(command === 'enroll'
          ? 'La configuración está preparada. Verifica tu nuevo autenticador antes de activarlo.'
          : 'La solicitud finalizó. Comprueba el estado autorizado antes de continuar.');
      }
    } catch (caught) {
      if (generation === this.feedbackGeneration && this.view() === origin)
        this.error.set(IdentityApiService.publicError(caught));
    } finally {
      // Values read into FormData are never echoed; input secrets are cleared on failure too.
      clearSensitiveInputValues(form);
      if (generation !== this.feedbackGeneration || this.view() !== origin) {
        this.message.set('');
        this.error.set('');
        this.feedbackScope.set('');
      }
      this.busy.set(false);
    }
  }
}
