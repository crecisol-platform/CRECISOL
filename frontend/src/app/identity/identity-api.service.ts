import { canonicalPhotoPixels } from './canonical-photo-pixels';
import { createTotpQr } from './totp-setup-qr';
import { BrowserSessionWindow } from './browser-session-window';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';

export type IdentityState = 'anonymous' | 'challenge' | 'authenticated';

interface NeutralReceipt { readonly accepted: boolean; }
interface LoginReceipt {
  readonly stepUpRequired: boolean;
  readonly challenge?: string;
  readonly sessionToken?: string;
  readonly enrollmentRequired?: boolean;
}
interface MfaReceipt { readonly csrfToken: string; readonly expiresAtUtc: string; readonly idleMinutes: number; readonly recoveryCodes?: readonly string[]; }
/** Only minimal, tenant-authorized, masked applicant data may reach Angular. */
export interface PendingMembershipView {
  readonly id: string;
  readonly requestedAtUtc: string;
  readonly mailboxHint: string;
}

const API = '/api/v1/identity';

/** Same-origin client. The session lives in a Secure HttpOnly cookie; only a session-bound CSRF proof enters JS memory. */
@Injectable({ providedIn: 'root' })
export class IdentityApiService {
  private readonly http = inject(HttpClient);
  private readonly stateValue = signal<IdentityState>('anonymous');
  private challengeValue: string | null = null;
  private csrfValue: string | null = null;
  // Invalidate responses created under an older session or MFA enrollment.
  private sessionRevision = 0;
  // A later denial or decision invalidates older pending-approval responses.
  private pendingRequestRevision = 0;
  private enrollmentRevision = 0;
  private pairingRevision = 0;
  readonly idleMinutes = signal(30);
  private activityLastSent = 0;
  private activityInFlight = false;

  readonly state = this.stateValue.asReadonly();
  readonly restoring = signal(true);
  // Presentation-only notice after server rejection or local expiry; never extends authority.
  readonly sessionEndedNotice = signal(false);

  constructor() {
    if (typeof document !== 'undefined') {
      for (const event of ['pointerdown', 'keydown', 'touchstart', 'wheel'])
        document.addEventListener(event, (action) => {
          if (action.isTrusted) this.onRealActivity();
        }, { passive: true });
    }
    void this.restore();
  }
  private onRealActivity(): void {
    if (this.state() !== 'authenticated' || this.activityInFlight ||
        Date.now() - this.activityLastSent < 60_000) return;
    this.activityLastSent = Date.now(); this.activityInFlight = true;
    const revision = this.sessionRevision;
    void this.post('sessions/keepalive', {}).catch((error: unknown) => {
      // A late 401 from an older cookie may not close a newer authenticated session.
      if (revision === this.sessionRevision && error instanceof HttpErrorResponse && error.status === 401) {
        this.clear(); this.sessionEndedNotice.set(true);
      }
    }).finally(() => { this.activityInFlight = false; });
  }

