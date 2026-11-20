import { AfterViewInit, Directive, ElementRef, OnDestroy, inject } from '@angular/core';

type EditableField = HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement;
type FieldRecord = { node: HTMLElement; previousDescription: string | null; previousInvalid: string | null };

/** Clear ONLY input values with security significance, never server-generated data or the entire form.
 * This runs even after an HTTP failure, preserving safe email/reason fields for correction.
 */
export function clearSensitiveInputValues(form: HTMLFormElement | null | undefined): void {
  if (!form) return;
  for (const input of Array.from(form.querySelectorAll('input'))) {
    const sensitiveName = /(?:password|secret|token|share|proofCode|code|otp|mfa|pin)/i.test(input.name);
    const sensitiveAutocomplete = input.autocomplete === 'one-time-code';
    if (input.disabled || input.readOnly || input.type === 'hidden' || input.type === 'checkbox' ||
        input.type === 'radio' || input.type === 'file') continue;
    if (input.type !== 'password' && !sensitiveName && !sensitiveAutocomplete) continue;
    input.value = '';
    input.dispatchEvent(new Event('input', { bubbles: true }));
  }
}

function fieldValidationCopy(field: EditableField): string {
  const validity = field.validity;
  if (validity.valueMissing) return field instanceof HTMLInputElement && field.type === 'checkbox'
    ? 'Confirma esta opción antes de continuar.' : 'Este campo es obligatorio.';
  if (validity.typeMismatch) return 'Revisa el formato de este campo.';
  if (validity.patternMismatch) return 'El formato no coincide con el requerido.';
  if (validity.tooShort) return 'El valor es demasiado corto.';
  if (validity.tooLong) return 'El valor supera el tamaño permitido.';
  if (validity.rangeUnderflow || validity.rangeOverflow || validity.stepMismatch)
    return 'Elige un valor dentro del intervalo permitido.';
  if (validity.badInput) return 'Introduce un valor válido.';
  return 'Revisa este campo antes de continuar.';
}

/** Delegated native constraint-validation feedback, no credential persistence.
 * Native constraints remain authoritative for submission; this only explains invalid inputs.
 */
@Directive({ selector: 'form[csFieldFeedback]', standalone: true })
export class SecureFieldFeedbackDirective implements AfterViewInit, OnDestroy {
  private static counter = 0;
  private readonly form = inject(ElementRef<HTMLFormElement>).nativeElement;
  private readonly rendered = new Map<EditableField, FieldRecord>();

  private readonly invalid = (event: Event): void => {
    const target = event.target;
    if (!(target instanceof HTMLInputElement || target instanceof HTMLSelectElement ||
          target instanceof HTMLTextAreaElement) || !this.form.contains(target)) return;
    let record = this.rendered.get(target);
    if (!record) {
      const note = this.form.ownerDocument.createElement('small');
      note.className = 'cs-field-feedback';
      note.id = 'cs-field-feedback-' + ++SecureFieldFeedbackDirective.counter;
      note.setAttribute('role', 'alert');
      record = {
        node: note,
        previousDescription: target.getAttribute('aria-describedby'),
        previousInvalid: target.getAttribute('aria-invalid')
      };
      this.rendered.set(target, record);
      target.insertAdjacentElement('afterend', note);
      const current = record.previousDescription?.trim();
      target.setAttribute('aria-describedby', current ? current + ' ' + note.id : note.id);
    }
    record.node.textContent = fieldValidationCopy(target);
    target.setAttribute('aria-invalid', 'true');
  };
  private readonly edited = (event: Event): void => {
    const target = event.target;
    if (!(target instanceof HTMLInputElement || target instanceof HTMLSelectElement ||
          target instanceof HTMLTextAreaElement)) return;
    if (!this.rendered.has(target)) return;
    if (target.validity.valid) this.release(target);
    else this.invalid(event);
  };
  private readonly reset = (): void => { this.clear(); };

  ngAfterViewInit(): void {
    this.form.addEventListener('invalid', this.invalid, true);
    this.form.addEventListener('input', this.edited, true);
    this.form.addEventListener('change', this.edited, true);
    this.form.addEventListener('reset', this.reset);
  }
  private release(field: EditableField): void {
    const record = this.rendered.get(field);
    if (!record) return;
    record.node.remove();
    if (record.previousDescription === null) field.removeAttribute('aria-describedby');
    else field.setAttribute('aria-describedby', record.previousDescription);
    if (record.previousInvalid === null) field.removeAttribute('aria-invalid');
    else field.setAttribute('aria-invalid', record.previousInvalid);
    this.rendered.delete(field);
  }
  private clear(): void {
    for (const field of [...this.rendered.keys()]) this.release(field);
  }
  ngOnDestroy(): void {
    this.form.removeEventListener('invalid', this.invalid, true);
    this.form.removeEventListener('input', this.edited, true);
    this.form.removeEventListener('change', this.edited, true);
    this.form.removeEventListener('reset', this.reset);
    this.clear();
  }
}
