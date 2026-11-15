import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';

export type IdentityState = 'anonymous' | 'challenge' | 'authenticated';

interface NeutralReceipt { readonly accepted: boolean; }
interface LoginReceipt {
  readonly stepUpRequired: boolean;
  readonly challenge?: string;
  readonly sessionToken?: string;
}
interface MfaReceipt { readonly sessionToken: string; }
interface PrivateCaseReceipt { readonly externalKey: string; }

const API = '/api/v1/identity';

/** Same-origin identity client. Session secrets stay in memory only. */
@Injectable({ providedIn: 'root' })
export class IdentityApiService {
  private readonly http = inject(HttpClient);
  private readonly stateValue = signal<IdentityState>('anonymous');
  private challengeValue: string | null = null;
  private bearerValue: string | null = null;

  readonly state = this.stateValue.asReadonly();

  async requestRegistration(mailbox: string): Promise<void> {
    await firstValueFrom(this.http.post<NeutralReceipt>(`${API}/registration-intents`, {
      mailbox
    }).pipe(timeout({ first: 8000 })));
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
    this.stateValue.set('challenge');
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
    this.bearerValue = result.sessionToken;
    this.stateValue.set('authenticated');
  }

  async readPrivateCase(resourceId: string): Promise<string> {
    const token = this.bearerValue;
    if (!token || !/^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(resourceId))
      throw new Error('identity_unavailable');
    const result = await firstValueFrom(this.http.get<PrivateCaseReceipt>(
      `/api/v1/private/cases/${resourceId}`,
      { headers: { Authorization: `Bearer ${token}` } }
    ).pipe(timeout({ first: 8000 })));
    if (typeof result.externalKey !== 'string' || !result.externalKey.trim())
      throw new Error('identity_unavailable');
    return result.externalKey;
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
    this.challengeValue = null;
    this.bearerValue = null;
    this.stateValue.set('anonymous');
  }

  static publicError(error: unknown): string {
    if (error instanceof HttpErrorResponse && error.status === 0)
      return 'El servicio de identidad no está disponible. Inténtalo de nuevo.';
    return 'No se pudo completar la solicitud. Revisa los datos e inténtalo de nuevo.';
  }
}

