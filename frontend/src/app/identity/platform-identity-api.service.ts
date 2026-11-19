import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';
import { canonicalPhotoPixels } from './canonical-photo-pixels';
import { BrowserSessionWindow } from './browser-session-window';
import { createTotpQr } from './totp-setup-qr';

/** Closed platform identity realm. It cannot reuse a tenant CSRF proof,
 * session cookie, account, membership or recovery permission. */
@Injectable({ providedIn: 'root' })
export class PlatformIdentityApiService {
  private readonly http = inject(HttpClient);
  private csrf = '';
  private challenge = '';
  private sessionRevision = 0;
  readonly state = signal<'anonymous' | 'challenge' | 'authenticated'>('anonymous');
  readonly restoring = signal(true);
  readonly handoffShare = signal('');
  readonly profile = signal<{ displayName: string; language: string; timeZone: string } | null>(null);
  readonly photo = signal('');
  readonly abuse = signal<ReadonlyArray<{ kind: string; count: number }>>([]);
  readonly recoveryReference = signal('');
  readonly locatedCase = signal('');
  readonly operatorRecoverySeed = signal('');
  readonly operatorRecoveryQr = signal('');
  readonly factorSecret = signal('');
  readonly factorQr = signal('');
  readonly recoveryCodes = signal<readonly string[]>([]);
  readonly sessions = signal<ReadonlyArray<{ id: string; device: string; lastSeenAtUtc: string; expiresAtUtc: string }>>([]);
  private recoveryChallenge = '';
  private recoveryCase = '';
  private factorChallenge = '';
  // Revision gates prevent a cancelled/expired async QR from restoring a secret.
  private factorQrRevision = 0;
  private recoveryQrRevision = 0;
  private backupTimeout: ReturnType<typeof setTimeout> | undefined;
  private factorTimeout: ReturnType<typeof setTimeout> | undefined;
  private recoveryTimeout: ReturnType<typeof setTimeout> | undefined;
  private shareTimeout: ReturnType<typeof setTimeout> | undefined;
  private readonly sessionWindow = new BrowserSessionWindow(15 * 60_000, 5 * 60_000, () => { void this.logout(); });
  readonly case = signal<{ caseId: string; readyAtUtc: string | null; blocked: boolean; consumed: boolean } | null>(null);
  constructor() { void this.restore(); }
  private async restore(): Promise<void> {
    const revision = this.sessionRevision;
    try {
      const result = await this.post<{ csrfToken: string; expiresAtUtc: string }>('restore', {});
      if (revision !== this.sessionRevision) return;
      this.csrf = result.csrfToken;
      this.sessionWindow.establish(result.expiresAtUtc); this.state.set('authenticated');
    } catch { if (revision === this.sessionRevision) this.clear(); }
    finally { this.restoring.set(false); }
  }
  async login(mailbox: string, password: string): Promise<void> {
    const revision = ++this.sessionRevision;
    const result = await this.post<{ challenge: string }>('login', { mailbox, password });
    if (revision !== this.sessionRevision) return;
    this.challenge = result.challenge; this.state.set('challenge');
  }
  async mfa(code: string): Promise<void> {
    const revision = this.sessionRevision, challenge = this.challenge;
    const result = await this.post<{ csrfToken: string; expiresAtUtc: string }>('mfa', { challenge, code });
    if (revision !== this.sessionRevision || challenge !== this.challenge) return;
    if (!/^[A-F0-9]{64}$/i.test(result.csrfToken)) throw new Error('identity_unavailable');
    this.challenge = ''; this.csrf = result.csrfToken; this.sessionWindow.establish(result.expiresAtUtc); this.state.set('authenticated');
  }
  async lookup(caseId: string): Promise<void> {
    const revision = this.sessionRevision;
    this.case.set(null);
    const result = await this.post<{ caseId: string; readyAtUtc: string | null; blocked: boolean; consumed: boolean }>('case', { caseId });
    if (revision === this.sessionRevision) this.case.set(result);
  }
  async findCase(mailbox: string, realm: string, reason: string, code: string): Promise<void> {
    const revision = this.sessionRevision;
    this.locatedCase.set('');
    const result = await this.post<{ caseId: string }>('cases/find', { mailbox, realm, reason, code });
    if (revision === this.sessionRevision) this.locatedCase.set(result.caseId);
  }
  async review(caseId: string, decision: string, evidenceHash: string, reason: string, code: string): Promise<void> {
    await this.post('review', { caseId, decision, identityVerified: 'true', evidenceHash, reason, code });
    await this.lookup(caseId);
  }
  async releaseShare(caseId: string, code: string): Promise<void> {
    const revision = this.sessionRevision;
    this.handoffShare.set('');
    const result = await this.post<{ share: string }>('release-share', { caseId, code });
    if (revision !== this.sessionRevision || this.state() !== 'authenticated') return;
    this.handoffShare.set(result.share);
    if (this.shareTimeout) clearTimeout(this.shareTimeout);
    this.shareTimeout = setTimeout(() => this.handoffShare.set(''), 30 * 60_000);
  }
  async logout(): Promise<void> {
    // Tear down private state before waiting on an untrusted network response.
    const csrf = this.csrf;
    this.restoring.set(true);
    this.clear();
    try {
      await firstValueFrom(this.http.post<void>('/api/v1/platform-identity/logout', {}, {
        headers: csrf ? { 'X-Crecisol-Browser': '1', 'X-Crecisol-CSRF': csrf } : { 'X-Crecisol-Browser': '1' },
        keepalive: true
      }).pipe(timeout({ first: 8000 })));
    } catch { /* Local privacy teardown is already complete. */ }
    finally { this.restoring.set(false); }
  }
  async loadProfile(): Promise<void> {
    const revision = this.sessionRevision;
    const profile = await this.post<{ displayName: string; language: string; timeZone: string; hasPhoto: boolean }>('profile/read', {});
    if (revision !== this.sessionRevision || this.state() !== 'authenticated') return;
    this.profile.set(profile); this.photo.set('');
    if (profile.hasPhoto) {
      const photo = await this.post<{ png: string }>('profile/photo', {});
      if (revision !== this.sessionRevision || this.state() !== 'authenticated') return;
      if (!/^[A-Za-z0-9+/=]+$/.test(photo.png) || photo.png.length > 32768) throw new Error('identity_unavailable');
      this.photo.set('data:image/png;base64,' + photo.png);
    }
  }
  async saveProfile(displayName: string, language: string, timeZone: string): Promise<void> {
    await this.post('profile/save', { displayName, language, timeZone }); await this.loadProfile();
  }
  async uploadPhoto(file: File): Promise<void> {
    await this.post('profile/photo-upload', { pixels: await canonicalPhotoPixels(file) }); await this.loadProfile();
  }
  async loadAbuse(): Promise<void> {
    const revision = this.sessionRevision;
    const result = await this.post<{ signals: Array<{ kind: string; count: number }> }>('abuse-summary', {});
    if (revision === this.sessionRevision && this.state() === 'authenticated') this.abuse.set(result.signals);
  }
  async loadSessions(): Promise<void> {
    const revision = this.sessionRevision;
    const result = await this.post<{ sessions: Array<{ id: string; device: string; lastSeenAtUtc: string; expiresAtUtc: string }> }>('sessions', {});
    if (revision === this.sessionRevision && this.state() === 'authenticated') this.sessions.set(result.sessions);
  }
  async rotateSession(): Promise<void> {
    const revision = this.sessionRevision;
    const result = await this.post<{ csrfToken: string; expiresAtUtc: string }>('sessions/rotate', {});
    if (revision !== this.sessionRevision || this.state() !== 'authenticated') return;
    this.csrf = result.csrfToken; this.sessionWindow.establish(result.expiresAtUtc); await this.loadSessions();
  }
  async revokeSession(sessionId: string): Promise<void> { await this.post('sessions/revoke', { sessionId }); await this.loadSessions(); }
  async revokeAll(code: string): Promise<void> { await this.post('sessions/revoke-all', { code }); this.clear(); }
  async changePassword(currentPassword: string, newPassword: string, code: string): Promise<void> {
    await this.post('password/change', { currentPassword, newPassword, code }); this.clear();
  }
  async requestMailbox(mailbox: string, code: string): Promise<void> { await this.post('mailbox/request', { mailbox, code }); }
  async completeMailbox(token: string, code: string): Promise<void> { await this.post('mailbox/complete', { token, code }); this.clear(); }
  async replaceFactor(currentPassword: string, code: string): Promise<void> {
    const revision = ++this.factorQrRevision;
    const result = await this.post<{ challenge: string; secret: string; uri: string }>('factor/replace', { currentPassword, code });
    if (revision !== this.factorQrRevision || this.state() !== 'authenticated') return;
    const qr = await createTotpQr(result.secret, result.uri);
    if (revision !== this.factorQrRevision || this.state() !== 'authenticated') return;
    this.factorChallenge = result.challenge; this.factorSecret.set(result.secret); this.factorQr.set(qr);
    if (this.factorTimeout) clearTimeout(this.factorTimeout);
    this.factorTimeout = setTimeout(() => { this.discardFactorSetup(); }, 5 * 60_000);
  }
  async confirmFactor(code: string): Promise<void> {
    const revision = this.factorQrRevision, challenge = this.factorChallenge;
    if (!challenge) throw new Error('identity_unavailable');
    const result = await this.post<{ recoveryCodes: string[] }>('factor/confirm', { challenge, code });
    if (revision !== this.factorQrRevision || challenge !== this.factorChallenge) return;
    this.clear(); this.showBackups(result.recoveryCodes);
  }
  async requestRecovery(mailbox: string): Promise<void> {
    const revision = this.sessionRevision;
    const result = await this.post<{ reference: string }>('operator-recovery/request', { mailbox, automaticMethodsUnavailable: 'true' });
    if (revision === this.sessionRevision) this.recoveryReference.set(result.reference);
  }
  async completeRecovery(caseId: string, shareA: string, shareB: string, newPassword: string): Promise<void> {
    const revision = ++this.recoveryQrRevision;
    const result = await this.post<{ challenge: string; secret: string; uri: string }>('operator-recovery/complete', { caseId, shareA, shareB, newPassword });
    if (revision !== this.recoveryQrRevision) return;
    const qr = await createTotpQr(result.secret, result.uri);
    if (revision !== this.recoveryQrRevision) return;
    this.recoveryCase = caseId; this.recoveryChallenge = result.challenge;
    this.operatorRecoverySeed.set(result.secret); this.operatorRecoveryQr.set(qr);
    if (this.recoveryTimeout) clearTimeout(this.recoveryTimeout);
    this.recoveryTimeout = setTimeout(() => { this.discardRecoverySetup(); }, 5 * 60_000);
  }
  async confirmRecovery(code: string): Promise<void> {
    const revision = this.recoveryQrRevision, caseId = this.recoveryCase, token = this.recoveryChallenge;
    if (!caseId || !token) throw new Error('identity_unavailable');
    const result = await this.post<{ recoveryCodes: string[] }>('operator-recovery/enroll', { caseId, token, code });
    if (revision !== this.recoveryQrRevision || token !== this.recoveryChallenge) return;
    this.clear(); this.showBackups(result.recoveryCodes);
  }
  async reportIncident(token: string): Promise<void> { await this.post('incidents/report', { token }); this.clear(); }
  private async post<T = unknown>(route: string, body: object): Promise<T> {
    try {
      const result = await firstValueFrom(this.http.post<T>(`/api/v1/platform-identity/${route}`, body, {
        headers: this.csrf ? { 'X-Crecisol-Browser': '1', 'X-Crecisol-CSRF': this.csrf } : { 'X-Crecisol-Browser': '1' },
        keepalive: route === 'logout'
      }).pipe(timeout({ first: 12_000 })));
      if (this.state() === 'authenticated') this.sessionWindow.touch();
      return result;
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 401 && ['session', 'sessions'].includes(route)) this.clear();
      throw error;
    }
  }
  discardFactorSetup(): void {
    ++this.factorQrRevision;
    if (this.factorTimeout) clearTimeout(this.factorTimeout);
    this.factorTimeout = undefined; this.factorChallenge = '';
    this.factorSecret.set(''); this.factorQr.set('');
  }
  discardRecoverySetup(): void {
    ++this.recoveryQrRevision;
    if (this.recoveryTimeout) clearTimeout(this.recoveryTimeout);
    this.recoveryTimeout = undefined; this.recoveryCase = ''; this.recoveryChallenge = '';
    this.operatorRecoverySeed.set(''); this.operatorRecoveryQr.set('');
  }
  clear(): void {
    ++this.sessionRevision;
    ++this.factorQrRevision;
    ++this.recoveryQrRevision;
    for (const timer of [this.backupTimeout, this.factorTimeout, this.recoveryTimeout, this.shareTimeout]) if (timer) clearTimeout(timer);
    this.backupTimeout = undefined; this.factorTimeout = undefined; this.recoveryTimeout = undefined; this.shareTimeout = undefined;
    this.csrf = ''; this.challenge = ''; this.state.set('anonymous'); this.case.set(null); this.handoffShare.set(''); this.profile.set(null); this.photo.set(''); this.abuse.set([]);
    this.recoveryCase = ''; this.recoveryChallenge = ''; this.factorChallenge = ''; this.factorSecret.set(''); this.factorQr.set(''); this.operatorRecoverySeed.set(''); this.operatorRecoveryQr.set(''); this.recoveryCodes.set([]);
    // A recovery case reference is never kept after logout/session teardown.
    this.recoveryReference.set(''); this.locatedCase.set('');
    this.sessions.set([]);
    this.sessionWindow.clear();
  }
  private showBackups(codes: readonly string[]): void {
    this.recoveryCodes.set(codes);
    this.backupTimeout = setTimeout(() => this.recoveryCodes.set([]), 5 * 60_000);
  }
}
