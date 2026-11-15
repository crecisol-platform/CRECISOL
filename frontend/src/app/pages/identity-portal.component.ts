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
      }
      @if (identity.state() === 'challenge') {
        <section aria-labelledby="mfa-heading">
          <h2 id="mfa-heading">Verificación en dos pasos</h2>
          <form (submit)="verify($event)">
            <label for="mfa-code">Código de tu aplicación de autenticación</label>
            <input id="mfa-code" name="mfaCode" inputmode="numeric" autocomplete="one-time-code"
              required minlength="6" maxlength="8">
            <button type="submit" [disabled]="busy()">{{ busy() ? 'Verificando…' : 'Verificar' }}</button>
          </form>
        </section>
      }
      @if (identity.state() === 'authenticated') {
        <section aria-labelledby="private-heading">
          <h2 id="private-heading">Sesión autenticada</h2>
          <p>La API verifica tenant, sucursal, recurso y permiso en cada solicitud.</p>
          <label for="case-id">Identificador de recurso</label>
          <input id="case-id" name="caseId" autocomplete="off" (input)="setResourceId($event)">
          <button type="button" [disabled]="busy()" (click)="readPrivateCase()">Abrir recurso</button>
          <button type="button" [disabled]="busy()" (click)="logout()">Cerrar sesión</button>
          @if (resourceKey()) { <p role="status">Recurso autorizado.</p> }
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
  readonly resourceKey = signal('');
  resourceId = '';

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

  async readPrivateCase(): Promise<void> {
    await this.run(async () => {
      await this.identity.readPrivateCase(this.resourceId);
      this.resourceKey.set('authorized');
      this.message.set('El recurso fue autorizado por el servidor.');
    });
  }

  async logout(): Promise<void> {
    await this.identity.logout();
    this.resourceKey.set('');
    this.message.set('La sesión local se cerró.');
  }

  setResourceId(event: Event): void {
    this.resourceId = (event.target as HTMLInputElement).value;
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


