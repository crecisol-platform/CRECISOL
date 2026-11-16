import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { PlatformIdentityApiService } from '../identity/platform-identity-api.service';
import { IdentityApiService } from '../identity/identity-api.service';
import { InstitutionalModalComponent } from '../ui/institutional-modal/institutional-modal.component';
import { PlatformOperatorSessionsComponent, PlatformOperatorProfileComponent, PlatformOperatorMonitorComponent, PlatformOperatorSecurityComponent, PlatformOperatorCasesComponent } from './platform-operator-views.component';

@Component({
  selector: 'cs-platform-identity-panel', standalone: true, changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [InstitutionalModalComponent, PlatformOperatorSessionsComponent, PlatformOperatorProfileComponent, PlatformOperatorMonitorComponent, PlatformOperatorSecurityComponent, PlatformOperatorCasesComponent],
  templateUrl: './platform-identity-panel.component.html',
  styleUrl: './identity-flow.component.css'
})
export class PlatformIdentityPanelComponent {
  readonly platform = inject(PlatformIdentityApiService);
  readonly panel = signal<'casos' | 'sesiones' | 'perfil' | 'seguridad' | 'monitor'>('casos');
  readonly busy = signal(false); readonly message = signal(''); readonly error = signal('');
  async action(event: Event, action: string): Promise<void> {
    event.preventDefault(); const form = event.currentTarget as HTMLFormElement; const data = new FormData(form);
    const field = (name: string): string => String(data.get(name) ?? '');
    await this.run(async () => {
      if (action === 'login') await this.platform.login(field('mailbox'), field('password'));
      if (action === 'mfa') await this.platform.mfa(field('code'));
      if (action === 'case') await this.platform.lookup(field('caseId'));
      if (action === 'find-case') await this.platform.findCase(field('mailbox'), field('realm'), field('reason'), field('code'));
      if (action === 'review') {
        if (field('verified') !== 'on') throw new Error('identity_unavailable');
        await this.platform.review(field('caseId'), field('decision'), field('evidenceHash'), field('reason'), field('code'));
      }
      if (action === 'share') await this.platform.releaseShare(field('caseId'), field('code'));
      if (action === 'password') await this.platform.changePassword(field('currentPassword'), field('newPassword'), field('code'));
      if (action === 'revoke-all') await this.platform.revokeAll(field('code'));
      if (action === 'mailbox-request') await this.platform.requestMailbox(field('mailbox'), field('code'));
      if (action === 'mailbox-complete') await this.platform.completeMailbox(field('token'), field('code'));
      if (action === 'factor') await this.platform.replaceFactor(field('currentPassword'), field('code'));
      if (action === 'factor-confirm') await this.platform.confirmFactor(field('code'));
      if (action === 'operator-recovery-request') {
        if (field('unavailable') !== 'on') throw new Error('identity_unavailable');
        await this.platform.requestRecovery(field('mailbox'));
      }
      if (action === 'operator-recovery-complete') await this.platform.completeRecovery(field('caseId'), field('shareA'), field('shareB'), field('newPassword'));
      if (action === 'operator-recovery-enroll') await this.platform.confirmRecovery(field('code'));
      if (action === 'operator-incident') await this.platform.reportIncident(field('token'));
      if (action === 'profile') await this.platform.saveProfile(field('displayName'), field('language'), field('timeZone'));
      form.reset(); this.message.set('Solicitud procesada por el ámbito de identidad de plataforma.');
    });
  }
  async logout(): Promise<void> { await this.run(() => this.platform.logout()); }
  async loadProfile(): Promise<void> { await this.run(() => this.platform.loadProfile()); }
  async loadAbuse(): Promise<void> { await this.run(() => this.platform.loadAbuse()); }
  async loadSessions(): Promise<void> { await this.run(() => this.platform.loadSessions()); }
  async rotateSession(): Promise<void> { await this.run(() => this.platform.rotateSession()); }
  async revokeSession(id: string): Promise<void> { await this.run(() => this.platform.revokeSession(id)); }
  async photo(event: Event): Promise<void> {
    event.preventDefault(); const form = event.currentTarget as HTMLFormElement; const file = new FormData(form).get('photo');
    await this.run(async () => { if (!(file instanceof File)) throw new Error('identity_unavailable'); await this.platform.uploadPhoto(file); form.reset(); });
  }
  private async run(action: () => Promise<void>): Promise<void> {
    if (this.busy()) return; this.busy.set(true); this.error.set(''); this.message.set('');
    try { await action(); } catch (e) { this.error.set(IdentityApiService.publicError(e)); } finally { this.busy.set(false); }
  }
}
