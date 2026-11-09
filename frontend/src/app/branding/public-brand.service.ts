import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';
import { BrandLoadState, parsePublishedPlatformBrand } from './public-brand';

// Fixed same-origin GET. Never include scope, tenant, query, auth token or host.
const PUBLIC_BRAND_ENDPOINT = '/api/v1/public/branding/platform';

@Injectable({ providedIn: 'root' })
export class PublicBrandService {
  private readonly http = inject(HttpClient);
  private readonly currentState = signal<BrandLoadState>({ kind: 'loading' });
  private pending = false;

  readonly state = this.currentState.asReadonly();

  async load(): Promise<void> {
    if (this.pending) return;
    this.pending = true;
    this.currentState.set({ kind: 'loading' });
    try {
      const payload = await firstValueFrom(
        this.http.get<unknown>(PUBLIC_BRAND_ENDPOINT).pipe(timeout({ first: 6500 }))
      );
      this.currentState.set({
        kind: 'ready',
        brand: parsePublishedPlatformBrand(payload)
      });
    } catch {
      // Never reuse an old brand, persisted browser storage, tenant selection or demo identity.
      this.currentState.set({ kind: 'unavailable' });
    } finally {
      this.pending = false;
    }
  }
}
