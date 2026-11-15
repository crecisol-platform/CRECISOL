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
interface MfaReceipt { readonly sessionToken: string; readonly recoveryCodes?: readonly string[]; }

const API = '/api/v1/identity';

/** Same-origin identity client. Session secrets stay in memory only. */
@Injectable({ providedIn: 'root' })
export class IdentityApiService {
  private readonly http = inject(HttpClient);
  private readonly stateValue = signal<IdentityState>('anonymous');
  private challengeValue: string | null = null;
  private bearerValue: string | null = null;

  readonly state = this.stateValue.asReadonly();
  readonly enrollmentRequired = signal(false);
  readonly enrollmentSecret = signal('');
  readonly recoveryCodes = signal<readonly string[]>([]);
  readonly sessions = signal<ReadonlyArray<{ id: string; expiresAtUtc: string }>>([]);
  readonly privateReference = signal('');
  private expiryTimer: ReturnType<typeof setTimeout> | undefined;
  private challengeTimer: ReturnType<typeof setTimeout> | undefined;

  async requestRegistration(mailbox: string): Promise<void> {
    await firstValueFrom(this.http.post<NeutralReceipt>(`${API}/registration-intents`, {
      mailbox
    }).pipe(timeout({ first: 8000 })));
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
    let receipt: LoginReceipt;
    try {
      receipt = await firstValueFrom(this.http.post<LoginReceipt>(`${API}/login`, {
        mailbox, password
      }).pipe(timeout({ first: 8000 })));
    } finally {
      password = '';
    }
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
    const challenge = this.challengeValue;
    if (!challenge) throw new Error('identity_unavailable');
    const result = await firstValueFrom(this.http.post<MfaReceipt>(`${API}/mfa/verify`, {
      challenge, code
    }).pipe(timeout({ first: 8000 })));
    if (!result.sessionToken || result.sessionToken.length > 128) {
      this.clear();
      throw new Error('identity_unavailable');
    }
    this.challengeValue = null;
    if (this.challengeTimer) clearTimeout(this.challengeTimer);
    this.challengeTimer = undefined;
    this.bearerValue = result.sessionToken;
    this.recoveryCodes.set(result.recoveryCodes ?? []);
    this.enrollmentSecret.set('');
    this.enrollmentRequired.set(false);
    this.stateValue.set('authenticated');
    this.expiryTimer = setTimeout(() => this.clear(), 30 * 60_000);
  }

  async enrollMfa(): Promise<void> {
    if (!this.challengeValue || !this.enrollmentRequired()) throw new Error('identity_unavailable');
    const result = await this.post<{ secret: string }>('mfa/enroll', { challenge: this.challengeValue }, false);
    this.enrollmentSecret.set(result.secret);
  }

  async refreshSessions(): Promise<void> {
    const result = await this.post<{ sessions: Array<{ id: string; expiresAtUtc: string }> }>('sessions', {});
    this.sessions.set(result.sessions);
  }

  async rotateSession(): Promise<void> {
    const result = await this.post<{ sessionToken: string }>('sessions/rotate', {});
    this.bearerValue = result.sessionToken;
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
    const result = await this.post<LoginReceipt>('factor/replace', { currentPassword, code });
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

  async decideMembership(membershipId: string, decision: string, reason: string, code: string): Promise<void> {
    await this.post('memberships/decide', { membershipId, decision, reason, code });
  }

  async readPrivate(branchId: string, resourceId: string): Promise<void> {
    this.privateReference.set('');
    const result = await this.post<{ reference: string }>('private/read', { branchId, resourceId });
    this.privateReference.set(result.reference);
  }

  private async post<T = unknown>(operation: string, body: object, authenticated = true): Promise<T> {
    if (authenticated && !this.bearerValue) throw new Error('identity_unavailable');
    try {
      return await firstValueFrom(this.http.post<T>(`${API}/${operation}`, body, {
        headers: authenticated ? { Authorization: `Bearer ${this.bearerValue}` } : {}
      }).pipe(timeout({ first: 12_000 })));
    } catch (error) {
      // A policy denial of a resource is not evidence that a session expired.
      // Session validation endpoints decide whether local identity is retained.
      if (authenticated && operation === 'sessions' && error instanceof HttpErrorResponse && error.status === 401)
        this.clear();
      throw error;
    }
  }

  async logout(): Promise<void> {
    const token = this.bearerValue;
    this.clear();
    if (token) {
      try {
        await firstValueFrom(this.http.post<void>(`${API}/logout`, {}, {
          headers: { Authorization: `Bearer ${token}` }
        }).pipe(timeout({ first: 8000 })));
      } catch {
        // Local credentials are discarded even if server revocation is unavailable.
      }
    }
  }

  clear(): void {
    if (this.challengeTimer) clearTimeout(this.challengeTimer);
    this.challengeTimer = undefined;
    if (this.expiryTimer) clearTimeout(this.expiryTimer);
    this.expiryTimer = undefined;
    this.challengeValue = null;
    this.bearerValue = null;
    this.stateValue.set('anonymous');
    this.enrollmentRequired.set(false);
    this.enrollmentSecret.set('');
    this.sessions.set([]);
    this.privateReference.set('');
    this.recoveryCodes.set([]);
  }

  static publicError(error: unknown): string {
    if (error instanceof HttpErrorResponse && error.status === 0)
      return 'El servicio de identidad no está disponible. Inténtalo de nuevo.';
    return 'No se pudo completar la solicitud. Revisa los datos e inténtalo de nuevo.';
  }
}

