import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { PlatformIdentityApiService } from '../identity/platform-identity-api.service';
import { IdentityApiService } from '../identity/identity-api.service';

@Component({
  selector: 'cs-platform-identity-panel', standalone: true, changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <details><summary>Personal autorizado de identidad CRECISOL</summary>
      <h2>Identidad de plataforma</h2>
      <p>Acceso exclusivo de operadores previamente provisionados. Una cuenta de negocio no concede acceso a esta consola ni permite recuperar identidades globales.</p>
      @if (platform.state() === 'anonymous') {
        <form (submit)="action($event, 'login')">
          <label for="platform-mailbox">Correo del operador de identidad</label><input id="platform-mailbox" name="mailbox" type="email" autocomplete="username" required maxlength="254">
          <label for="platform-password">Contraseña del operador</label><input id="platform-password" name="password" type="password" autocomplete="current-password" required maxlength="128">
          <button [disabled]="busy() || platform.restoring()">Acceder a identidad de plataforma</button>
        </form>
        <details><summary>Recuperar acceso de operador de plataforma</summary>
          <p>Se necesitan otros dos operadores de identidad, verificación independiente y al menos 24 horas. Ninguna aprobación de un negocio sirve para este ámbito.</p>
          <form (submit)="action($event, 'operator-recovery-request')">
            <label for="operator-recovery-mail">Correo del operador sin acceso</label><input id="operator-recovery-mail" name="mailbox" type="email" required maxlength="254" autocomplete="email">
            <label><input name="unavailable" type="checkbox" required> No tengo disponibles mis métodos seguros de operador</label>
            <button [disabled]="busy()">Solicitar recuperación de operador</button>
          </form>
          @if (platform.recoveryReference()) { <output aria-label="Referencia de recuperación de operador">{{ platform.recoveryReference() }}</output> }
          <form (submit)="action($event, 'operator-recovery-complete')">
            <label for="operator-recovery-case">Caso aprobado de operador</label><input id="operator-recovery-case" name="caseId" required maxlength="36">
            <label for="operator-share-a">Parte del primer revisor de operador</label><input id="operator-share-a" name="shareA" required pattern="[0-9a-fA-F]{64}" autocomplete="off">
            <label for="operator-share-b">Parte del segundo revisor de operador</label><input id="operator-share-b" name="shareB" required pattern="[0-9a-fA-F]{64}" autocomplete="off">
            <label for="operator-recovery-password">Nueva contraseña del operador recuperado</label><input id="operator-recovery-password" name="newPassword" type="password" required minlength="15" maxlength="128" autocomplete="new-password">
            <button [disabled]="busy()">Iniciar reenrolamiento de operador</button>
          </form>
          @if (platform.operatorRecoverySeed()) {
            <p>Configura TOTP SHA256, seis dígitos y 30 segundos. Esta autorización vence en cinco minutos.</p>
            <output aria-label="Semilla MFA del operador recuperado">{{ platform.operatorRecoverySeed() }}</output>
            <form (submit)="action($event, 'operator-recovery-enroll')">
              <label for="operator-recovery-code">Código del nuevo MFA de operador</label><input id="operator-recovery-code" name="code" required pattern="[0-9]{6}" autocomplete="one-time-code">
              <button [disabled]="busy()">Confirmar nuevo MFA de operador</button>
            </form>
          }
          <form (submit)="action($event, 'operator-incident')">
            <label for="operator-incident-token">Código del aviso de seguridad de operador</label><input id="operator-incident-token" name="token" required pattern="[0-9a-fA-F]{64}" autocomplete="off">
            <button [disabled]="busy()">Reportar incidente de operador</button>
          </form>
        </details>
      }
      @if (platform.state() === 'challenge') {
        <form (submit)="action($event, 'mfa')">
          <label for="platform-mfa">Código MFA del operador</label><input id="platform-mfa" name="code" inputmode="numeric" autocomplete="one-time-code" required pattern="([0-9]{6}|[0-9a-fA-F]{32})">
          <button [disabled]="busy()">Verificar operador</button>
        </form>
      }
      @if (platform.state() === 'authenticated') {
        <p role="status">Operador de identidad verificado. Esta sesión pertenece al ámbito de plataforma.</p>
        <details><summary>Mis sesiones de plataforma</summary>
          <button type="button" [disabled]="busy()" (click)="loadSessions()">Consultar sesiones de operador</button>
          <button type="button" [disabled]="busy()" (click)="rotateSession()">Rotar sesión de operador</button>
          @for (session of platform.sessions(); track session.id) {
            <p>{{ session.device }} · última actividad UTC {{ session.lastSeenAtUtc }} · vence {{ session.expiresAtUtc }}</p>
            <button type="button" [disabled]="busy()" (click)="revokeSession(session.id)">Revocar sesión de operador {{ session.id }}</button>
          }
          <form (submit)="action($event, 'revoke-all')">
            <label for="operator-sessions-code">Código MFA para cerrar sesiones de plataforma</label><input id="operator-sessions-code" name="code" required maxlength="32" autocomplete="one-time-code">
            <button [disabled]="busy()">Cerrar todas mis sesiones de plataforma</button>
          </form>
        </details>
        <details><summary>Mi perfil de operador</summary>
          <button type="button" [disabled]="busy()" (click)="loadProfile()">Consultar perfil de operador</button>
          @if (platform.profile(); as profile) { <p aria-label="Nombre del perfil de operador">{{ profile.displayName }}</p> }
          @if (platform.photo()) { <img [src]="platform.photo()" width="64" height="64" alt="Foto de mi perfil de operador"> }
          <form (submit)="action($event, 'profile')">
            <label for="operator-profile-name">Nombre visible del operador</label><input id="operator-profile-name" name="displayName" required maxlength="80" autocomplete="nickname">
            <label for="operator-profile-language">Idioma del perfil de operador</label><select id="operator-profile-language" name="language"><option value="es-HN">Español de Honduras</option><option value="es">Español</option><option value="en">English</option></select>
            <label for="operator-profile-zone">Zona horaria del operador</label><input id="operator-profile-zone" name="timeZone" required maxlength="64" value="America/Tegucigalpa">
            <button [disabled]="busy()">Guardar perfil de operador</button>
          </form>
          <form (submit)="photo($event)">
            <label for="operator-photo">Foto del operador (PNG, JPEG o WebP, hasta 2 MB)</label><input id="operator-photo" name="photo" type="file" accept="image/png,image/jpeg,image/webp" required>
            <button [disabled]="busy()">Guardar foto de operador</button>
          </form>
        </details>
        <details><summary>Señales de abuso de identidad</summary>
          <p>Solo cantidades agregadas; no expone correos, direcciones IP ni datos de otros negocios.</p>
          <button type="button" [disabled]="busy()" (click)="loadAbuse()">Consultar señales agregadas</button>
          <ul>@for (signal of platform.abuse(); track signal.kind) { <li>{{ signal.kind }}: {{ signal.count }}</li> }</ul>
        </details>
        <details><summary>Cambiar contraseña de operador</summary>
          <form (submit)="action($event, 'password')">
            <label for="operator-current-password">Contraseña actual del operador</label><input id="operator-current-password" name="currentPassword" type="password" required maxlength="128" autocomplete="current-password">
            <label for="operator-new-password">Nueva contraseña de operador</label><input id="operator-new-password" name="newPassword" type="password" required minlength="15" maxlength="128" autocomplete="new-password">
            <label for="operator-password-code">Código MFA para cambiar contraseña de operador</label><input id="operator-password-code" name="code" required maxlength="32" autocomplete="one-time-code">
            <button [disabled]="busy()">Cambiar contraseña de plataforma</button>
          </form>
        </details>
        <details><summary>Cambiar correo de operador</summary>
          <form (submit)="action($event, 'mailbox-request')">
            <label for="operator-new-mailbox">Nuevo correo del operador</label><input id="operator-new-mailbox" name="mailbox" type="email" required maxlength="254" autocomplete="email">
            <label for="operator-mailbox-code">Código MFA para solicitar correo de operador</label><input id="operator-mailbox-code" name="code" required maxlength="32" autocomplete="one-time-code">
            <button [disabled]="busy()">Solicitar nuevo correo de operador</button>
          </form>
          <form (submit)="action($event, 'mailbox-complete')">
            <label for="operator-mailbox-token">Token del nuevo correo de operador</label><input id="operator-mailbox-token" name="token" required pattern="[0-9a-fA-F]{64}" autocomplete="off">
            <label for="operator-mailbox-confirm-code">Código MFA para confirmar correo de operador</label><input id="operator-mailbox-confirm-code" name="code" required maxlength="32" autocomplete="one-time-code">
            <button [disabled]="busy()">Confirmar correo de operador</button>
          </form>
        </details>
        <details><summary>Reemplazar MFA de operador</summary>
          <form (submit)="action($event, 'factor')">
            <label for="operator-factor-password">Contraseña para reemplazar MFA de operador</label><input id="operator-factor-password" name="currentPassword" type="password" required maxlength="128" autocomplete="current-password">
            <label for="operator-factor-code">Código MFA o respaldo para reemplazar factor de operador</label><input id="operator-factor-code" name="code" required maxlength="32" autocomplete="one-time-code">
            <button [disabled]="busy()">Preparar nuevo factor de operador</button>
          </form>
          @if (platform.factorSecret()) {
            <output aria-label="Semilla del factor de operador a reemplazar">{{ platform.factorSecret() }}</output>
            <form (submit)="action($event, 'factor-confirm')">
              <label for="operator-factor-confirm-code">Código del factor nuevo de operador</label><input id="operator-factor-confirm-code" name="code" required pattern="[0-9]{6}" autocomplete="one-time-code">
              <button [disabled]="busy()">Activar nuevo factor de operador</button>
            </form>
          }
        </details>
        <form (submit)="action($event, 'case')">
          <label for="platform-case">Referencia del caso de recuperación</label><input id="platform-case" name="caseId" required maxlength="36">
          <button [disabled]="busy()">Consultar caso de identidad</button>
        </form>
        <details><summary>Localizar una solicitud de identidad pendiente</summary>
          <p>Permite localizar un caso cuando el titular perdió su referencia o recibió una referencia neutra por una solicitud previa. Requiere un motivo de soporte y MFA fresco; no devuelve empresas, perfiles ni correos privados.</p>
          <form (submit)="action($event, 'find-case')">
            <label for="find-case-mail">Correo indicado por el titular del caso</label><input id="find-case-mail" name="mailbox" type="email" required maxlength="254" autocomplete="off">
            <label for="find-case-realm">Ámbito del caso de identidad</label><select id="find-case-realm" name="realm"><option value="TENANT_IDENTITY">Identidad de negocio</option><option value="PLATFORM_OPERATOR">Operador de plataforma</option></select>
            <label for="find-case-reason">Motivo de localización del caso</label><input id="find-case-reason" name="reason" required minlength="12" maxlength="240">
            <label for="find-case-code">Código MFA para localizar caso</label><input id="find-case-code" name="code" required maxlength="32" autocomplete="one-time-code">
            <button [disabled]="busy()">Localizar referencia de identidad</button>
          </form>
          @if (platform.locatedCase()) { <output aria-label="Referencia localizada de identidad">{{ platform.locatedCase() }}</output> }
        </details>
        @if (platform.case(); as item) {
          <p>Referencia {{ item.caseId }} · bloqueado {{ item.blocked }} · completado {{ item.consumed }}</p>
          <p>Disponible después de: {{ item.readyAtUtc ?? 'pendiente de doble revisión' }}</p>
        }
        <form (submit)="action($event, 'review')">
          <label for="review-case">Caso a revisar</label><input id="review-case" name="caseId" required maxlength="36">
          <label for="review-decision">Decisión de identidad</label><select id="review-decision" name="decision"><option value="approve">Aprobar</option><option value="reject">Rechazar</option></select>
          <label for="review-evidence">SHA-256 del expediente de verificación</label><input id="review-evidence" name="evidenceHash" required pattern="[0-9a-fA-F]{64}" autocomplete="off">
          <label for="review-reason">Motivo de la revisión de identidad</label><input id="review-reason" name="reason" required minlength="12" maxlength="240">
          <label for="review-code">Código MFA fresco para revisar</label><input id="review-code" name="code" required pattern="([0-9]{6}|[0-9a-fA-F]{32})" autocomplete="one-time-code">
          <label><input name="verified" type="checkbox" required> Verifiqué la identidad de forma independiente; no usé preguntas personales ni solo un correo nuevo</label>
          <button [disabled]="busy()">Registrar revisión de identidad</button>
        </form>
        <p>Se necesitan dos operadores distintos y un mínimo de 24 horas. Un rechazo o una denuncia bloquea el caso. En este entorno usa únicamente expedientes ficticios.</p>
        <form (submit)="action($event, 'share')">
          <label for="share-case">Caso para entrega segura</label><input id="share-case" name="caseId" required maxlength="36">
          <label for="share-code">Código MFA para emitir mi parte</label><input id="share-code" name="code" required pattern="([0-9]{6}|[0-9a-fA-F]{32})" autocomplete="one-time-code">
          <button [disabled]="busy()">Emitir mi parte de recuperación</button>
        </form>
        @if (platform.handoffShare()) {
          <p>Entrega esta parte solo al titular cuya identidad verificaste, por un canal independiente del otro operador. Vence en 30 minutos; una nueva emisión invalida la anterior.</p>
          <output aria-label="Parte de recuperación del operador">{{ platform.handoffShare() }}</output>
          <button type="button" (click)="platform.handoffShare.set('')">Ocultar mi parte</button>
        }
        <button type="button" [disabled]="busy()" (click)="logout()">Cerrar sesión de plataforma</button>
      }
      @if (platform.recoveryCodes().length) {
        <aside aria-label="Códigos de respaldo del operador"><p>Guarda estos códigos fuera del navegador. Cada uno sirve una sola vez.</p>
          <ul>@for (code of platform.recoveryCodes(); track code) { <li><code>{{ code }}</code></li> }</ul>
          <button type="button" (click)="platform.recoveryCodes.set([])">Ocultar respaldos de operador</button>
        </aside>
      }
      @if (message()) { <p role="status">{{ message() }}</p> }
      @if (error()) { <p role="alert">{{ error() }}</p> }
    </details>
  `,
  styles: [`form{display:grid;gap:.75rem;margin-block:1.5rem;max-width:28rem}input,button,select{font:inherit;min-height:2.75rem;min-width:0;max-width:100%;box-sizing:border-box}output,code{overflow-wrap:anywhere}img{max-width:100%;height:auto}:focus-visible{outline:3px solid #165dff;outline-offset:2px}`]
})
export class PlatformIdentityPanelComponent {
  readonly platform = inject(PlatformIdentityApiService);
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
