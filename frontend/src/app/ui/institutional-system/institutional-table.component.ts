import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { validColumns, type TableColumn, type TableRow } from '../patterns/pattern-contract';

/** Read-only, display-ready rows. Host passes only backend-authorized data.
 * rowsScopeKey must match current scopeKey to prevent stale-tenant presentation
 * after switching realms; this is NOT a replacement for backend authorization.
 * Text interpolation escapes HTML: no innerHTML, exports, fetch or storage.
 */
@Component({
  selector: 'cs-ui-table',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './institutional-table.component.html',
  styleUrl: './institutional-table.component.css'
})
export class InstitutionalTableComponent {
  readonly caption = input.required<string>();
  readonly columns = input.required<readonly TableColumn[]>();
  readonly rows = input.required<readonly TableRow[]>();
  readonly scopeKey = input.required<string>();
  readonly rowsScopeKey = input.required<string>();
  readonly emptyMessage = input('No hay registros disponibles.');
  readonly configurationValid = computed(() => validColumns(this.columns(), this.caption()));
  readonly contextValid = computed(() => this.scopeKey().trim().length > 0 &&
    this.scopeKey() === this.rowsScopeKey());
  readonly visibleRows = computed(() => this.configurationValid() && this.contextValid()
    ? this.rows().slice(0, 200) : ([] as readonly TableRow[]));
}
