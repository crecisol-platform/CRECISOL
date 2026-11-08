import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { isPublicPlatformBrand, PublicPlatformBrand } from './public-brand.model';

export type BrandLoadState = 'loading' | 'ready' | 'error';

/** One PUBLIC endpoint. No tenant selection from user-controlled URL or headers. */
@Injectable({ providedIn: 'root' })
export class PublicBrandService {
  private readonly http = inject(HttpClient);
  readonly state = signal<BrandLoadState>('loading');
  readonly identity = signal<PublicPlatformBrand | null>(null);

  load(): void {
    this.state.set('loading');
    this.http.get<unknown>('/api/v1/public/branding/platform', {
      headers: { Accept: 'application/json' }
    }).subscribe({
      next: (payload) => {
        if (!isPublicPlatformBrand(payload)) {
          this.identity.set(null);
          this.state.set('error');
          return;
        }
        this.identity.set(payload);
        this.state.set('ready');
      },
      error: () => {
        this.identity.set(null);
        this.state.set('error');
      }
    });
  }
}
