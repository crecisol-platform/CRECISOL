import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { InstitutionalIconComponent, type InstitutionalIconName } from './institutional-icon.component';
import { InstitutionalActionComponent } from './institutional-action.component';

export type InstitutionalStateKind = 'empty' | 'loading' | 'error' | 'success';
const STATE_ICONS: Readonly<Record<InstitutionalStateKind, InstitutionalIconName>> = {
  empty: 'inbox', loading: 'clock', error: 'error', success: 'check'
};

/** Universal empty/loading/error/success shell: a state does not grant access. */
@Component({
  selector: 'cs-ui-state',
  standalone: true,
  imports: [InstitutionalIconComponent, InstitutionalActionComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './institutional-state.component.html',
  styleUrl: './institutional-state.component.css'
})
export class InstitutionalStateComponent {
  readonly kind = input<InstitutionalStateKind>('empty');
  readonly heading = input.required<string>();
  readonly message = input.required<string>();
  readonly actionLabel = input('');
  readonly action = output<void>();
  readonly icon = computed<InstitutionalIconName>(() => STATE_ICONS[this.kind()]);

  requestAction(): void {
    if (this.kind() !== 'loading' && this.actionLabel()) this.action.emit();
  }
}
