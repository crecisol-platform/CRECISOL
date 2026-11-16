import { ChangeDetectionStrategy, Component, ElementRef, OnDestroy, ViewChild, input } from '@angular/core';

/**
 * Modal institucional C05. Usa la top layer nativa (<dialog>.showModal):
 * foco atrapado por el navegador, contenido de fondo inerte y Escape estándar.
 * No realiza acciones privilegiadas: cada formulario sigue usando la API existente.
 */
@Component({
  selector: 'cs-institutional-modal',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './institutional-modal.component.html',
  styleUrl: './institutional-modal.component.css'
})
export class InstitutionalModalComponent implements OnDestroy {
  private static sequence = 0;
  private readonly instanceId = ++InstitutionalModalComponent.sequence;
  readonly titleId = `cs-modal-title-${this.instanceId}`;
  readonly descriptionId = `cs-modal-description-${this.instanceId}`;
  readonly title = input.required<string>();
  readonly description = input('');
  readonly busy = input(false);
  readonly feedback = input('');
  readonly error = input('');
  readonly wide = input(false);
  @ViewChild('surface', { static: true }) private surface?: ElementRef<HTMLDialogElement>;
  private previouslyFocused: HTMLElement | null = null;

  open(): void {
    const dialog = this.surface?.nativeElement;
    if (!dialog || dialog.open) return;
    this.previouslyFocused = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    dialog.showModal();
  }

  close(): void {
    if (this.busy()) return; // Nunca ocultar una transacción que aún está procesándose.
    this.surface?.nativeElement.close();
  }

  onCancel(event: Event): void {
    if (this.busy()) event.preventDefault();
  }

  onClosed(): void {
    // Restituir el punto de lectura y navegación a teclado.
    if (this.previouslyFocused?.isConnected) this.previouslyFocused.focus();
    this.previouslyFocused = null;
  }

  ngOnDestroy(): void {
    const dialog = this.surface?.nativeElement;
    if (dialog?.open) dialog.close();
  }
}
