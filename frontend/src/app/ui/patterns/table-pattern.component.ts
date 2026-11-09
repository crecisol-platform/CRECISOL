import {
  ChangeDetectionStrategy, Component, computed, input, signal, OnChanges, SimpleChanges
} from '@angular/core';
import { validColumns, type TableColumn, type TableRow } from './pattern-contract';

/** Read-only table shell. Host must pass freshly authorized, scope-bound rows.
 * Never fetch, authorize, cache, export or mutate tenant data here.
 */
@Component({
  selector: 'cs-table-pattern',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './table-pattern.component.html',
  styleUrl: './table-pattern.component.css'
})
export class TablePatternComponent implements OnChanges {
  readonly caption = input.required<string>();
  readonly columns = input.required<readonly TableColumn[]>();
  readonly rows = input.required<readonly TableRow[]>();
  readonly scopeKey = input.required<string>();

  readonly filter = signal('');
  readonly sortKey = signal<string | null>(null);
  readonly descending = signal(false);
  readonly valid = computed(() => validColumns(this.columns(), this.caption()) &&
    this.scopeKey().trim().length > 0);
  readonly displayed = computed(() => {
    if (!this.valid()) return [] as readonly TableRow[];
    const columns = this.columns();
    const query = this.filter().normalize('NFC').trim().toLocaleLowerCase('es');
    const data = this.rows().slice(0, 200).filter(row =>
      columns.some(column => (row[column.key] ?? '').toLocaleLowerCase('es').includes(query))
    );
    const key = this.sortKey();
    if (!key || !columns.some(column => column.key === key)) return data;
    const direction = this.descending() ? -1 : 1;
    return data.map((row, index) => ({ row, index }))
      .sort((a, b) => direction * String(a.row[key] ?? '').localeCompare(
        String(b.row[key] ?? ''), 'es', { numeric: true, sensitivity: 'base' }
      ) || a.index - b.index)
      .map(item => item.row);
  });

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['scopeKey'] && !changes['scopeKey'].firstChange) {
      // Changing context removes presentation state; host MUST also refresh
      // its backend-authorized rows, never pass stale rows from another tenant.
      this.filter.set('');
      this.sortKey.set(null);
      this.descending.set(false);
    }
  }

  setFilter(event: Event): void {
    if (!this.valid()) return;
    const control = event.target;
    if (control instanceof HTMLInputElement) this.filter.set(control.value.slice(0, 80));
  }

  sort(column: string): void {
    if (!this.valid() || !this.columns().some(c => c.key === column)) return;
    if (this.sortKey() === column) {
      this.descending.update(value => !value);
    } else {
      this.sortKey.set(column);
      this.descending.set(false);
    }
  }

  ariaSort(column: string): 'ascending' | 'descending' | 'none' {
    return this.sortKey() === column
      ? (this.descending() ? 'descending' : 'ascending') : 'none';
  }
}