  private async restore(): Promise<void> {
    const revision = this.sessionRevision;
    try {
      const result = await firstValueFrom(this.http.post<{ csrfToken: string; expiresAtUtc: string; idleMinutes: number }>(`${API}/browser/restore`, {}, {
        headers: { 'X-Crecisol-Browser': '1' }
      }).pipe(timeout({ first: 8000 })));
      if (revision !== this.sessionRevision) return;
      if (/^[0-9a-f]{64}$/i.test(result.csrfToken)) {
        this.csrfValue = result.csrfToken;
        this.sessionWindow.establish(result.expiresAtUtc, result.idleMinutes);
        this.idleMinutes.set(result.idleMinutes);
        this.activityLastSent = Date.now();
        this.stateValue.set('authenticated');
      }
    } catch { if (revision === this.sessionRevision) this.clear(); }
    finally { this.restoring.set(false); }
  }
  readonly enrollmentRequired = signal(false);
  readonly enrollmentSecret = signal('');
  readonly enrollmentQr = signal('');
  readonly recoveryCodes = signal<readonly string[]>([]);
  readonly sessions = signal<ReadonlyArray<{ id: string; expiresAtUtc: string; device: string; factor: string; lastSeenAtUtc: string }>>([]);
  readonly privateReference = signal('');
  readonly passkeys = signal<ReadonlyArray<{ id: string; createdAtUtc: string }>>([]);
  readonly terms = signal<{ version: string; hash: string; text: string } | null>(null);
  readonly pairing = signal<{ token: string; fingerprint: string; qr: string } | null>(null);
  private pairingKey: CryptoKey | null = null;
  private pairingNonce = '';
  readonly securityEvents = signal<ReadonlyArray<{ action: string; outcome: number; atUtc: string }>>([]);
  readonly pendingMemberships = signal<readonly PendingMembershipView[]>([]);
  readonly pendingNextCursor = signal<string | null>(null);
  readonly pendingLoaded = signal(false);
  readonly pendingDenied = signal(false);
  readonly assistedReference = signal('');
  readonly profile = signal<{ displayName: string; language: string; timeZone: string } | null>(null);
  readonly photo = signal('');
  // A suspended tab must not log out another active tab sharing this HttpOnly cookie.
  // Server expiry/revocation is enforced independently on every API call.
  private readonly sessionWindow = new BrowserSessionWindow(() => {
    this.clear(); this.sessionEndedNotice.set(true);
  });
  private challengeTimer: ReturnType<typeof setTimeout> | undefined;
  private backupCodesTimer: ReturnType<typeof setTimeout> | undefined;

  async requestRegistration(mailbox: string): Promise<void> {
    await firstValueFrom(this.http.post<NeutralReceipt>(`${API}/registration-intents`, {
      mailbox
    }).pipe(timeout({ first: 8000 })));
  }

  async loadTerms(): Promise<void> {
    this.terms.set(await this.post<{ version: string; hash: string; text: string }>('terms', {}, false));
  }

  async startPairing(): Promise<void> {
    const revision = this.sessionRevision;
    const pairingRevision = ++this.pairingRevision;
    // A new attempt replaces any previous QR and its in-memory signing key.
    this.pairingKey = null;
    this.pairingNonce = '';
    this.pairing.set(null);
    const keys = await crypto.subtle.generateKey({ name: 'ECDSA', namedCurve: 'P-256' }, false, ['sign', 'verify']);
    const hex = (bytes: ArrayBuffer): string => Array.from(new Uint8Array(bytes), b => b.toString(16).padStart(2, '0')).join('').toUpperCase();
    const publicKey = hex(await crypto.subtle.exportKey('spki', keys.publicKey));
    const signature = hex(await crypto.subtle.sign({ name: 'ECDSA', hash: 'SHA-256' }, keys.privateKey,
      new TextEncoder().encode('CRECISOL:C05:PAIR:START')));
    if (revision !== this.sessionRevision || pairingRevision !== this.pairingRevision) return;
    const result = await this.post<{ token: string; nonce: string; fingerprint: string }>('pairings/start', { publicKey, signature }, false);
    if (revision !== this.sessionRevision || pairingRevision !== this.pairingRevision) return;
    this.pairingKey = keys.privateKey; this.pairingNonce = result.nonce;
    // CommonJS uses its default export when Angular prebundling is disabled.
    const { default: QRCode } = await import('qrcode');
    const qr = await QRCode.toDataURL(`${location.origin}/cuenta#pair=${result.token}.${result.fingerprint}`, { width: 320, errorCorrectionLevel: 'M' });
    if (revision !== this.sessionRevision || pairingRevision !== this.pairingRevision) return;
    this.pairing.set({ token: result.token, fingerprint: result.fingerprint, qr });
  }

