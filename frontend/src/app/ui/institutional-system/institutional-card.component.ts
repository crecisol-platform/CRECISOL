import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Structural card with a caller-assigned stable heading ID.
 * Content and actions are projected: no implicit permission or data loading.
 */
@Component({
  selector: 'cs-ui-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './institutional-card.component.html',
  styleUrl: './institutional-card.component.css'
})
export class InstitutionalCardComponent {
  readonly headingId = input.required<string>();
  readonly heading = input.required<string>();
  readonly description = input('');
  readonly eyebrow = input('');
  readonly headingLevel = input<'h2' | 'h3'>('h2');
  readonly tone = input<'base' | 'soft' | 'accent'>('base');
}
