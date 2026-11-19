import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { InstitutionalIconComponent, type InstitutionalIconName } from './institutional-icon.component';

/** One semantic button for the full C05 system. No network or authorization. */
@Component({
  selector: 'cs-ui-action',
  standalone: true,
  imports: [InstitutionalIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './institutional-action.component.html',
  styleUrl: './institutional-action.component.css'
})
export class InstitutionalActionComponent {
  readonly label = input.required<string>();
  readonly variant = input<'primary' | 'secondary' | 'quiet' | 'danger'>('primary');
  readonly type = input<'button' | 'submit' | 'reset'>('button');
  readonly icon = input<InstitutionalIconName | null>(null);
  readonly disabled = input(false);
  readonly busy = input(false);
  readonly busyLabel = input('Procesando');
  readonly activated = output<void>();

  activate(): void {
    if (this.busy() || this.disabled()) return;
    this.activated.emit();
  }
}
