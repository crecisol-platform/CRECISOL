import { DOCUMENT } from '@angular/common';
import {
  ChangeDetectionStrategy, Component, computed, effect, inject, signal
} from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { PublicBrandService } from '../branding/public-brand.service';
import { BrandLoadState } from '../branding/public-brand';

@Component({
  selector: 'cs-platform-home',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './platform-home.component.html',
  styleUrl: './platform-home.component.css'
})
export class PlatformHomeComponent {
  private readonly brandSource = inject(PublicBrandService);
  private readonly meta = inject(Meta);
  private readonly pageTitle = inject(Title);
  private readonly document = inject(DOCUMENT);

  readonly state = this.brandSource.state;
  // A published revision owns both the header icon and the hero image.
  // The static official image is a bootstrap asset, never a fixed identity override.
  readonly headerAssetFailedKey = signal<string | null>(null);
  readonly headerAsset = computed(() => {
    const s = this.state();
    if (s.kind !== 'ready') return null;
    const url = s.brand.presentation?.faviconUrl ?? s.brand.logoUrl;
    return { url, revision: s.brand.revision, key: String(s.brand.revision) + ':' + url };
  });
  // C01-06: display only the published platform's same-origin asset path.
  // Failed assets fall back to the existing illustration; no candidate bytes are embedded.
  readonly logoReadyKey = signal<string | null>(null);
  readonly logoFailedKey = signal<string | null>(null);
  readonly logoKey = computed(() => {
    const s = this.state();
    return s.kind === 'ready' ? String(s.brand.revision) + ':' + s.brand.logoUrl : null;
  });

  readonly categories = computed(() => {
    const state = this.state();
    if (state.kind !== 'ready') return [];
    return state.brand.descriptor
      .split('·')
      .map(item => item.trim())
      .filter(Boolean)
      .slice(0, 6);
  });

  constructor() {
    effect(() => this.applyPublicMetadata(this.state()));
    void this.brandSource.load();
  }

  onHeaderAssetError(url: string, revision: number): void {
    const key = String(revision) + ':' + url;
    if (this.headerAsset()?.key === key) {
      this.headerAssetFailedKey.set(key);
    }
  }

  onLogoLoad(url: string, revision: number): void {
    const key = String(revision) + ':' + url;
    if (this.logoKey() === key) {
      this.logoReadyKey.set(key);
      this.logoFailedKey.set(null);
    }
  }

  onLogoError(url: string, revision: number): void {
    const key = String(revision) + ':' + url;
    if (this.logoKey() === key) {
      this.logoFailedKey.set(key);
      this.logoReadyKey.set(null);
    }
  }

  retry(): void {
    void this.brandSource.load();
  }

  private applyPublicMetadata(state: BrandLoadState): void {
    // Head is reset on EVERY state change; stale published data must not remain.
    const favicon = this.document.head.querySelector(
      'link[rel="icon"][data-crecisol-managed="true"]'
    );
    favicon?.remove();

    if (state.kind !== 'ready') {
      this.pageTitle.setTitle('Identidad pública no disponible');
      this.meta.removeTag('name="description"');
      this.meta.removeTag('name="theme-color"');
      return;
    }

    const brand = state.brand;
    this.pageTitle.setTitle(brand.presentation?.seoTitle ?? brand.name);
    this.meta.updateTag({
      name: 'description',
      content: brand.presentation?.seoDescription ?? brand.descriptor
    });
    this.meta.updateTag({ name: 'theme-color', content: brand.primaryColor });

    // The published favicon is the only icon identity shown in the browser head.
    // No hardcoded default icon can survive a later brand change.
    const faviconPath = brand.presentation?.faviconUrl;
    if (faviconPath) {
      const link = this.document.createElement('link');
      link.setAttribute('rel', 'icon');
      link.setAttribute('type', faviconPath.endsWith('.webp') ? 'image/webp' : 'image/png');
      link.setAttribute('href', faviconPath);
      link.setAttribute('data-crecisol-managed', 'true');
      this.document.head.appendChild(link);
    }
  }
}
