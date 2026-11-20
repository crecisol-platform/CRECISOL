import { ChangeDetectionStrategy, Component, computed, signal } from '@angular/core';
import type { PublishedPlatformBrand } from '../../src/app/branding/public-brand';
import {
  InstitutionalActionComponent,
  InstitutionalCardComponent,
  InstitutionalFieldComponent,
  InstitutionalNavigationComponent,
  InstitutionalNoticeComponent,
  InstitutionalStateComponent,
  InstitutionalTableComponent,
  InstitutionalThemeDirective
} from '../../src/app/ui/institutional-system';

const SYNTHETIC_BRAND: PublishedPlatformBrand = {
  revision: 1,
  name: 'CRECISOL',
  slogan: 'Tu negocio, a tu manera.',
  descriptor: 'Comercio · Servicios · Membresías',
  primaryColor: '#0d468b',
  accentColor: '#1878c8',
  logoUrl: '/assets/sintetico.png',
  websiteUrl: null,
  publishedAt: '2026-11-19T00:00:00Z',
  presentation: {
    faviconUrl: '/assets/sintetico.png',
    typographyToken: 'system',
    iconSetToken: 'outline',
    seoTitle: 'CRECISOL',
    seoDescription: 'Interfaz sintética de componentes',
    copyrightNotice: null
  }
};

@Component({
  selector: 'cs-institutional-harness',
  standalone: true,
  imports: [
    InstitutionalThemeDirective, InstitutionalActionComponent, InstitutionalCardComponent,
    InstitutionalFieldComponent, InstitutionalNavigationComponent, InstitutionalNoticeComponent,
    InstitutionalStateComponent, InstitutionalTableComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './institutional-harness.component.html',
  styleUrl: './institutional-harness.component.css'
})
export class InstitutionalHarnessComponent {
  readonly brandMode = signal<'default' | 'humanist' | 'invalid'>('default');
  readonly brand = computed<PublishedPlatformBrand>(() => {
    const kind = this.brandMode();
    if (kind === 'humanist') {
      return {
        ...SYNTHETIC_BRAND, primaryColor: '#174a79', accentColor: '#327ca5',
        presentation: { ...SYNTHETIC_BRAND.presentation!, typographyToken: 'humanist', iconSetToken: 'solid' }
      };
    }
    if (kind === 'invalid') {
      // All values still flow through the real theme directive validation.
      return {
        ...SYNTHETIC_BRAND, primaryColor: 'url(https://invalid.example/image.svg)',
        accentColor: 'red; background-image: url(https://invalid.example/i.svg)',
        presentation: { ...SYNTHETIC_BRAND.presentation!, typographyToken: 'system', iconSetToken: 'outline' }
      };
    }
    return SYNTHETIC_BRAND;
  });

  readonly navigation = Object.freeze([
    { id: 'resumen', label: 'Resumen' },
    { id: 'perfil', label: 'Mi perfil' },
    { id: 'llaves', label: 'Llaves' },
    { id: 'seguridad', label: 'Seguridad' },
    { id: 'aprobaciones', label: 'Aprobaciones' }
  ]);
  readonly current = signal('resumen');
  readonly scope = signal<'empresa-a' | 'empresa-b'>('empresa-a');
  readonly authorizedRowsScope = signal('empresa-a');
  readonly columns = Object.freeze([
    { key: 'device', label: 'Dispositivo' },
    { key: 'factor', label: 'Verificación' },
    { key: 'status', label: 'Estado' }
  ]);
  readonly records = Object.freeze([
    { device: 'Chromium · ejemplo', factor: 'TOTP', status: 'Sesión activa' },
    { device: 'Otro navegador · ejemplo', factor: 'Llave de acceso', status: 'Revocada' },
    { device: '<img src=x onerror=alert(1)>', factor: 'Texto inerte', status: 'Sin ejecución' }
  ]);
  readonly email = signal('');
  readonly emailError = signal('');
  readonly busy = signal(false);
  readonly feedback = signal('Todos los contenidos son ficticios; este entorno no se conecta a ninguna API.');
  readonly state = signal<'empty' | 'loading' | 'error' | 'success'>('empty');

  setEmail(event: Event): void {
    const target = event.target;
    if (target instanceof HTMLInputElement) {
      this.email.set(target.value);
      this.emailError.set('');
    }
  }
  submitLocal(event: Event): void {
    event.preventDefault();
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(this.email())) {
      this.emailError.set('Revisa el formato de este correo de prueba.');
      return;
    }
    this.emailError.set('');
    this.feedback.set('Formato revisado localmente. No se envió ninguna solicitud.');
    this.state.set('success');
  }
  changeScope(): void {
    this.scope.update(v => v === 'empresa-a' ? 'empresa-b' : 'empresa-a');
    // The previously authorized demo records are deliberately NOT rebound.
    // No stale rows should be visible until a new scope-matched result arrives.
  }
  resetScope(): void { this.authorizedRowsScope.set(this.scope()); }
  showAction(): void {
    this.feedback.set('Interacción local registrada. Ningún dato fue modificado.');
  }
}
