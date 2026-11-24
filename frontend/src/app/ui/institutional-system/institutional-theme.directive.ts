import { Directive, computed, input } from '@angular/core';
import type { PublishedPlatformBrand } from '../../branding/public-brand';

/** Presentation-only published-platform theme.
 * Use ONLY with PublicBrandService.state().brand; this never loads a theme,
 * authorizes a tenant, chooses storage, or accepts arbitrary CSS from users.
 * Primary/accent colors are for decoration. Text and action tokens remain AA-safe.
 */
@Directive({
  selector: '[csInstitutionalTheme]',
  standalone: true,
  host: {
    '[attr.data-cs-theme]': '"institutional"',
    '[attr.data-cs-font]': 'typography()',
    '[attr.data-cs-icon]': 'iconStyle()',
    '[style.--cs-brand-primary]': 'decorativePrimary()',
    '[style.--cs-brand-accent]': 'decorativeAccent()'
  }
})
export class InstitutionalThemeDirective {
  readonly brand = input<PublishedPlatformBrand | null>(null, { alias: 'csInstitutionalTheme' });
  readonly decorativePrimary = computed(() => validatedColor(this.brand()?.primaryColor, '#0d468b'));
  readonly decorativeAccent = computed(() => validatedColor(this.brand()?.accentColor, '#1878c8'));
  readonly typography = computed(() => {
    const font = this.brand()?.presentation?.typographyToken;
    return font === 'humanist' || font === 'editorial' ? font : 'system';
  });
  readonly iconStyle = computed(() => this.brand()?.presentation?.iconSetToken === 'solid' ? 'solid' : 'outline');
}
// No CSS syntax, URLs, fallback declarations, non-hex input or layout injection.
export function validatedColor(input: unknown, fallback: string): string {
  return typeof input === 'string' && /^#[0-9a-fA-F]{6}$/.test(input) ? input : fallback;
}
