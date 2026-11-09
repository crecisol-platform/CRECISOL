import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'cs-not-found',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="not-found" id="contenido" tabindex="-1">
      <p class="eyebrow">Navegación</p>
      <h1>Página no disponible</h1>
      <p>Esta ruta no forma parte de la identidad pública.</p>
      <a href="/" class="back-link">Volver al inicio</a>
    </main>
  `,
  styles: [`
    :host { display: grid; min-height: 100svh; place-items: center; background: #f5f8fd; color: #172944; }
    .not-found { width: min(90vw, 32rem); padding: 3rem; border: 1px solid #e2e8f1;
      border-radius: 1.5rem; background: white; box-shadow: 0 12px 35px #12325b0b; }
    .eyebrow { text-transform: uppercase; letter-spacing: .15em; font-size: .72rem; font-weight: 800; }
    h1 { font-size: clamp(1.9rem, 5vw, 2.6rem); letter-spacing: -.045em; }
    p { line-height: 1.6; }
    .back-link { display: inline-flex; min-height: 44px; align-items: center; color: #064e91;
      font-weight: 750; text-underline-offset: .2em; }
    .back-link:focus-visible { outline: 3px solid #1769b5; outline-offset: 5px; }
  `]
})
export class NotFoundComponent {}
