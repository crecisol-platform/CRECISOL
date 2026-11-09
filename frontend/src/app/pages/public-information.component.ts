import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, effect, inject, signal } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { PublicBrandService } from '../branding/public-brand.service';
import type { BrandLoadState } from '../branding/public-brand';
import { InteractionStateComponent } from '../ui/interaction-state.component';
import { publicBrandFallback } from '../ui/interaction-state.model';
import {
  PUBLIC_INFO_NAV, PUBLIC_INFO_PAGES,
  type PublicInfoId, type PublicInfoPage
} from './public-information.model';

@Component({
  selector: 'cs-public-information',
  standalone: true,
  imports: [RouterLink, InteractionStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './public-information.component.html',
  styleUrl: './public-information.component.css'
})
export class PublicInformationComponent {
  private readonly brandSource = inject(PublicBrandService);
  private readonly route = inject(ActivatedRoute);
  private readonly meta = inject(Meta);
  private readonly title = inject(Title);
  private readonly document = inject(DOCUMENT);
  private readonly destroy = inject(DestroyRef);
  // Connectivity is a presentation hint only; it cannot authorize a response.
  private readonly online = signal(this.document.defaultView?.navigator.onLine ?? true);
  private readonly updateOnline = () => {
    this.online.set(this.document.defaultView?.navigator.onLine ?? true);
  };

  readonly state = this.brandSource.state;
  readonly nav = PUBLIC_INFO_NAV;
  readonly loadingView = publicBrandFallback('loading', true);
  readonly failedView = computed(() => publicBrandFallback('unavailable', this.online()));
  readonly page: PublicInfoPage;

  constructor() {
    const pageId: unknown = this.route.snapshot.data['publicPage'];
    if (pageId !== 'experiencias' && pageId !== 'demostraciones' &&
        pageId !== 'alta-empresas') {
      // Never route unknown or tenant-provided values to a public/privileged page.
      throw new Error('Invalid platform public information route.');
    }
    this.page = PUBLIC_INFO_PAGES[pageId as PublicInfoId];
    const browser = this.document.defaultView;
    browser?.addEventListener('online', this.updateOnline);
    browser?.addEventListener('offline', this.updateOnline);
    this.destroy.onDestroy(() => {
      browser?.removeEventListener('online', this.updateOnline);
      browser?.removeEventListener('offline', this.updateOnline);
    });
    effect(() => this.applyPublicHead(this.state()));
    void this.brandSource.load();
  }

  retry(): void {
    void this.brandSource.load();
  }

  private applyPublicHead(state: BrandLoadState): void {
    // Clear on every state transition: never show stale favicon, colors or
    // metadata after changing routes or after an invalid publication response.
    this.document.head.querySelector(
      'link[rel="icon"][data-crecisol-managed="true"]'
    )?.remove();

    if (state.kind !== 'ready') {
      this.title.setTitle('Identidad pública no disponible');
      this.meta.removeTag('name="description"');
      this.meta.removeTag('name="theme-color"');
      return;
    }

    const brand = state.brand;
    this.title.setTitle(this.page.title + ' · ' + brand.name);
    this.meta.updateTag({
      name: 'description',
      content: brand.presentation?.seoDescription ?? brand.descriptor
    });
    this.meta.updateTag({ name: 'theme-color', content: brand.primaryColor });

    const faviconPath = brand.presentation?.faviconUrl;
    if (faviconPath) {
      // The server-issued path is validated by parsePublishedPlatformBrand.
      const icon = this.document.createElement('link');
      icon.setAttribute('rel', 'icon');
      icon.setAttribute('type', faviconPath.endsWith('.webp') ? 'image/webp' : 'image/png');
      icon.setAttribute('href', faviconPath);
      icon.setAttribute('data-crecisol-managed', 'true');
      this.document.head.appendChild(icon);
    }
  }
}
