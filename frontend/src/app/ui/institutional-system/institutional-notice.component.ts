import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { InstitutionalIconComponent, type InstitutionalIconName } from './institutional-icon.component';

export type InstitutionalNoticeKind = 'info' | 'success' | 'warning' | 'error';
const NOTICE_ICONS: Readonly<Record<InstitutionalNoticeKind, InstitutionalIconName>> = {
  info: 'info', success: 'check', warning: 'warning', error: 'error'
};

/** A presentation receipt, never proof that the backend accepted an action. */
@Component({
  selector: 'cs-ui-notice',
  standalone: true,
  imports: [InstitutionalIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './institutional-notice.component.html',
  styleUrl: './institutional-notice.component.css'
})
export class InstitutionalNoticeComponent {
  readonly kind = input<InstitutionalNoticeKind>('info');
  readonly heading = input.required<string>();
  readonly message = input.required<string>();
  readonly icon = computed<InstitutionalIconName>(() => NOTICE_ICONS[this.kind()]);
}
