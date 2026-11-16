import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { IdentityApiService } from '../identity/identity-api.service';
import { PlatformIdentityPanelComponent } from './platform-identity-panel.component';

@Component({
  selector: 'cs-identity-portal',
  standalone: true,
  imports: [PlatformIdentityPanelComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="identity" id="contenido" tabindex="-1">
      <h1>Acceso a tu cuenta</h1>
      <p>La empresa se determina por el dominio verificado. No selecciones una empresa aquí.</p>
      <details><summary>Condiciones de la cuenta</summary>
        <button type="button" [disabled]="busy() || identity.restoring()" (click)="loadTerms()">Leer condiciones vigentes</button>
        @if (identity.terms(); as terms) {
          <h2>Condiciones {{ terms.version }}</h2><p style="white-space:pre-wrap">{{ terms.text }}</p>
          @if (identity.state() === 'authenticated') {
            <form (submit)="securityAction($event, 'terms')">
              <label><input name="accepted" type="checkbox" required> Acepto esta versión de las condiciones</label>
              <button [disabled]="busy() || identity.restoring()">Registrar aceptación</button>
            </form>
          }
        }
      </details>
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
            <button type="submit" [disabled]="busy() || identity.restoring()">{{ busy() ? 'Verificando…' : 'Continuar' }}</button>
          </form>
          <p>Para usar una llave de acceso en este entorno local, abre <a href="http://localhost:4200/cuenta">la cuenta en localhost</a>.</p>
          <button type="button" [disabled]="busy() || identity.restoring()" (click)="passkeyLogin()">Entrar con llave de acceso</button>
          <details><summary>Entrar vinculando un navegador</summary>
            <p>Genera una solicitud y apruébala desde otro navegador donde ya tengas una sesión verificada. También puedes copiar los datos; no necesitas escanear el QR en este mismo dispositivo. La solicitud vence en dos minutos.</p>
            <button type="button" [disabled]="busy() || identity.restoring()" (click)="startPairing()">Crear solicitud de vinculación</button>
            @if (identity.pairing(); as pairing) {
              <img [src]="pairing.qr" width="320" height="320" alt="QR de solicitud de vinculación; también puedes copiar los datos siguientes">
              <p>Solicitud: <output aria-label="Solicitud de vinculación">{{ pairing.token }}</output></p>
              <p>Huella para comparar: <output aria-label="Huella de vinculación">{{ pairing.fingerprint }}</output></p>
              <button type="button" [disabled]="busy()" (click)="completePairing()">Completar vinculación aprobada</button>
            }
          </details>
        </section>
        <section aria-labelledby="register-heading">
          <h2 id="register-heading">Solicitar una cuenta</h2>
          <p>La solicitud no activa una membresía ni concede acceso a una empresa.</p>
          <form (submit)="register($event)">
            <label for="register-mailbox">Correo electrónico</label>
            <input id="register-mailbox" name="mailbox" type="email" autocomplete="email"
              required maxlength="254">
            <button type="submit" [disabled]="busy() || identity.restoring()">{{ busy() ? 'Enviando…' : 'Enviar solicitud' }}</button>
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
            <button type="submit" [disabled]="busy() || identity.restoring()">{{ busy() ? 'Verificando…' : 'Crear cuenta pendiente' }}</button>
          </form>
        </section>
        <details><summary>Aceptar una invitación</summary>
          <p>Primero confirma tu correo y crea una cuenta. Lee las condiciones vigentes antes de aceptar la invitación. Si todavía no tienes MFA, escribe enroll; si ya lo tienes, usa un código vigente o de respaldo.</p>
          <form (submit)="securityAction($event, 'invitation-accept')">
            <label for="invite-mailbox">Correo invitado</label><input id="invite-mailbox" name="mailbox" type="email" autocomplete="username" required maxlength="254">
            <label for="invite-password">Contraseña de la cuenta invitada</label><input id="invite-password" name="password" type="password" autocomplete="current-password" required maxlength="128">
            <label for="invite-code">Código MFA del invitado</label><input id="invite-code" name="code" required maxlength="32" autocomplete="one-time-code">
            <label for="invite-token">Token de invitación</label><input id="invite-token" name="token" required pattern="[0-9a-fA-F]{64}" autocomplete="off">
            <label><input name="accepted" type="checkbox" required> He leído y acepto las condiciones vigentes para esta invitación</label>
            <button [disabled]="busy() || !identity.terms()">Aceptar invitación de cuenta</button>
          </form>
        </details>
        <details><summary>Ya tengo una identidad; solicitar acceso aquí</summary>
          <p>Solicita primero una confirmación de correo en este negocio y lee sus condiciones. Demuestra tu contraseña y MFA existentes para crear solo una relación pendiente de aprobación. No se enlazan cuentas por el correo sin estas pruebas.</p>
          <form (submit)="securityAction($event, 'membership-request')">
            <label for="join-mailbox">Correo de mi identidad existente</label><input id="join-mailbox" name="mailbox" type="email" required maxlength="254" autocomplete="username">
            <label for="join-password">Contraseña de mi identidad existente</label><input id="join-password" name="password" type="password" required maxlength="128" autocomplete="current-password">
            <label for="join-code">Código MFA para solicitar relación</label><input id="join-code" name="code" required maxlength="32" autocomplete="one-time-code">
            <label for="join-proof-id">Identificador de confirmación para solicitar relación</label><input id="join-proof-id" name="proofId" required maxlength="36">
            <label for="join-proof-code">Código de correo para solicitar relación</label><input id="join-proof-code" name="proofCode" required pattern="[0-9a-fA-F]{64}" autocomplete="off">
            <label><input name="accepted" type="checkbox" required> Acepto las condiciones vigentes de este negocio para solicitar la relación</label>
            <button [disabled]="busy() || !identity.terms()">Enviar solicitud de relación pendiente</button>
          </form>
        </details>
      }
      @if (identity.state() === 'challenge') {
        <section aria-labelledby="mfa-heading">
          <h2 id="mfa-heading">Verificación en dos pasos</h2>
          @if (identity.enrollmentRequired()) {
            <p>Configura tu aplicación de autenticación con SHA256, seis dígitos y periodo de 30 segundos.</p>
            @if (!identity.enrollmentSecret()) {
              <button type="button" [disabled]="busy() || identity.restoring()" (click)="enroll()">Configurar segundo factor</button>
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
            <button type="submit" [disabled]="busy() || identity.restoring()">{{ busy() ? 'Verificando…' : 'Verificar' }}</button>
          </form>
          <button type="button" [disabled]="busy() || identity.restoring()" (click)="identity.clear()">Volver a iniciar sesión</button>
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
          <details><summary>Mi perfil en esta empresa</summary>
            <button type="button" [disabled]="busy()" (click)="loadProfile()">Consultar mi perfil</button>
            @if (identity.profile(); as profile) {
              <p aria-label="Nombre de mi perfil">{{ profile.displayName }}</p>
              <p>{{ profile.language }} · {{ profile.timeZone }}</p>
            }
            @if (identity.photo()) { <img [src]="identity.photo()" width="64" height="64" alt="Mi foto de perfil en esta empresa"> }
            <form (submit)="securityAction($event, 'profile')">
              <label for="profile-name">Nombre visible en esta empresa</label><input id="profile-name" name="displayName" required maxlength="80" autocomplete="nickname">
              <label for="profile-language">Idioma preferido</label><select id="profile-language" name="language"><option value="es-HN">Español de Honduras</option><option value="es">Español</option><option value="en">English</option></select>
              <label for="profile-zone">Zona horaria de tu perfil</label><input id="profile-zone" name="timeZone" value="America/Tegucigalpa" required maxlength="64" autocomplete="off">
              <button [disabled]="busy()">Guardar mi perfil</button>
            </form>
            <form (submit)="uploadPhoto($event)">
              <label for="profile-photo">Foto de perfil (PNG, JPEG o WebP, hasta 2 MB)</label><input id="profile-photo" name="photo" type="file" accept="image/png,image/jpeg,image/webp" required>
              <p>Se guarda una miniatura cuadrada sin los metadatos del original.</p>
              <button [disabled]="busy()">Guardar mi foto de perfil</button>
            </form>
            <button type="button" [disabled]="busy()" (click)="removePhoto()">Eliminar mi foto</button>
          </details>
          <details><summary>Mi historial de seguridad</summary>
            <button type="button" [disabled]="busy()" (click)="securityEvents()">Consultar mis eventos</button>
            <ul>@for (event of identity.securityEvents(); track $index) { <li>{{ event.atUtc }} · {{ event.action }} · resultado {{ event.outcome }}</li> }</ul>
          </details>
          <details><summary>Llaves de acceso</summary>
            <p>Registra una llave con verificación de usuario. Se necesita un código MFA o de respaldo para agregarla o revocarla.</p>
            <form (submit)="securityAction($event, 'passkey-add')">
              <label for="passkey-code">Código MFA para registrar llave</label><input id="passkey-code" name="code" autocomplete="one-time-code" required maxlength="32" pattern="([0-9]{6}|[0-9a-fA-F]{32})">
              <button [disabled]="busy() || identity.restoring()">Registrar llave de acceso</button>
            </form>
            <button type="button" [disabled]="busy() || identity.restoring()" (click)="passkeyList()">Consultar llaves</button>
            @for (key of identity.passkeys(); track key.id) { <p>Llave {{ key.id }} · registrada {{ key.createdAtUtc }}</p> }
            <form (submit)="securityAction($event, 'passkey-revoke')">
              <label for="passkey-id">Identificador de llave a revocar</label><input id="passkey-id" name="factorId" required maxlength="36">
              <label for="passkey-revoke-code">Código MFA para revocar llave</label><input id="passkey-revoke-code" name="code" autocomplete="one-time-code" required maxlength="32" pattern="([0-9]{6}|[0-9a-fA-F]{32})">
              <button [disabled]="busy() || identity.restoring()">Revocar llave de acceso</button>
            </form>
          </details>
          <button type="button" [disabled]="busy() || identity.restoring()" (click)="sessionAction('list')">Consultar sesiones</button>
          <button type="button" [disabled]="busy() || identity.restoring()" (click)="sessionAction('rotate')">Rotar sesión</button>
          <details><summary>Revocar todas mis sesiones</summary>
            <form (submit)="securityAction($event, 'sessions-all')">
              <label for="sessions-all-code">Código MFA para cerrar todas las sesiones</label><input id="sessions-all-code" name="code" autocomplete="one-time-code" required maxlength="32">
              <button [disabled]="busy()">Cerrar todas mis sesiones</button>
            </form>
          </details>
          <details><summary>Aprobar otro navegador</summary>
            <p>Autoriza solo un navegador que tú controles. Compara la huella con la pantalla que inició la solicitud; no apruebes códigos que te envíe otra persona.</p>
            <form (submit)="securityAction($event, 'pairing-approve')">
              <label for="pair-token">Solicitud del navegador nuevo</label><input id="pair-token" name="token" [value]="incomingPairToken" required pattern="[0-9a-fA-F]{64}" autocomplete="off">
              <label for="pair-fingerprint">Huella del navegador nuevo</label><input id="pair-fingerprint" name="fingerprint" [value]="incomingPairFingerprint" required pattern="[0-9A-F]{12}" autocomplete="off">
              <label for="pair-code">Código MFA para aprobar navegador</label><input id="pair-code" name="code" required maxlength="32" autocomplete="one-time-code">
              <label><input name="confirmed" type="checkbox" required> Comparé la huella y autorizo este navegador</label>
              <button [disabled]="busy()">Aprobar vinculación</button>
            </form>
          </details>
          @for (session of identity.sessions(); track session.id) {
            <p>Sesión {{ session.id }} · {{ session.device }} · {{ session.factor }} · última actividad UTC {{ session.lastSeenAtUtc }} · vence {{ session.expiresAtUtc }}</p>
            <button type="button" [disabled]="busy() || identity.restoring()" (click)="revoke(session.id)">Revocar sesión {{ session.id }}</button>
          }
          <form (submit)="securityAction($event, 'private')">
            <label for="private-branch">Sucursal del recurso</label><input id="private-branch" name="branchId" required maxlength="36">
            <label for="private-resource">Identificador del recurso</label><input id="private-resource" name="resourceId" required maxlength="36">
            <button [disabled]="busy() || identity.restoring()">Consultar recurso autorizado</button>
          </form>
          @if (identity.privateReference()) { <p aria-label="Referencia privada">{{ identity.privateReference() }}</p> }
          <details><summary>Cambiar contraseña</summary>
            <form (submit)="securityAction($event, 'password')">
              <label for="current-password">Contraseña actual</label><input id="current-password" name="currentPassword" type="password" autocomplete="current-password" required maxlength="128">
              <label for="new-password">Nueva contraseña</label><input id="new-password" name="newPassword" type="password" autocomplete="new-password" required minlength="15" maxlength="128">
              <label for="password-mfa">Código MFA para contraseña</label><input id="password-mfa" name="code" autocomplete="one-time-code" required maxlength="32" pattern="([0-9]{6}|[0-9a-fA-F]{32})">
              <button [disabled]="busy() || identity.restoring()">Actualizar contraseña</button>
            </form>
          </details>
          <details><summary>Reemplazar segundo factor</summary>
            <p>Requiere tu contraseña y el factor actual o un código de respaldo. Revoca los factores y sesiones anteriores.</p>
            <form (submit)="securityAction($event, 'factor')">
              <label for="factor-password">Contraseña para reemplazar MFA</label><input id="factor-password" name="currentPassword" type="password" autocomplete="current-password" required maxlength="128">
              <label for="factor-code">Código actual o respaldo MFA</label><input id="factor-code" name="code" autocomplete="one-time-code" required maxlength="32" pattern="([0-9]{6}|[0-9a-fA-F]{32})">
              <button [disabled]="busy() || identity.restoring()">Reemplazar factor MFA</button>
            </form>
          </details>
          <details><summary>Recuperar MFA con una llave de acceso</summary>
            <p>Requiere haber entrado con una llave de acceso en los últimos dos minutos y un token del correo verificado. Revoca todos los autenticadores y exige configurar MFA de nuevo.</p>
            <form (submit)="securityAction($event, 'recovery-request')">
              <label for="passkey-recovery-mail">Correo verificado para recuperar MFA</label><input id="passkey-recovery-mail" name="mailbox" type="email" required maxlength="254" autocomplete="email">
              <button [disabled]="busy()">Pedir token para recuperar MFA</button>
            </form>
            <form (submit)="securityAction($event, 'passkey-recovery')">
              <label for="passkey-recovery-token">Token de correo para recuperar MFA</label><input id="passkey-recovery-token" name="token" required pattern="[0-9a-fA-F]{64}" autocomplete="off">
              <label for="passkey-recovery-password">Nueva contraseña tras recuperar MFA</label><input id="passkey-recovery-password" name="newPassword" type="password" required minlength="15" maxlength="128" autocomplete="new-password">
              <button [disabled]="busy()">Recuperar autenticadores con llave</button>
            </form>
          </details>
          <details><summary>Cambiar correo</summary>
            <form (submit)="securityAction($event, 'mailbox-request')">
              <label for="new-mailbox">Nuevo correo</label><input id="new-mailbox" name="mailbox" type="email" autocomplete="email" required maxlength="254">
              <label for="mailbox-mfa">Código MFA para solicitar correo</label><input id="mailbox-mfa" name="code" autocomplete="one-time-code" required maxlength="32" pattern="([0-9]{6}|[0-9a-fA-F]{32})">
              <button [disabled]="busy() || identity.restoring()">Solicitar cambio de correo</button>
            </form>
            <form (submit)="securityAction($event, 'mailbox-complete')">
              <label for="mailbox-token">Token del nuevo correo</label><input id="mailbox-token" name="token" autocomplete="off" required pattern="[0-9a-fA-F]{64}">
              <label for="mailbox-confirm-mfa">Código MFA para confirmar correo</label><input id="mailbox-confirm-mfa" name="code" autocomplete="one-time-code" required maxlength="32" pattern="([0-9]{6}|[0-9a-fA-F]{32})">
              <button [disabled]="busy() || identity.restoring()">Confirmar cambio de correo</button>
            </form>
          </details>
          <details><summary>Revisar membresía con permiso administrativo</summary>
            <p>Esta acción requiere autorización vigente del servidor. No concede permisos al solicitante.</p>
            <form (submit)="securityAction($event, 'membership')">
              <label for="membership-id">Membresía pendiente</label><input id="membership-id" name="membershipId" required maxlength="36">
              <label for="membership-decision">Decisión</label><select id="membership-decision" name="decision"><option value="approve">Aprobar</option><option value="reject">Rechazar</option></select>
              <label for="membership-reason">Motivo</label><input id="membership-reason" name="reason" required maxlength="240">
              <label for="membership-code">Código MFA del aprobador</label><input id="membership-code" name="code" autocomplete="one-time-code" required maxlength="32" pattern="([0-9]{6}|[0-9a-fA-F]{32})">
              <button [disabled]="busy() || identity.restoring()">Registrar decisión</button>
            </form>
          </details>
          <details><summary>Invitar a una cuenta</summary>
            <p>Requiere permiso administrativo. La invitación solo permite aceptar una membresía básica en esta empresa y vence en diez minutos.</p>
            <form (submit)="securityAction($event, 'invitation-issue')">
              <label for="invite-recipient">Correo a invitar</label><input id="invite-recipient" name="mailbox" type="email" required maxlength="254" autocomplete="email">
              <label for="invite-actor-code">Código MFA para invitar</label><input id="invite-actor-code" name="code" required maxlength="32" autocomplete="one-time-code" pattern="([0-9]{6}|[0-9a-fA-F]{32})">
              <button [disabled]="busy() || identity.restoring()">Enviar invitación</button>
            </form>
          </details>
          <button type="button" [disabled]="busy() || identity.restoring()" (click)="logout()">Cerrar sesión</button>
        </section>
      }
      @if (identity.state() === 'anonymous') {
        <section aria-labelledby="recovery-heading"><h2 id="recovery-heading">Recuperar contraseña</h2>
          <p>La recuperación requiere el correo verificado y tu segundo factor. No inicia una sesión.</p>
          <form (submit)="securityAction($event, 'recovery-request')">
            <label for="recovery-mailbox">Correo para recuperación</label><input id="recovery-mailbox" name="mailbox" type="email" autocomplete="email" required maxlength="254">
            <button [disabled]="busy() || identity.restoring()">Solicitar recuperación</button>
          </form>
          <form (submit)="securityAction($event, 'recovery-complete')">
            <label for="recovery-token">Token de recuperación</label><input id="recovery-token" name="token" autocomplete="off" required pattern="[0-9a-fA-F]{64}">
            <label for="recovery-code">Código MFA para recuperación</label><input id="recovery-code" name="code" autocomplete="one-time-code" required maxlength="32" pattern="([0-9]{6}|[0-9a-fA-F]{32})">
            <label for="recovery-password">Contraseña recuperada</label><input id="recovery-password" name="newPassword" type="password" autocomplete="new-password" required minlength="15" maxlength="128">
            <button [disabled]="busy() || identity.restoring()">Restablecer contraseña</button>
          </form>
          <details><summary>Reportar un cambio que no reconozco</summary>
            <p>Usa el código del aviso de seguridad enviado a tu correo verificado. La denuncia revoca las sesiones; no concede acceso ni cambia contraseñas.</p>
            <form (submit)="securityAction($event, 'incident')">
              <label for="incident-token">Código del aviso de seguridad</label><input id="incident-token" name="token" required pattern="[0-9a-fA-F]{64}" autocomplete="off">
              <button [disabled]="busy()">Reportar actividad no reconocida</button>
            </form>
          </details>
          <details><summary>Perdí todos mis métodos de acceso</summary>
            <p>Prueba primero una llave alternativa o tus códigos de respaldo. Si no están disponibles, solicita revisión de identidad. Requiere dos operadores de plataforma distintos, verificación independiente y al menos 24 horas de espera. Nunca se usan preguntas personales.</p>
            <form (submit)="securityAction($event, 'assisted-request')">
              <label for="assisted-mailbox">Correo de la cuenta que necesita asistencia</label><input id="assisted-mailbox" name="mailbox" type="email" required maxlength="254" autocomplete="email">
              <label><input name="unavailable" type="checkbox" required> Mis métodos automáticos seguros no están disponibles</label>
              <button [disabled]="busy()">Solicitar recuperación asistida</button>
            </form>
            @if (identity.assistedReference()) { <p>Referencia de tu solicitud: <output aria-label="Referencia de recuperación asistida">{{ identity.assistedReference() }}</output>. Esta respuesta no confirma la existencia de una cuenta.</p> }
            <form (submit)="securityAction($event, 'assisted-complete')">
              <label for="assisted-case">Referencia del caso aprobado</label><input id="assisted-case" name="caseId" required maxlength="36">
              <label for="assisted-token">Token del canal previo verificado</label><input id="assisted-token" name="token" required pattern="[0-9a-fA-F]{64}" autocomplete="off">
              <label for="assisted-password">Nueva contraseña para la recuperación asistida</label><input id="assisted-password" name="newPassword" type="password" required minlength="15" maxlength="128" autocomplete="new-password">
              <button [disabled]="busy()">Completar caso con el canal previo</button>
            </form>
            <p>Si tampoco tienes el correo, cada operador que verificó tu identidad puede entregarte su parte por separado después del plazo de seguridad. Necesitas las dos.</p>
            <form (submit)="securityAction($event, 'assisted-handoff')">
              <label for="handoff-case">Referencia para entrega por dos operadores</label><input id="handoff-case" name="caseId" required maxlength="36">
              <label for="handoff-a">Parte del primer operador</label><input id="handoff-a" name="shareA" required pattern="[0-9a-fA-F]{64}" autocomplete="off">
              <label for="handoff-b">Parte del segundo operador</label><input id="handoff-b" name="shareB" required pattern="[0-9a-fA-F]{64}" autocomplete="off">
              <label for="handoff-password">Contraseña para recuperación por dos operadores</label><input id="handoff-password" name="newPassword" type="password" required minlength="15" maxlength="128" autocomplete="new-password">
              <button [disabled]="busy()">Completar recuperación con ambas partes</button>
            </form>
          </details>
        </section>
      }
      <cs-platform-identity-panel />
      @if (message()) { <p role="status" aria-live="polite">{{ message() }}</p> }
      @if (error()) { <p role="alert">{{ error() }}</p> }
    </main>
  `,
  styles: [`
    .identity { max-width: 40rem; margin: 2rem auto; padding: 1.5rem; }
    section { margin-block: 2rem; padding-block: 1rem; border-top: 1px solid #a8a8a8; }
    form { display: grid; gap: .75rem; max-width: 28rem; }
    input, select { min-width: 0; max-width: 100%; box-sizing: border-box; }
    img { max-width: 100%; height: auto; }
    output, code { overflow-wrap: anywhere; }
    input { min-height: 2.75rem; padding: .5rem; font: inherit; }
    button { min-height: 2.75rem; padding: .5rem 1rem; font: inherit; }
    :focus-visible { outline: 3px solid #165dff; outline-offset: 2px; }
  `]
})
export class IdentityPortalComponent {
  readonly incomingPairToken = /^#pair=([A-F0-9]{64})\.([A-F0-9]{12})$/.exec(location.hash)?.[1] ?? '';
  readonly incomingPairFingerprint = /^#pair=([A-F0-9]{64})\.([A-F0-9]{12})$/.exec(location.hash)?.[2] ?? '';
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


