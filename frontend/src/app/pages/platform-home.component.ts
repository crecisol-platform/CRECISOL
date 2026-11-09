import { DOCUMENT } from '@angular/common';
import {
  ChangeDetectionStrategy, Component, computed, effect, inject
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

    // The image asset itself remains pending verification in C01-06.
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
