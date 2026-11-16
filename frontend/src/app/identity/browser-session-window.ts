/** Privacy teardown follows the server's fixed deadline and network inactivity.
 * A client timer never extends server authority or stores a session secret. */
export class BrowserSessionWindow {
  private absolute: ReturnType<typeof setTimeout> | undefined;
  private idle: ReturnType<typeof setTimeout> | undefined;
  constructor(private readonly maximum: number, private readonly inactivity: number, private readonly expired: () => void) {}
  establish(expiresAtUtc: string): void {
    const remaining = Date.parse(expiresAtUtc) - Date.now();
    if (!Number.isFinite(remaining) || remaining <= 0 || remaining > this.maximum + 60_000) throw new Error('identity_unavailable');
    this.clear(); this.absolute = setTimeout(() => this.fire(), remaining); this.touch();
  }
  touch(): void {
    if (!this.absolute) return;
    if (this.idle) clearTimeout(this.idle);
    this.idle = setTimeout(() => this.fire(), this.inactivity);
  }
  clear(): void {
    if (this.absolute) clearTimeout(this.absolute); if (this.idle) clearTimeout(this.idle);
    this.absolute = undefined; this.idle = undefined;
  }
  private fire(): void { const active = this.absolute !== undefined; this.clear(); if (active) this.expired(); }
}
