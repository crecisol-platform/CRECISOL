import { ChangeDetectionStrategy, Component, ElementRef, OnDestroy, ViewChild, effect, input, output, signal } from '@angular/core';

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
  private static readonly openInstances = new Set<InstitutionalModalComponent>();
  private static originalOverflow = '';
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
  // A global receipt must not appear in another newly opened dialog.
  readonly localFeedback = signal('');
  readonly localError = signal('');
  @ViewChild('surface', { static: true }) private surface?: ElementRef<HTMLDialogElement>;
  @ViewChild('heading', { static: true }) private heading?: ElementRef<HTMLElement>;
  @ViewChild('scrollBody', { static: true }) private scrollBody?: ElementRef<HTMLElement>;
  private previouslyFocused: HTMLElement | null = null;
  private submittedWhileOpen = false;
  private actionStartedHere = false;

  constructor() {
    effect(() => {
      const busy = this.busy(), feedback = this.feedback(), error = this.error();
      if (!this.surface?.nativeElement.open) return;
      if (busy && this.actionStartedHere) this.submittedWhileOpen = true;
      if (busy || !this.submittedWhileOpen) return;
      this.submittedWhileOpen = false;
      if (error) {
        this.localError.set(error);
        this.localFeedback.set('');
      } else if (feedback) {
        this.localFeedback.set(feedback);
        this.localError.set('');
        if (this.closeOnSuccess()) this.surface.nativeElement.close();
      }
    });
  }

  onDialogSubmit(): void {
    this.actionStartedHere = true;
    this.localFeedback.set('');
    this.localError.set('');
  }

  onDialogClick(event: Event): void {
    const target = event.target;
    if (!(target instanceof Element)) return;
    const button = target.closest('button');
    if (!button || button.classList.contains('cs-modal__cancel') ||
        button.classList.contains('cs-modal__dismiss')) return;
    this.onDialogSubmit();
  }

  open(): void {
    const dialog = this.surface?.nativeElement;
    if (!dialog || dialog.open) return;
    this.previouslyFocused = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    this.localFeedback.set('');
    this.localError.set('');
    this.actionStartedHere = false;
    this.submittedWhileOpen = false;
    dialog.showModal();
    if (InstitutionalModalComponent.openInstances.size === 0)
      InstitutionalModalComponent.originalOverflow = document.body.style.overflow;
    InstitutionalModalComponent.openInstances.add(this);
    document.body.style.overflow = 'hidden';
    if (this.scrollBody) this.scrollBody.nativeElement.scrollTop = 0;
    this.heading?.nativeElement.focus();
  }

  close(): void {
    if (this.busy()) return; // Nunca ocultar una transacción que aún está procesándose.
    this.surface?.nativeElement.close();
  }

  onCancel(event: Event): void {
    if (this.busy()) event.preventDefault();
  }

  onClosed(): void {
    this.releasePageScroll();
    // No retener contraseñas/códigos de formularios cancelados en el DOM.
    this.surface?.nativeElement.querySelectorAll('form').forEach(form => form.reset());
    this.dismissed.emit();
    // Restituir el punto de lectura y navegación a teclado.
    if (this.previouslyFocused?.isConnected) this.previouslyFocused.focus();
    this.previouslyFocused = null;
    this.submittedWhileOpen = false;
    this.actionStartedHere = false;
    this.localFeedback.set('');
    this.localError.set('');
  }

  private releasePageScroll(): void {
    if (!InstitutionalModalComponent.openInstances.delete(this)) return;
    if (InstitutionalModalComponent.openInstances.size === 0)
      document.body.style.overflow = InstitutionalModalComponent.originalOverflow;
  }

  ngOnDestroy(): void {
    const dialog = this.surface?.nativeElement;
    if (dialog?.open) dialog.close();
    this.releasePageScroll();
  }
}