  async completePairing(): Promise<void> {
    const revision = this.sessionRevision;
    const request = this.pairing();
    if (!request || !this.pairingKey) throw new Error('identity_unavailable');
    const signature = Array.from(new Uint8Array(await crypto.subtle.sign({ name: 'ECDSA', hash: 'SHA-256' }, this.pairingKey,
      new TextEncoder().encode(`CRECISOL:C05:PAIR:${request.token}:${this.pairingNonce}`))), b => b.toString(16).padStart(2, '0')).join('');
    if (revision !== this.sessionRevision || this.pairing() !== request) return;
    const result = await this.post<MfaReceipt>('pairings/complete', { token: request.token, signature }, false);
    if (revision !== this.sessionRevision || this.pairing() !== request) return;
    this.pairingKey = null; this.pairingNonce = ''; this.pairing.set(null);
    this.acceptSession(result);
  }

  async approvePairing(token: string, fingerprint: string, code: string): Promise<void> {
    await this.post('pairings/approve', { token, fingerprint, code, confirmed: 'true' });
  }

  async revokeAllSessions(code: string): Promise<void> {
    await this.post('sessions/revoke-all', { code });
    this.clear();
  }

  async loadSecurityEvents(): Promise<void> {
    const revision = this.sessionRevision;
    const result = await this.post<{ events: Array<{ action: string; outcome: number; atUtc: string }> }>('security/events', {});
    if (revision === this.sessionRevision && this.state() === 'authenticated') this.securityEvents.set(result.events);
  }

  async reportIncident(token: string): Promise<void> {
    await this.post('incidents/report', { token }, false);
    this.clear();
  }

  async requestAssisted(mailbox: string): Promise<void> {
    const revision = this.sessionRevision;
    const result = await this.post<{ reference: string }>('assisted/request', { mailbox, automaticMethodsUnavailable: 'true' }, false);
    if (revision === this.sessionRevision) this.assistedReference.set(result.reference);
  }
  async completeAssisted(caseId: string, token: string, newPassword: string): Promise<void> {
    await this.post('assisted/complete', { caseId, token, newPassword }, false); this.clear();
  }
  async completeAssistedHandoff(caseId: string, shareA: string, shareB: string, newPassword: string): Promise<void> {
    await this.post('assisted/handoff-complete', { caseId, shareA, shareB, newPassword }, false); this.clear();
  }
  async recoverWithPasskey(token: string, newPassword: string): Promise<void> {
    await this.post('factor/recover-passkey', { token, newPassword }); this.clear();
  }
  async loadProfile(): Promise<void> {
    const revision = this.sessionRevision;
    const result = await this.post<{ displayName: string; language: string; timeZone: string; hasPhoto: boolean }>('profile/read', {});
    if (revision !== this.sessionRevision || this.state() !== 'authenticated') return;
    this.profile.set(result); this.photo.set('');
    if (result.hasPhoto) {
      const image = await this.post<{ png: string; mime: string; width: number; height: number }>('profile/photo', {});
      if (revision !== this.sessionRevision || this.state() !== 'authenticated') return;
      if (image.mime !== 'image/png' || image.width !== 64 || image.height !== 64 || image.png.length > 32768 || !/^[A-Za-z0-9+/=]+$/.test(image.png))
        throw new Error('identity_unavailable');
      this.photo.set('data:image/png;base64,' + image.png);
    }
  }
  async saveProfile(displayName: string, language: string, timeZone: string): Promise<void> {
    await this.post('profile/save', { displayName, language, timeZone }); await this.loadProfile();
  }
  async uploadPhoto(file: File): Promise<void> {
    await this.post('profile/photo-upload', { pixels: await canonicalPhotoPixels(file) }); await this.loadProfile();
  }

  async removePhoto(): Promise<void> { await this.post('profile/photo-remove', {}); this.photo.set(''); }

