import { ChangeDetectionStrategy, Component, ElementRef, OnDestroy, ViewChild, computed, effect, inject, input, output, signal } from '@angular/core';
import { OperationFeedbackComponent } from '../feedback/operation-feedback.component';

/**
 * Modal institucional C05. Usa la top layer nativa (<dialog>.showModal):
 * foco atrapado por el navegador, contenido de fondo inerte y Escape estándar.
 * No realiza acciones privilegiadas: cada formulario sigue usando la API existente.
 */
export type InstitutionalModalKind = 'simple' | 'form' | 'destructive' | 'wizard';

@Component({
  selector: 'cs-institutional-modal',
  standalone: true,
  imports: [OperationFeedbackComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './institutional-modal.component.html',
  styleUrl: './institutional-modal.component.css'
})
export class InstitutionalModalComponent implements OnDestroy {
  // Angular input "title" is also a native HTML tooltip attribute on the host.
  // Remove the DOM attribute before presenting the dialog; the input signal and
  // the dialog's accessible title remain unchanged.
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
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
  /** Visual/interaction pattern only. Never authorizes a privileged API request. */
  readonly kind = input<InstitutionalModalKind>('form');
  readonly warning = input('');
  readonly steps = input<readonly string[]>([]);
  readonly step = input(1);
  readonly activeStep = computed(() => {
    const requested = this.step();
    const value = Number.isFinite(requested) ? Math.trunc(requested) : 1;
    return Math.max(1, Math.min(Math.max(1, this.steps().length), value));
  });
  readonly dismissed = output<void>();
  // A global receipt must not appear in another newly opened dialog.
  readonly localFeedback = signal('');
  readonly localError = signal('');
  @ViewChild('surface', { static: true }) private surface?: ElementRef<HTMLDialogElement>;
  @ViewChild('heading', { static: true }) private heading?: ElementRef<HTMLElement>;
  @ViewChild('scrollBody', { static: true }) private scrollBody?: ElementRef<HTMLElement>;
  private previouslyFocused: HTMLElement | null = null;
  private submittedWhileOpen = false;
  readonly actionStartedHere = signal(false);
  readonly hasMoreContent = signal(false);
  private presentedWizardStep = 1;
  private contentResizeObserver: ResizeObserver | undefined;

  constructor() {
    effect(() => {
      const step = this.activeStep(), kind = this.kind();
      if (kind !== 'wizard' || !this.surface?.nativeElement.open ||
          this.presentedWizardStep === step) return;
      this.presentedWizardStep = step;
      // A newly displayed step must not inherit the scroll offset of the
      // previous form. Announce the transition with the dialog heading.
      queueMicrotask(() => {
        if (!this.surface?.nativeElement.open || this.activeStep() !== step) return;
        if (this.scrollBody) this.scrollBody.nativeElement.scrollTop = 0;
        this.heading?.nativeElement.focus({ preventScroll: true });
        requestAnimationFrame(() => this.updateScrollCue());
      });
    });
    effect(() => {
      const busy = this.busy(), feedback = this.feedback(), error = this.error();
      if (!this.surface?.nativeElement.open) return;
      if (busy && this.actionStartedHere()) this.submittedWhileOpen = true;
      if (busy || !this.submittedWhileOpen) return;
      this.submittedWhileOpen = false;
      // A later unrelated request cannot impersonate another modal transaction.
      this.actionStartedHere.set(false);
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

  /** The continuation hint is driven by real overflow, not by the form type. */
  updateScrollCue(): void {
    const region = this.scrollBody?.nativeElement;
    const open = this.surface?.nativeElement.open === true;
    this.hasMoreContent.set(Boolean(open && region &&
      region.scrollHeight - region.scrollTop - region.clientHeight > 12));
  }

  onDialogSubmit(): void {
    this.actionStartedHere.set(true);
    this.localFeedback.set('');
    this.localError.set('');
  }

  onDialogClick(event: Event): void {
    const target = event.target;
    if (!(target instanceof Element)) return;
    const button = target.closest('button');
    if (!button || button.closest('.cs-modal__header, .cs-modal__footer')) return;
    // Only actual form submissions or explicit direct commands may claim
    // the global success receipt. Wizard navigation never impersonates a request.
    if (button.hasAttribute('data-modal-action')) this.onDialogSubmit();
  }

  open(): void {
    const dialog = this.surface?.nativeElement;
    if (!dialog || dialog.open || this.busy()) return;
    // Never layer a second privileged modal over another dialog.
    // Also respect native modal dialogs owned by other system flows (for example
    // a session-expiry notice). Never put a privileged action over their top layer.
    if (InstitutionalModalComponent.openInstances.size !== 0 ||
        document.querySelector('dialog:modal')) return;
    this.host.nativeElement.removeAttribute('title');
    this.previouslyFocused = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    this.localFeedback.set('');
    this.localError.set('');
    this.actionStartedHere.set(false);
    this.submittedWhileOpen = false;
    this.presentedWizardStep = this.activeStep();
    dialog.showModal();
    if (InstitutionalModalComponent.openInstances.size === 0)
      InstitutionalModalComponent.originalOverflow = document.body.style.overflow;
    InstitutionalModalComponent.openInstances.add(this);
    document.body.style.overflow = 'hidden';
    if (this.scrollBody) this.scrollBody.nativeElement.scrollTop = 0;
    this.heading?.nativeElement.focus();
    this.contentResizeObserver?.disconnect();
    if (this.scrollBody) {
      this.contentResizeObserver = new ResizeObserver(() => this.updateScrollCue());
      this.contentResizeObserver.observe(this.scrollBody.nativeElement);
    }
    requestAnimationFrame(() => this.updateScrollCue());
  }

  close(): void {
    if (this.busy()) return; // Nunca ocultar una transacción que aún está procesándose.
    this.surface?.nativeElement.close();
  }

  onCancel(event: Event): void {
    // During a server mutation Escape cannot hide the status nor abandon the
    // protected transaction; the dialog remains focus-trapped until completion.
    if (this.busy()) event.preventDefault();
  }

  onClosed(): void {
    this.contentResizeObserver?.disconnect();
    this.contentResizeObserver = undefined;
    this.hasMoreContent.set(false);
    this.releasePageScroll();
    // No retener contraseñas/códigos de formularios cancelados en el DOM.
    this.surface?.nativeElement.querySelectorAll('form').forEach(form => form.reset());
    this.dismissed.emit();
    // Restituir el punto de lectura y navegación a teclado.
    if (this.previouslyFocused?.isConnected) this.previouslyFocused.focus();
    this.previouslyFocused = null;
    this.submittedWhileOpen = false;
    this.actionStartedHere.set(false);
    this.localFeedback.set('');
    this.localError.set('');
  }

  private releasePageScroll(): void {
    if (!InstitutionalModalComponent.openInstances.delete(this)) return;
    if (InstitutionalModalComponent.openInstances.size === 0)
      document.body.style.overflow = InstitutionalModalComponent.originalOverflow;
  }

  ngOnDestroy(): void {
    this.contentResizeObserver?.disconnect();
    this.contentResizeObserver = undefined;
    const dialog = this.surface?.nativeElement;
    if (dialog?.open) dialog.close();
    this.releasePageScroll();
  }
}
