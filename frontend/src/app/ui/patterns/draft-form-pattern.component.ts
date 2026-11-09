import { DOCUMENT } from '@angular/common';
import {
  ChangeDetectionStrategy, Component, computed, DestroyRef, ElementRef,
  inject, input, output, signal, viewChild, OnChanges, SimpleChanges
} from '@angular/core';
import { draftIssue, validDraftConfig, type DraftFieldConfig } from './pattern-contract';

/** Local draft pattern: no persistence, network, session or write acknowledgement.
 * Real products must add backend validation + unsaved-route guard separately.
 */
@Component({
  selector: 'cs-draft-form-pattern',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './draft-form-pattern.component.html',
  styleUrl: './draft-form-pattern.component.css'
})
export class DraftFormPatternComponent implements OnChanges {
  readonly config = input.required<DraftFieldConfig>();
  readonly scopeKey = input.required<string>();
  /** Host decides whether navigation can proceed before changing scope. */
  readonly dirtyChange = output<boolean>();
  readonly safeConfig = computed(() => validDraftConfig(this.config()));
  readonly draft = signal('');
  readonly touched = signal(false);
  readonly locallyChecked = signal(false);
  readonly issue = computed(() => draftIssue(this.draft(), this.config()));
  readonly dirty = computed(() => this.draft().length > 0);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('discardDialog');
  private readonly document = inject(DOCUMENT);
  private readonly destroy = inject(DestroyRef);

  constructor() {
    const browser = this.document.defaultView;
    const guard = (event: BeforeUnloadEvent) => {
      if (this.dirty()) {
        event.preventDefault();
        event.returnValue = '';
      }
    };
    browser?.addEventListener('beforeunload', guard);
    this.destroy.onDestroy(() => browser?.removeEventListener('beforeunload', guard));
  }

  ngOnChanges(changes: SimpleChanges): void {
    if ((changes['scopeKey'] && !changes['scopeKey'].firstChange) ||
        (changes['config'] && !changes['config'].firstChange)) {
      // Isolation is stronger than retaining a previous tenant draft; future
      // host must confirm navigation BEFORE changing an authorized context.
      this.draft.set('');
      this.touched.set(false);
      this.locallyChecked.set(false);
    }
  }

  edit(event: Event): void {
    if (!this.safeConfig()) return;
    const control = event.target;
    if (!(control instanceof HTMLInputElement)) return;
    this.draft.set(control.value);
    this.dirtyChange.emit(this.dirty());
    this.touched.set(false);
    this.locallyChecked.set(false);
  }

  checkLocal(event: Event): void {
    event.preventDefault();
    if (!this.safeConfig()) return;
    this.touched.set(true);
    this.locallyChecked.set(true);
  }

  askDiscard(): void {
    if (this.dirty()) this.dialog().nativeElement.showModal();
  }

  cancelDiscard(): void {
    this.dialog().nativeElement.close();
  }

  discard(): void {
    this.draft.set('');
    this.dirtyChange.emit(false);
    this.touched.set(false);
    this.locallyChecked.set(false);
    this.dialog().nativeElement.close();
  }
}
