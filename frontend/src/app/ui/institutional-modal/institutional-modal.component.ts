import { ChangeDetectionStrategy, Component, ElementRef, OnDestroy, ViewChild, effect, input, output } from '@angular/core';

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
  readonly closeOnSuccess = input(false);
  readonly dismissed = output<void>();
  @ViewChild('surface', { static: true }) private surface?: ElementRef<HTMLDialogElement>;
  private previouslyFocused: HTMLElement | null = null;
  private submittedWhileOpen = false;

  constructor() {
    effect(() => {
      const busy = this.busy(), feedback = this.feedback(), error = this.error();
      if (!this.closeOnSuccess() || !this.surface?.nativeElement.open) return;
      if (busy) this.submittedWhileOpen = true;
      else if (this.submittedWhileOpen && feedback && !error) {
        this.submittedWhileOpen = false;
        this.surface.nativeElement.close();
      }
    });
  }

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
    // No retener contraseñas/códigos de formularios cancelados en el DOM.
    this.surface?.nativeElement.querySelectorAll('form').forEach(form => form.reset());
    this.dismissed.emit();
    // Restituir el punto de lectura y navegación a teclado.
    if (this.previouslyFocused?.isConnected) this.previouslyFocused.focus();
    this.previouslyFocused = null;
    this.submittedWhileOpen = false;
  }

  ngOnDestroy(): void {
    const dialog = this.surface?.nativeElement;
    if (dialog?.open) dialog.close();
  }
}
