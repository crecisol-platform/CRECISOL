/** The server determines the sliding expiry on each validated request.
 * The browser only clears private UI when that server-issued deadline passes.
 * No browser event can extend authority without a successful API keepalive. */
export class BrowserSessionWindow {
  private expiryTimer: ReturnType<typeof setTimeout> | undefined;
  constructor(private readonly expired: () => void) {}
  establish(expiresAtUtc: string, idleMinutes: number): void {
    const remaining = Date.parse(expiresAtUtc) - Date.now();
    if (!Number.isInteger(idleMinutes) || idleMinutes < 5 || idleMinutes > 120 ||
        !Number.isFinite(remaining) || remaining <= 0 ||
        remaining > idleMinutes * 60_000 + 90_000)
      throw new Error('identity_unavailable');
    this.clear();
    this.expiryTimer = setTimeout(() => this.fire(), remaining);
  }
  clear(): void {
    if (this.expiryTimer) clearTimeout(this.expiryTimer);
    this.expiryTimer = undefined;
  }
  private fire(): void {
    if (!this.expiryTimer) return;
    this.clear();
    this.expired();
  }
}
