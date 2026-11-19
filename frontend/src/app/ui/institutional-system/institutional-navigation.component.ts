import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { safeDisplayText, safeKey } from '../patterns/pattern-contract';

export interface InstitutionalNavigationItem {
  readonly id: string;
  readonly label: string;
  readonly disabled?: boolean;
}

/** Within-workspace navigation. Emits intent, not an authorization decision.
 * Host owns route guard and current realm; never concatenate tenant IDs here.
 */
@Component({
  selector: 'cs-ui-navigation',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './institutional-navigation.component.html',
  styleUrl: './institutional-navigation.component.css'
})
export class InstitutionalNavigationComponent {
  readonly items = input.required<readonly InstitutionalNavigationItem[]>();
  readonly activeId = input.required<string>();
  readonly ariaLabel = input('Navegación de la sección');
  readonly selected = output<string>();
  readonly validItems = computed(() => {
    const items = this.items();
    if (items.length === 0 || items.length > 12 ||
      new Set(items.map(item => item.id)).size !== items.length ||
      !items.every(item => safeKey(item.id) && safeDisplayText(item.label, 60))) return [];
    return items;
  });
  choose(item: InstitutionalNavigationItem): void {
    if (item.disabled || !this.validItems().some(it => it.id === item.id) ||
      item.id === this.activeId()) return;
    this.selected.emit(item.id);
  }
}
