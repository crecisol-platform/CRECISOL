import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { PlatformIdentityApiService } from '../identity/platform-identity-api.service';
import { IdentityApiService } from '../identity/identity-api.service';
import { OperationFeedbackComponent } from '../ui/feedback/operation-feedback.component';
import { SecureFieldFeedbackDirective, clearSensitiveInputValues } from '../ui/feedback/secure-field-feedback.directive';
import { activityLabel, contextualFeedback } from '../ui/feedback/operation-feedback.model';
import { InstitutionalModalComponent } from '../ui/institutional-modal/institutional-modal.component';
import { InstitutionalNavigationComponent } from '../ui/institutional-system';
import { operatorSections, operatorSectionFor } from './c05-navigation.model';
import { PlatformOperatorSessionsComponent, PlatformOperatorProfileComponent, PlatformOperatorMonitorComponent, PlatformOperatorSecurityComponent, PlatformOperatorCasesComponent } from './platform-operator-views.component';

@Component({
  selector: 'cs-platform-identity-panel', standalone: true, changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [InstitutionalModalComponent, InstitutionalNavigationComponent, PlatformOperatorSessionsComponent, PlatformOperatorProfileComponent, PlatformOperatorMonitorComponent, PlatformOperatorSecurityComponent, PlatformOperatorCasesComponent, OperationFeedbackComponent, SecureFieldFeedbackDirective],
  templateUrl: './platform-identity-panel.component.html',
  styleUrl: './identity-flow.component.css'
})
export class PlatformIdentityPanelComponent {
  readonly platform = inject(PlatformIdentityApiService);
  readonly panel = signal<'casos' | 'sesiones' | 'perfil' | 'seguridad' | 'monitor'>('casos');
  // Separate anonymous recovery flow state from the operator authenticated realm.
  readonly recoveryStep = signal<1 | 2>(1);
  readonly busy = signal(false); readonly message = signal(''); readonly error = signal('');
  readonly activity = signal('');
  readonly feedbackScope = signal('');
  readonly visibleFeedback = computed(() => contextualFeedback(
    this.feedbackScope(), this.panel(), this.busy(), this.message(), this.error(), this.activity()));
  private feedbackGeneration = 0;
  readonly navigationItems = computed(() =>
    operatorSections.map(({ id, label }) => ({ id, label, disabled: this.busy() })));
  readonly activeSection = computed(() =>
    operatorSections.find(section => section.id === this.panel()) ?? operatorSections[0]);