  async acceptTerms(): Promise<void> {
    const terms = this.terms();
    if (!terms) throw new Error('identity_unavailable');
    await this.post('terms/accept', { version: terms.version, hash: terms.hash, accepted: 'true' });
  }

  async issueInvitation(mailbox: string, code: string): Promise<void> {
    await this.post('invitations/issue', { mailbox, code });
  }

  async acceptInvitation(mailbox: string, password: string, code: string, token: string): Promise<void> {
    const terms = this.terms();
    if (!terms) throw new Error('identity_unavailable');
    await this.post('invitations/accept', { mailbox, password, code, token,
      version: terms.version, hash: terms.hash, accepted: 'true' }, false);
  }
  async requestMembership(mailbox: string, password: string, code: string, proofId: string, proofCode: string): Promise<void> {
    const terms = this.terms(); if (!terms) throw new Error('identity_unavailable');
    await this.post('memberships/request', { mailbox, password, code, proofId, proofCode,
      version: terms.version, hash: terms.hash, accepted: 'true' }, false);
  }

  async completeRegistration(proofId: string, code: string, mailbox: string, password: string): Promise<void> {
    if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(proofId) ||
        !/^[0-9a-f]{64}$/i.test(code) || mailbox.length > 254 || password.length < 15 || password.length > 128)
      throw new Error('identity_unavailable');
    try {
      await firstValueFrom(this.http.post<NeutralReceipt>(`${API}/registration-completions`, {
        proofId, code, mailbox, password
      }).pipe(timeout({ first: 8000 })));
    } finally {
      password = '';
    }
  }

  async beginLogin(mailbox: string, password: string): Promise<void> {
    const revision = ++this.sessionRevision;
    let receipt: LoginReceipt;
    try {
      receipt = await this.post<LoginReceipt>('login', { mailbox, password }, false);
    } finally {
      password = '';
    }
    if (revision !== this.sessionRevision) return;
    if (!receipt.stepUpRequired || !receipt.challenge || receipt.sessionToken) {
      this.clear();
      throw new Error('identity_unavailable');
    }
    this.challengeValue = receipt.challenge;
    this.enrollmentRequired.set(receipt.enrollmentRequired === true);
    this.stateValue.set('challenge');
    this.challengeTimer = setTimeout(() => this.clear(), 5 * 60_000);
  }

  async verifyMfa(code: string): Promise<void> {
    const revision = this.sessionRevision;
    const challenge = this.challengeValue;
    if (!challenge) throw new Error('identity_unavailable');
    const result = await this.post<MfaReceipt>('mfa/verify', { challenge, code }, false);
    if (revision !== this.sessionRevision || challenge !== this.challengeValue) return;
    this.acceptSession(result);
  }

  private acceptSession(result: MfaReceipt): void {
    if (!result.csrfToken || !/^[0-9a-f]{64}$/i.test(result.csrfToken)) {
      this.clear();
      throw new Error('identity_unavailable');
    }
    this.challengeValue = null;
    if (this.challengeTimer) clearTimeout(this.challengeTimer);
    this.challengeTimer = undefined;
    this.csrfValue = result.csrfToken;
    this.showRecoveryCodes(result.recoveryCodes ?? []);
    this.enrollmentSecret.set('');
    this.enrollmentQr.set('');
    this.enrollmentRequired.set(false);
    this.stateValue.set('authenticated');
    this.sessionWindow.establish(result.expiresAtUtc, result.idleMinutes);
    this.idleMinutes.set(result.idleMinutes);
    this.activityLastSent = Date.now();
  }

  async loginPasskey(): Promise<void> {
    const revision = this.sessionRevision;
    const options = await this.post<{ challenge: string; publicKey: PublicKeyCredentialRequestOptionsJSON }>('passkeys/options', {}, false);
    if (revision !== this.sessionRevision) return;
    const credential = await navigator.credentials.get({
      publicKey: PublicKeyCredential.parseRequestOptionsFromJSON(options.publicKey)
    }) as PublicKeyCredential | null;
    if (revision !== this.sessionRevision) return;
    if (!credential) throw new Error('identity_unavailable');
    const result = await this.post<MfaReceipt>('passkeys/login', {
      challenge: options.challenge, response: JSON.stringify(credential.toJSON())
    }, false);
    if (revision !== this.sessionRevision) return;
    this.acceptSession(result);
  }

  async registerPasskey(code: string): Promise<void> {
    const options = await this.post<{ challenge: string; publicKey: PublicKeyCredentialCreationOptionsJSON }>('passkeys/register-options', { code });
    const credential = await navigator.credentials.create({
      publicKey: PublicKeyCredential.parseCreationOptionsFromJSON(options.publicKey)
    }) as PublicKeyCredential | null;
    if (!credential) throw new Error('identity_unavailable');
    await this.post('passkeys/register', { challenge: options.challenge, response: JSON.stringify(credential.toJSON()) });
    await this.refreshPasskeys();
  }

  async refreshPasskeys(): Promise<void> {
    const revision = this.sessionRevision;
    const result = await this.post<{ passkeys: Array<{ id: string; createdAtUtc: string }> }>('passkeys', {});
    if (revision === this.sessionRevision && this.state() === 'authenticated') this.passkeys.set(result.passkeys);
  }

  async revokePasskey(factorId: string, code: string): Promise<void> {
    await this.post('passkeys/revoke', { factorId, code });
    this.clear();
  }

  async enrollMfa(): Promise<void> {
    const challenge = this.challengeValue;
    if (!challenge || !this.enrollmentRequired() || this.stateValue() !== 'challenge')
      throw new Error('identity_unavailable');
    const revision = ++this.enrollmentRevision;
    this.enrollmentSecret.set('');
    this.enrollmentQr.set('');
    const result = await this.post<{ secret: string; uri: string }>('mfa/enroll', { challenge }, false);
    // A session timeout or logout can invalidate enrollment while the HTTP
    // request or local QR encoder is still running. Never re-display a stale
    // seed after a new session/challenge has replaced this one.
    if (revision !== this.enrollmentRevision || this.challengeValue !== challenge ||
        !this.enrollmentRequired() || this.stateValue() !== 'challenge') return;
    const qr = await createTotpQr(result.secret, result.uri);
    if (revision !== this.enrollmentRevision || this.challengeValue !== challenge ||
        !this.enrollmentRequired() || this.stateValue() !== 'challenge') return;
    this.enrollmentSecret.set(result.secret);
    this.enrollmentQr.set(qr);
  }

  async refreshSessions(): Promise<void> {
    const revision = this.sessionRevision;
    const result = await this.post<{ sessions: Array<{ id: string; expiresAtUtc: string; device: string; factor: string; lastSeenAtUtc: string }> }>('sessions', {});
    if (revision === this.sessionRevision && this.state() === 'authenticated') this.sessions.set(result.sessions);
  }

  async rotateSession(): Promise<void> {
    const revision = this.sessionRevision;
    const result = await this.post<{ csrfToken: string; expiresAtUtc: string; idleMinutes: number }>('sessions/rotate', {});
    if (revision !== this.sessionRevision || this.state() !== 'authenticated') return;
    this.csrfValue = result.csrfToken;
    this.sessionWindow.establish(result.expiresAtUtc, result.idleMinutes);
    this.idleMinutes.set(result.idleMinutes);
    this.activityLastSent = Date.now();
    await this.refreshSessions();
  }

  async revokeSession(sessionId: string): Promise<void> {
    await this.post('sessions/revoke', { sessionId });
    await this.refreshSessions();
  }

  async requestRecovery(mailbox: string): Promise<void> {
    await this.post('recovery/request', { mailbox }, false);
  }

  async completeRecovery(token: string, code: string, newPassword: string): Promise<void> {
    await this.post('recovery/complete', { token, code, newPassword }, false);
    this.clear();
  }

  async changePassword(currentPassword: string, newPassword: string, code: string): Promise<void> {
    await this.post('password/change', { currentPassword, newPassword, code });
    this.clear();
  }

  async replaceFactor(currentPassword: string, code: string): Promise<void> {
    const revision = this.sessionRevision;
    const result = await this.post<LoginReceipt>('factor/replace', { currentPassword, code });
    if (revision !== this.sessionRevision) return;
    this.clear();
    if (!result.challenge || !result.enrollmentRequired) throw new Error('identity_unavailable');
    this.challengeValue = result.challenge;
    this.enrollmentRequired.set(true);
    this.stateValue.set('challenge');
    this.challengeTimer = setTimeout(() => this.clear(), 5 * 60_000);
  }

  async requestMailbox(mailbox: string, code: string): Promise<void> {
    await this.post('mailbox/request', { mailbox, code });
  }

  async completeMailbox(token: string, code: string): Promise<void> {
    await this.post('mailbox/complete', { token, code });
    this.clear();
  }

  async loadPendingMemberships(next = false): Promise<void> {
    const revision = this.sessionRevision;
    const cursor = next ? this.pendingNextCursor() : 'start';
    if (!cursor) return;
    const loadRevision = ++this.pendingRequestRevision;
    let result: { requests: PendingMembershipView[]; nextCursor: string | null };
    try {
      result = await this.post<{ requests: PendingMembershipView[]; nextCursor: string | null }>(
        'memberships/pending', { cursor });
    } catch (error) {
      if (revision === this.sessionRevision && loadRevision === this.pendingRequestRevision) {
        // Never retain applicant identifiers or masked PII after a failed
        // authorization revalidation, session invalidation or network error.
        this.pendingMemberships.set([]);
        this.pendingNextCursor.set(null);
        this.pendingLoaded.set(false);
        this.pendingDenied.set(error instanceof HttpErrorResponse &&
          (error.status === 401 || error.status === 403));
      }
      throw error;
    }
    if (revision !== this.sessionRevision || loadRevision !== this.pendingRequestRevision ||
        this.state() !== 'authenticated') return;
    const incoming = Array.isArray(result.requests) ? result.requests : [];
    if (next) {
      const seen = new Set(this.pendingMemberships().map(item => item.id));
      this.pendingMemberships.update(previous => [
        ...previous, ...incoming.filter(item => !seen.has(item.id))
      ]);
    } else {
      this.pendingMemberships.set(incoming);
    }
    this.pendingNextCursor.set(result.nextCursor || null);
    this.pendingLoaded.set(true);
    this.pendingDenied.set(false);
  }

  async decideMembership(membershipId: string, decision: string, reason: string, code: string): Promise<void> {
    const revision = this.sessionRevision;
    await this.post('memberships/decide', { membershipId, decision, reason, code });
    if (revision !== this.sessionRevision || this.state() !== 'authenticated') return;
    ++this.pendingRequestRevision;
    // Never leave a stale, potentially already decided item actionable.
    this.pendingMemberships.set([]);
    this.pendingNextCursor.set(null);
    this.pendingLoaded.set(false);
    try { await this.loadPendingMemberships(); } catch { /* The approved decision stays committed; reload can be retried. */ }
  }

  async readPrivate(branchId: string, resourceId: string): Promise<void> {
    const revision = this.sessionRevision;
    this.privateReference.set('');
    const result = await this.post<{ reference: string }>('private/read', { branchId, resourceId });
    if (revision === this.sessionRevision && this.state() === 'authenticated') this.privateReference.set(result.reference);
  }

  private async post<T = unknown>(operation: string, body: object, authenticated = true): Promise<T> {
    if (authenticated && !this.csrfValue) throw new Error('identity_unavailable');
    const requestRevision = this.sessionRevision;
    try {
      const response = await firstValueFrom(this.http.post<T>(`${API}/${operation}`, body, {
        headers: this.csrfValue ? { 'X-Crecisol-Browser': '1', 'X-Crecisol-CSRF': this.csrfValue } : { 'X-Crecisol-Browser': '1' },
        observe: 'response'
      }).pipe(timeout({ first: 12_000 })));
      if (requestRevision === this.sessionRevision && authenticated && this.state() === 'authenticated') {
        const expiry = response.headers.get('X-Crecisol-Session-Expires-At');
        const minutes = Number(response.headers.get('X-Crecisol-Idle-Minutes'));
        if (expiry) {
          this.sessionWindow.establish(expiry, minutes);
          this.idleMinutes.set(minutes);
          this.activityLastSent = Date.now();
        }
      }
      return response.body as T;
    } catch (error) {
      // A policy denial of a resource is not evidence that a session expired.
      // Session validation endpoints decide whether local identity is retained.
      if (requestRevision === this.sessionRevision && authenticated && ['sessions', 'sessions/keepalive'].includes(operation) &&
          error instanceof HttpErrorResponse && error.status === 401) {
        this.clear(); this.sessionEndedNotice.set(true);
      }
      throw error;
    }
  }

  async logout(): Promise<void> {
    const token = this.csrfValue;
    this.restoring.set(true);
    this.clear();
    if (token) {
      try {
        await firstValueFrom(this.http.post<void>(`${API}/logout`, {}, {
          headers: { 'X-Crecisol-Browser': '1', 'X-Crecisol-CSRF': token }, keepalive: true
        }).pipe(timeout({ first: 8000 })));
      } catch {
        // Local credentials are discarded even if server revocation is unavailable.
      }
    }
    this.restoring.set(false);
  }

  /** New backup codes must remain available in Resumen until explicitly saved,
   * but never linger after logout/session expiry or the short disclosure window.
   */
  hideRecoveryCodes(): void {
    if (this.backupCodesTimer) clearTimeout(this.backupCodesTimer);
    this.backupCodesTimer = undefined;
    this.recoveryCodes.set([]);
  }
  private showRecoveryCodes(codes: readonly string[]): void {
    this.hideRecoveryCodes();
    if (codes.length === 0) return;
    this.recoveryCodes.set(codes);
    this.backupCodesTimer = setTimeout(() => {
      this.recoveryCodes.set([]);
      this.backupCodesTimer = undefined;
    }, 5 * 60_000);
  }

  clear(): void {
    this.sessionEndedNotice.set(false);
    ++this.sessionRevision;
    ++this.pendingRequestRevision;
    ++this.enrollmentRevision;
    ++this.pairingRevision;
    if (this.challengeTimer) clearTimeout(this.challengeTimer);
    this.challengeTimer = undefined;
    this.sessionWindow.clear();
    this.activityLastSent = 0;
    this.idleMinutes.set(30);
    this.challengeValue = null;
    this.csrfValue = null;
    this.stateValue.set('anonymous');
    this.enrollmentRequired.set(false);
    this.enrollmentSecret.set('');
    this.enrollmentQr.set('');
    this.sessions.set([]);
    this.privateReference.set('');
    this.assistedReference.set('');
    this.hideRecoveryCodes();
    this.passkeys.set([]);
    this.pairingKey = null; this.pairingNonce = ''; this.pairing.set(null);
    this.securityEvents.set([]);
    this.pendingMemberships.set([]);
    this.pendingNextCursor.set(null);
    this.pendingLoaded.set(false);
    this.pendingDenied.set(false);
    this.profile.set(null); this.photo.set('');
  }

  static publicError(error: unknown): string {
    if (error instanceof HttpErrorResponse && error.status === 0)
      return 'El servicio de identidad no está disponible. Inténtalo de nuevo.';
    return 'No se pudo completar la solicitud. Revisa los datos e inténtalo de nuevo.';
  }
}

