import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export type InstitutionalIconName =
  | 'check' | 'info' | 'warning' | 'error' | 'clock' | 'arrow-right'
  | 'shield' | 'lock' | 'key' | 'user' | 'search' | 'refresh' | 'inbox';

@Component({
  selector: 'cs-ui-icon',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './institutional-icon.component.html',
  styleUrl: './institutional-icon.component.css'
})
export class InstitutionalIconComponent {
  readonly name = input<InstitutionalIconName>('info');
}
