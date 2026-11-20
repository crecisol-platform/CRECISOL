import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { OperationFeedbackKind } from './operation-feedback.model';

/** Semantic, local status. This component never calls a privileged API or reads browser storage. */
@Component({
  selector: 'cs-operation-feedback', standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './operation-feedback.component.html',
  styleUrl: './operation-feedback.component.css'
})
export class OperationFeedbackComponent {
  readonly kind = input<OperationFeedbackKind>('info');
  readonly text = input.required<string>();
  readonly context = input('Operación segura');
}
