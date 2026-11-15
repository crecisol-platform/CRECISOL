import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { IdentityApiService } from '../identity/identity-api.service';

@Component({
  selector: 'cs-identity-portal',
  standalone: true,
  imports: [],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="identity" id="contenido" tabindex="-1">
      <h1>Acceso a tu cuenta</h1>
      <p>La empresa se determina por el dominio verificado. No selecciones una empresa aquí.</p>
      @if (identity.state() === 'anonymous') {
        <section aria-labelledby="login-heading">
          <h2 id="login-heading">Iniciar sesión</h2>
          <form (submit)="login($event)">
            <label for="login-mailbox">Correo electrónico</label>
            <input id="login-mailbox" name="mailbox" type="email" autocomplete="username"
              required maxlength="254">
            <label for="login-password">Contraseña</label>
            <input id="login-password" name="password" type="password"
              autocomplete="current-password" required maxlength="256">
            <button type="submit" [disabled]="busy()">{{ busy() ? 'Verificando…' : 'Continuar' }}</button>
          </form>
        </section>
        <section aria-labelledby="register-heading">
          <h2 id="register-heading">Solicitar una cuenta</h2>
          <p>La solicitud no activa una membresía ni concede acceso a una empresa.</p>
          <form (submit)="register($event)">
            <label for="register-mailbox">Correo electrónico</label>
            <input id="register-mailbox" name="mailbox" type="email" autocomplete="email"
              required maxlength="254">
            <button type="submit" [disabled]="busy()">{{ busy() ? 'Enviando…' : 'Enviar solicitud' }}</button>
          </form>
        </section>
        <section aria-labelledby="confirm-heading">
          <h2 id="confirm-heading">Confirmar correo</h2>
          <p>Confirma el correo y crea una cuenta pendiente de aprobación. La solicitud no concede acceso.</p>
          <form (submit)="confirmRegistration($event)">
            <label for="proof-id">Identificador de confirmación</label>
            <input id="proof-id" name="proofId" type="text" autocomplete="off"
              required maxlength="36" pattern="[0-9a-fA-F-]{36}">
            <label for="proof-code">Código de confirmación</label>
            <input id="proof-code" name="proofCode" type="text" inputmode="text"
              autocomplete="one-time-code" required minlength="64" maxlength="64"
              pattern="[0-9a-fA-F]{64}">
            <label for="completion-mailbox">Correo de la solicitud</label>
            <input id="completion-mailbox" name="completionMailbox" type="email" autocomplete="email"
              required maxlength="254">
            <label for="completion-password">Crear contraseña</label>
            <input id="completion-password" name="completionPassword" type="password"
              autocomplete="new-password" required minlength="15" maxlength="128">
            <button type="submit" [disabled]="busy()">{{ busy() ? 'Verificando…' : 'Crear cuenta pendiente' }}</button>
          </form>
        </section>
      }
      @if (identity.state() === 'challenge') {
        <section aria-labelledby="mfa-heading">
          <h2 id="mfa-heading">Verificación en dos pasos</h2>
          @if (identity.enrollmentRequired()) {
            <p>Configura tu aplicación de autenticación con SHA256, seis dígitos y periodo de 30 segundos.</p>
            @if (!identity.enrollmentSecret()) {
              <button type="button" [disabled]="busy()" (click)="enroll()">Configurar segundo factor</button>
            } @else {
              <p>Guarda esta clave en tu aplicación. Se muestra una sola vez en este recorrido.</p>
              <output aria-label="Clave de configuración MFA">{{ identity.enrollmentSecret() }}</output>
            }
          }
          <form (submit)="verify($event)">
            <label for="mfa-code">Código de tu aplicación de autenticación</label>
            <input id="mfa-code" name="mfaCode" inputmode="numeric" autocomplete="one-time-code"
              required minlength="6" maxlength="32" pattern="([0-9]{6}|[0-9a-fA-F]{32})">
            <p>También puedes usar uno de tus códigos de respaldo de un solo uso.</p>
            <button type="submit" [disabled]="busy()">{{ busy() ? 'Verificando…' : 'Verificar' }}</button>
          </form>
          <button type="button" [disabled]="busy()" (click)="identity.clear()">Volver a iniciar sesión</button>
        </section>
      }
      @if (identity.state() === 'authenticated') {
        <section aria-labelledby="session-heading">
          <h2 id="session-heading">Sesión verificada</h2>
          @if (identity.recoveryCodes().length) {
            <aside aria-label="Códigos de respaldo MFA">
              <p>Guarda estos códigos fuera del navegador. Cada código sirve una sola vez y no volverá a mostrarse.</p>
              <ul>@for (code of identity.recoveryCodes(); track code) { <li><code>{{ code }}</code></li> }</ul>
              <button type="button" (click)="identity.recoveryCodes.set([])">Ya guardé mis códigos</button>
            </aside>
          }
          <p>La autenticación no concede por sí sola autorización a recursos privados.</p>
          <button type="button" [disabled]="busy()" (click)="sessionAction('list')">Consultar sesiones</button>
          <button type="button" [disabled]="busy()" (click)="sessionAction('rotate')">Rotar sesión</button>
          @for (session of identity.sessions(); track session.id) {
            <p>Sesión {{ session.id }} · vence {{ session.expiresAtUtc }}</p>
            <button type="button" [disabled]="busy()" (click)="revoke(session.id)">Revocar sesión {{ session.id }}</button>
          }
          <form (submit)="securityAction($event, 'private')">
            <label for="private-branch">Sucursal del recurso</label><input id="private-branch" name="branchId" required maxlength="36">
            <label for="private-resource">Identificador del recurso</label><input id="private-resource" name="resourceId" required maxlength="36">
            <button [disabled]="busy()">Consultar recurso autorizado</button>
          </form>
          @if (identity.privateReference()) { <p aria-label="Referencia privada">{{ identity.privateReference() }}</p> }
          <details><summary>Cambiar contraseña</summary>
            <form (submit)="securityAction($event, 'password')">
              <label for="current-password">Contraseña actual</label><input id="current-password" name="currentPassword" type="password" autocomplete="current-password" required maxlength="128">
              <label for="new-password">Nueva contraseña</label><input id="new-password" name="newPassword" type="password" autocomplete="new-password" required minlength="15" maxlength="128">
              <label for="password-mfa">Código MFA para contraseña</label><input id="password-mfa" name="code" autocomplete="one-time-code" required maxlength="32" pattern="([0-9]{6}|[0-9a-fA-F]{32})">
              <button [disabled]="busy()">Actualizar contraseña</button>
            </form>
          </details>
          <details><summary>Reemplazar segundo factor</summary>
            <p>Requiere tu contraseña y el factor actual o un código de respaldo. Revoca los factores y sesiones anteriores.</p>
            <form (submit)="securityAction($event, 'factor')">
              <label for="factor-password">Contraseña para reemplazar MFA</label><input id="factor-password" name="currentPassword" type="password" autocomplete="current-password" required maxlength="128">
              <label for="factor-code">Código actual o respaldo MFA</label><input id="factor-code" name="code" autocomplete="one-time-code" required maxlength="32" pattern="([0-9]{6}|[0-9a-fA-F]{32})">
              <button [disabled]="busy()">Reemplazar factor MFA</button>
            </form>
          </details>
          <details><summary>Cambiar correo</summary>
            <form (submit)="securityAction($event, 'mailbox-request')">
              <label for="new-mailbox">Nuevo correo</label><input id="new-mailbox" name="mailbox" type="email" autocomplete="email" required maxlength="254">
              <label for="mailbox-mfa">Código MFA para solicitar correo</label><input id="mailbox-mfa" name="code" autocomplete="one-time-code" required maxlength="32" pattern="([0-9]{6}|[0-9a-fA-F]{32})">
              <button [disabled]="busy()">Solicitar cambio de correo</button>
            </form>
            <form (submit)="securityAction($event, 'mailbox-complete')">
              <label for="mailbox-token">Token del nuevo correo</label><input id="mailbox-token" name="token" autocomplete="off" required pattern="[0-9a-fA-F]{64}">
              <label for="mailbox-confirm-mfa">Código MFA para confirmar correo</label><input id="mailbox-confirm-mfa" name="code" autocomplete="one-time-code" required maxlength="32" pattern="([0-9]{6}|[0-9a-fA-F]{32})">
              <button [disabled]="busy()">Confirmar cambio de correo</button>
            </form>
          </details>
          <details><summary>Revisar membresía con permiso administrativo</summary>
            <p>Esta acción requiere autorización vigente del servidor. No concede permisos al solicitante.</p>
            <form (submit)="securityAction($event, 'membership')">
              <label for="membership-id">Membresía pendiente</label><input id="membership-id" name="membershipId" required maxlength="36">
              <label for="membership-decision">Decisión</label><select id="membership-decision" name="decision"><option value="approve">Aprobar</option><option value="reject">Rechazar</option></select>
              <label for="membership-reason">Motivo</label><input id="membership-reason" name="reason" required maxlength="240">
              <label for="membership-code">Código MFA del aprobador</label><input id="membership-code" name="code" autocomplete="one-time-code" required maxlength="32" pattern="([0-9]{6}|[0-9a-fA-F]{32})">
              <button [disabled]="busy()">Registrar decisión</button>
            </form>
          </details>
          <button type="button" [disabled]="busy()" (click)="logout()">Cerrar sesión</button>
        </section>
      }
      @if (identity.state() === 'anonymous') {
        <section aria-labelledby="recovery-heading"><h2 id="recovery-heading">Recuperar contraseña</h2>
          <p>La recuperación requiere el correo verificado y tu segundo factor. No inicia una sesión.</p>
          <form (submit)="securityAction($event, 'recovery-request')">
            <label for="recovery-mailbox">Correo para recuperación</label><input id="recovery-mailbox" name="mailbox" type="email" autocomplete="email" required maxlength="254">
            <button [disabled]="busy()">Solicitar recuperación</button>
          </form>
          <form (submit)="securityAction($event, 'recovery-complete')">
            <label for="recovery-token">Token de recuperación</label><input id="recovery-token" name="token" autocomplete="off" required pattern="[0-9a-fA-F]{64}">
            <label for="recovery-code">Código MFA para recuperación</label><input id="recovery-code" name="code" autocomplete="one-time-code" required maxlength="32" pattern="([0-9]{6}|[0-9a-fA-F]{32})">
            <label for="recovery-password">Contraseña recuperada</label><input id="recovery-password" name="newPassword" type="password" autocomplete="new-password" required minlength="15" maxlength="128">
            <button [disabled]="busy()">Restablecer contraseña</button>
          </form>
        </section>
      }
      @if (message()) { <p role="status" aria-live="polite">{{ message() }}</p> }
      @if (error()) { <p role="alert">{{ error() }}</p> }
    </main>
  `,
  styles: [`
    .identity { max-width: 40rem; margin: 2rem auto; padding: 1.5rem; }
    section { margin-block: 2rem; padding-block: 1rem; border-top: 1px solid #a8a8a8; }
    form { display: grid; gap: .75rem; max-width: 28rem; }
    input { min-height: 2.75rem; padding: .5rem; font: inherit; }
    button { min-height: 2.75rem; padding: .5rem 1rem; font: inherit; }
    :focus-visible { outline: 3px solid #165dff; outline-offset: 2px; }
  `]
})
export class IdentityPortalComponent {
  readonly identity = inject(IdentityApiService);
  readonly busy = signal(false);
  readonly message = signal('');
  readonly error = signal('');

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
    await this.identity.logout();
    this.message.set('La sesión local se cerró.');
  }

  async enroll(): Promise<void> { await this.run(() => this.identity.enrollMfa()); }
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
      this.message.set(action === 'recovery-request' ? 'Si la cuenta cumple los requisitos, recibirás instrucciones en el correo verificado.' : 'Solicitud procesada. Los cambios de credenciales cierran las sesiones anteriores.');
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


