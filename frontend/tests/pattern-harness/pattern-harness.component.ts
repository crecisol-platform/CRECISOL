import { ChangeDetectionStrategy, Component, computed, ElementRef, signal, viewChild } from '@angular/core';
import { DraftFormPatternComponent } from '../../src/app/ui/patterns/draft-form-pattern.component';
import { TablePatternComponent } from '../../src/app/ui/patterns/table-pattern.component';
import type { DraftFieldConfig, TableColumn, TableRow } from '../../src/app/ui/patterns/pattern-contract';

const SAFE_FIELD: DraftFieldConfig = Object.freeze({
  id: 'referencia-ejemplo', label: 'Referencia sintética',
  hint: 'Prueba con un código ficticio. No escribas datos personales.',
  rule: 'reference', required: true, maxLength: 30
});
const UNSAFE_FIELD: DraftFieldConfig = Object.freeze({
  ...SAFE_FIELD, label: '<img src=x onerror=alert(1)>'
});
const COLUMNS: readonly TableColumn[] = Object.freeze([
  { key: 'ref', label: 'Código' },
  { key: 'label', label: 'Descripción' },
  { key: 'status', label: 'Estado' }
]);
const A_ROWS: readonly TableRow[] = Object.freeze([
  { ref: 'A10', label: 'Registro 10', status: 'Listo' },
  { ref: 'A2', label: 'Registro 2', status: 'Pendiente' },
  { ref: 'A3', label: '<svg onload=alert(1)>', status: 'Preparado' }
]);
const B_ROWS: readonly TableRow[] = Object.freeze([
  { ref: 'B1', label: 'Otro ejemplo 1', status: 'Listo' },
  { ref: 'B2', label: 'Otro ejemplo 2', status: 'Pendiente' }
]);

@Component({
  selector: 'cs-pattern-harness',
  standalone: true,
  imports: [DraftFormPatternComponent, TablePatternComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [`
    :host{display:block;min-height:100vh;padding:clamp(18px,4vw,48px);
      font-family:ui-sans-serif,-apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif;
      color:#17314e;background:#eef4fa}
    main{max-width:1060px;margin:0 auto}
    h1{font-size:clamp(1.65rem,4vw,3rem);line-height:1.15;margin:0 0 12px}
    .intro{line-height:1.7;margin:0 0 24px;max-width:70ch;color:#3e5871}
    .testing-controls{display:flex;gap:12px;flex-wrap:wrap;margin-bottom:26px}
    .testing-controls button{min-height:46px;padding:10px 16px;
      border-radius:10px;background:#fff;color:#153a60;border:1px solid #9db3cb;
      font:inherit;cursor:pointer}
    button:focus-visible{outline:3px solid #1473d0;outline-offset:3px}
    /* C02-07: CI-only modal still observes real mobile focus/touch sizing. */
    dialog{width:min(calc(100% - 24px),30rem);max-height:85dvh;overflow:auto;
      background:#fff;color:#17314e;border:1px solid #9db3cb;border-radius:16px;
      padding:clamp(16px,4vw,26px);box-sizing:border-box}
    dialog h2,dialog p{overflow-wrap:anywhere;line-height:1.5}
    dialog button{display:inline-flex;align-items:center;justify-content:center;
      min-height:44px;min-width:44px;max-width:100%;padding:9px 14px;margin:4px 6px 4px 0;
      white-space:normal;text-align:center;border:1px solid #9db3cb;border-radius:10px;
      background:#fff;color:#17314e;font:inherit;font-weight:700;cursor:pointer}
    .patterns{display:grid;grid-template-columns:minmax(0,1fr);gap:24px}
  `],
  template: `
    <main>
      <h1>Patrones de formularios y tablas</h1>
      <p class="intro">Entorno sintético de pruebas C02-06: no forma parte de la aplicación pública, no contiene datos reales y nunca guarda ni envía información.</p>
      <div class="testing-controls">
        <button data-testid="switch-context" type="button" (click)="requestChange('scope')">
          Cambiar conjunto sintético
        </button>
        <button data-testid="invalid-schema" type="button" (click)="requestChange('schema')">
          Alternar configuración inválida
        </button>
      </div>
      <div class="patterns">
        <cs-draft-form-pattern [config]="field()" [scopeKey]="scope()"
          (dirtyChange)="dirty.set($event)"/>
        <cs-table-pattern [caption]="'Tabla sintética de lectura'"
          [columns]="columns" [rows]="rows()" [scopeKey]="scope()"/>
      </div>
      <dialog #changeDialog aria-labelledby="change-title" aria-describedby="change-description">
        <h2 id="change-title">¿Cambiar el ejemplo?</h2>
        <p id="change-description">Hay un borrador sin guardar. Continuar lo descartará por seguridad.</p>
        <button type="button" (click)="cancelChange()">Conservar borrador</button>
        <button type="button" (click)="confirmChange()">Descartar y cambiar</button>
      </dialog>
    </main>
  `
})
export class PatternHarnessComponent {
  private readonly changeDialog = viewChild.required<ElementRef<HTMLDialogElement>>('changeDialog');
  readonly dirty = signal(false);
  private readonly pending = signal<'scope' | 'schema' | null>(null);
  readonly scope = signal<'fixture-a' | 'fixture-b'>('fixture-a');
  readonly invalid = signal(false);
  readonly field = computed(() => this.invalid() ? UNSAFE_FIELD : SAFE_FIELD);
  readonly columns = COLUMNS;
  readonly rows = computed(() => this.scope() === 'fixture-a' ? A_ROWS : B_ROWS);

  requestChange(action: 'scope' | 'schema'): void {
    if (this.dirty()) {
      this.pending.set(action);
      this.changeDialog().nativeElement.showModal();
      return;
    }
    this.applyChange(action);
  }

  cancelChange(): void {
    this.pending.set(null);
    this.changeDialog().nativeElement.close();
  }

  confirmChange(): void {
    const action = this.pending();
    this.pending.set(null);
    this.changeDialog().nativeElement.close();
    if (action) {
      this.dirty.set(false);
      this.applyChange(action);
    }
  }

  private applyChange(action: 'scope' | 'schema'): void {
    if (action === 'scope') {
      this.scope.update(value => value === 'fixture-a' ? 'fixture-b' : 'fixture-a');
    } else {
      this.invalid.update(value => !value);
    }
  }
}
