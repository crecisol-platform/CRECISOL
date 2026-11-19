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
    try { const result = await this.post<{ csrfToken: string; expiresAtUtc: string }>('restore', {}); this.csrf = result.csrfToken;
      this.sessionWindow.establish(result.expiresAtUtc); this.state.set('authenticated'); }
    catch { this.clear(); } finally { this.restoring.set(false); }
  }
  async login(mailbox: string, password: string): Promise<void> {
    const result = await this.post<{ challenge: string }>('login', { mailbox, password });
    this.challenge = result.challenge; this.state.set('challenge');
  }
  async mfa(code: string): Promise<void> {
    const result = await this.post<{ csrfToken: string; expiresAtUtc: string }>('mfa', { challenge: this.challenge, code });
    if (!/^[A-F0-9]{64}$/i.test(result.csrfToken)) throw new Error('identity_unavailable');
    this.challenge = ''; this.csrf = result.csrfToken; this.sessionWindow.establish(result.expiresAtUtc); this.state.set('authenticated');
  }
  async lookup(caseId: string): Promise<void> {
    this.case.set(null);
    this.case.set(await this.post<{ caseId: string; readyAtUtc: string | null; blocked: boolean; consumed: boolean }>('case', { caseId }));
  }
  async findCase(mailbox: string, realm: string, reason: string, code: string): Promise<void> {
    this.locatedCase.set('');
    this.locatedCase.set((await this.post<{ caseId: string }>('cases/find', { mailbox, realm, reason, code })).caseId);
  }
  async review(caseId: string, decision: string, evidenceHash: string, reason: string, code: string): Promise<void> {
    await this.post('review', { caseId, decision, identityVerified: 'true', evidenceHash, reason, code });
    await this.lookup(caseId);
  }
  async releaseShare(caseId: string, code: string): Promise<void> {
    this.handoffShare.set('');
    const result = await this.post<{ share: string }>('release-share', { caseId, code });
    this.handoffShare.set(result.share);
    if (this.shareTimeout) clearTimeout(this.shareTimeout);
    this.shareTimeout = setTimeout(() => this.handoffShare.set(''), 30 * 60_000);
  }
  async logout(): Promise<void> {
    this.discardFactorSetup();
    this.discardRecoverySetup();
    this.restoring.set(true);
    try { await this.post('logout', {}); } catch { /* Local privacy teardown still completes. */ }
    finally { this.clear(); this.restoring.set(false); }
  }
  async loadProfile(): Promise<void> {
    const profile = await this.post<{ displayName: string; language: string; timeZone: string; hasPhoto: boolean }>('profile/read', {});
    this.profile.set(profile); this.photo.set('');
    if (profile.hasPhoto) {
      const photo = await this.post<{ png: string }>('profile/photo', {});
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
  async loadAbuse(): Promise<void> { this.abuse.set((await this.post<{ signals: Array<{ kind: string; count: number }> }>('abuse-summary', {})).signals); }
  async loadSessions(): Promise<void> { this.sessions.set((await this.post<{ sessions: Array<{ id: string; device: string; lastSeenAtUtc: string; expiresAtUtc: string }> }>('sessions', {})).sessions); }
  async rotateSession(): Promise<void> { const result = await this.post<{ csrfToken: string; expiresAtUtc: string }>('sessions/rotate', {});
    this.csrf = result.csrfToken; this.sessionWindow.establish(result.expiresAtUtc); await this.loadSessions(); }
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
    const result = await this.post<{ recoveryCodes: string[] }>('factor/confirm', { challenge: this.factorChallenge, code });
    this.clear(); this.showBackups(result.recoveryCodes);
  }
  async requestRecovery(mailbox: string): Promise<void> {
    const result = await this.post<{ reference: string }>('operator-recovery/request', { mailbox, automaticMethodsUnavailable: 'true' });
    this.recoveryReference.set(result.reference);
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
    const result = await this.post<{ recoveryCodes: string[] }>('operator-recovery/enroll', { caseId: this.recoveryCase, token: this.recoveryChallenge, code });
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