  // Switching views is a UI-only intent. Operator authentication, roles and
  // ownership remain enforced exclusively by the platform backend.
  selectPanel(requested: string): void {
    const next = operatorSectionFor(requested);
    if (!next || this.busy() || next === this.panel()) return;
    this.feedbackGeneration++;
    this.feedbackScope.set('');
    this.error.set('');
    this.message.set('');
    // Operator backup codes already expire in the platform service; do not
    // discard newly issued methods merely because an internal tab changed.
    if (next === 'seguridad') {
      void this.openSecurity(); // Preserve the pre-existing policy load contract.
    } else {
      this.panel.set(next);
    }
  }
  async action(event: Event, action: string): Promise<void> {
    event.preventDefault(); const form = event.currentTarget as HTMLFormElement; const data = new FormData(form);
    const field = (name: string): string => String(data.get(name) ?? '');
    await this.run(async () => {
      if (action === 'login') await this.platform.login(field('mailbox'), field('password'));
      if (action === 'mfa') await this.platform.mfa(field('code'));
      if (action === 'case') await this.platform.lookup(field('caseId'));
      if (action === 'find-case') await this.platform.findCase(field('mailbox'), field('realm'), field('reason'), field('code'));
      if (action === 'review') {
        if (field('verified') !== 'on' || field('confirmed') !== 'on' ||
            !['approve', 'reject'].includes(field('decision'))) throw new Error('identity_unavailable');
        await this.platform.review(field('caseId'), field('decision'), field('evidenceHash'), field('reason'), field('code'));
      }
      if (action === 'share') await this.platform.releaseShare(field('caseId'), field('code'));
      if (action === 'password') await this.platform.changePassword(field('currentPassword'), field('newPassword'), field('code'));
      if (action === 'session-policy') await this.platform.updateSessionPolicy(field('minutes'), field('expectedRevision'), field('code'));
      if (action === 'revoke-all') await this.platform.revokeAll(field('code'));
      if (action === 'mailbox-request') await this.platform.requestMailbox(field('mailbox'), field('code'));
      if (action === 'mailbox-complete') await this.platform.completeMailbox(field('token'), field('code'));
      if (action === 'factor') await this.platform.replaceFactor(field('currentPassword'), field('code'));
      if (action === 'factor-confirm') await this.platform.confirmFactor(field('code'));
      if (action === 'operator-recovery-request') {
        if (field('unavailable') !== 'on') throw new Error('identity_unavailable');
        await this.platform.requestRecovery(field('mailbox'));
        this.recoveryStep.set(2);
      }
      if (action === 'operator-recovery-complete') await this.platform.completeRecovery(field('caseId'), field('shareA'), field('shareB'), field('newPassword'));
      if (action === 'operator-recovery-enroll') await this.platform.confirmRecovery(field('code'));
      if (action === 'operator-incident') await this.platform.reportIncident(field('token'));
      if (action === 'profile') await this.platform.saveProfile(field('displayName'), field('language'), field('timeZone'));
      form.reset();
      const receipts: Readonly<Record<string, string>> = {
        login: 'Comprueba ahora el segundo factor del operador.',
        mfa: 'Identidad de plataforma verificada.',
        case: 'Expediente consultado conforme a los permisos de plataforma.',
        'find-case': 'Referencia localizada exclusivamente bajo el ámbito autorizado.',
        review: 'Revisión de identidad registrada; no sustituye una segunda revisión independiente.',
        share: 'Parte de recuperación emitida para entrega segura y temporal.',
        profile: 'Perfil institucional actualizado.',
        password: 'Cambio de contraseña procesado; inicia una nueva sesión.',
        'session-policy': 'Parámetro de inactividad registrado después de la verificación.',
        'revoke-all': 'Sesiones de plataforma cerradas.',
        'mailbox-request': 'Se solicitó la verificación del nuevo correo.',
        'mailbox-complete': 'Nuevo correo verificado; vuelve a autenticarte.',
        factor: 'Verifica el autenticador nuevo antes de activarlo.',
        'factor-confirm': 'Autenticador actualizado; vuelve a iniciar sesión.'
      };
      this.message.set(receipts[action] ?? 'Solicitud procesada en identidad de plataforma.');
    }, action, form);
  }
  async logout(): Promise<void> {
    await this.run(() => this.platform.logout(), 'logout');
    if (this.platform.state() === 'anonymous') this.panel.set('casos');
  }
  async loadProfile(): Promise<void> { await this.run(() => this.platform.loadProfile(), 'profile-read'); }
  async loadAbuse(): Promise<void> { await this.run(() => this.platform.loadAbuse(), 'load-abuse'); }
  async openSecurity(): Promise<void> {
    this.panel.set('seguridad');
    await this.run(() => this.platform.loadSessionPolicy(), 'session-policy-read');
  }
  async loadSessions(): Promise<void> { await this.run(() => this.platform.loadSessions(), 'sessions-list'); }
  async rotateSession(): Promise<void> {
    await this.run(async () => { await this.platform.rotateSession(); this.message.set('Sesión de operador renovada y vinculada a nuevas credenciales de navegador.'); }, 'sessions-rotate');
  }
  async revokeSession(id: string): Promise<void> {
    await this.run(async () => { await this.platform.revokeSession(id); this.message.set('Sesión seleccionada revocada en el servidor.'); }, 'sessions-revoke');
  }
  async photo(event: Event): Promise<void> {
    event.preventDefault(); const form = event.currentTarget as HTMLFormElement; const file = new FormData(form).get('photo');
    await this.run(async () => { if (!(file instanceof File)) throw new Error('identity_unavailable'); await this.platform.uploadPhoto(file); form.reset(); }, 'profile-photo', form);
  }
  private async run(
    action: () => Promise<void>, command = 'operation', form?: HTMLFormElement
  ): Promise<void> {
    if (this.busy()) return;
    const generation = ++this.feedbackGeneration;
    const origin = this.panel();
    this.feedbackScope.set(origin);
    this.activity.set(activityLabel(command));
    this.busy.set(true);
    this.error.set('');
    this.message.set('');
    try {
      await action();
      if (generation === this.feedbackGeneration && this.panel() === origin &&
          !this.message() && !this.error() && command !== 'session-policy-read')
        this.message.set('La solicitud de plataforma terminó. Comprueba el resultado autorizado.');
    } catch (caught) {
      if (generation === this.feedbackGeneration && this.panel() === origin)
        this.error.set(IdentityApiService.publicError(caught));
    } finally {
      clearSensitiveInputValues(form);
      if (generation !== this.feedbackGeneration || this.panel() !== origin) {
        this.feedbackScope.set('');
        this.message.set('');
        this.error.set('');
      }
      this.busy.set(false);
    }
  }
}
