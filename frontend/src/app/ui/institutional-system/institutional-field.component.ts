import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Field shell preserves native label-to-input semantics.
 * Caller must set id on projected input and aria-describedby to
 * controlId + '-hint' and controlId + '-error' when those text nodes exist.
 * Server validation and form submit remain exclusively with the host.
 */
@Component({
  selector: 'cs-ui-field',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './institutional-field.component.html',
  styleUrl: './institutional-field.component.css'
})
export class InstitutionalFieldComponent {
  readonly controlId = input.required<string>();
  readonly label = input.required<string>();
  readonly hint = input('');
  readonly error = input('');
  readonly required = input(false);
}
