import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import type { InteractionView } from './interaction-state.model';

/** C02-05: rendering only. The host owns live-region role and its data source.
 * The retry output is a UI intent, never a privileged API or a confirmation.
 */
@Component({
  selector: 'cs-interaction-state',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './interaction-state.component.html',
  styleUrl: './interaction-state.component.css'
})
export class InteractionStateComponent {
  readonly view = input.required<InteractionView>();
  readonly retry = output<void>();

  requestRetry(): void {
    const kind = this.view().kind;
    if (kind === 'error' || kind === 'offline') this.retry.emit();
  }
}
