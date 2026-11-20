import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

/** Native, accessible form surface. Emits submit intent with the original
 * DOM event; the host owns FormData, field validation, CSRF, MFA, authorization
 * and the backend response. No privileged action is performed here.
 */
@Component({
  selector: 'cs-ui-form',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './institutional-form.component.html',
  styleUrl: './institutional-form.component.css'
})
export class InstitutionalFormComponent {
  readonly formId = input.required<string>();
  readonly heading = input.required<string>();
  readonly description = input('');
  readonly busy = input(false);
  readonly validationMode = input<'native' | 'host'>('native');
  readonly submitIntent = output<Event>();

  submit(event: Event): void {
    event.preventDefault();
    if (!this.busy()) this.submitIntent.emit(event);
  }
}
