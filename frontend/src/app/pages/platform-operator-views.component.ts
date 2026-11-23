import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { identityDateLabel, identityReferenceLabel, identityTimeZoneLabel, operatorAbuseViews,
  operatorCaseView, operatorDeviceLabel, operatorLanguageLabel } from '../identity/platform-operator-presentation';
import { PlatformIdentityApiService } from '../identity/platform-identity-api.service';
import { InstitutionalModalComponent } from '../ui/institutional-modal/institutional-modal.component';
import { SecureFieldFeedbackDirective } from '../ui/feedback/secure-field-feedback.directive';

type OperatorAction = { event: Event; action: string };

@Component({
  selector: 'cs-platform-operator-sessions', standalone: true, changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [InstitutionalModalComponent, SecureFieldFeedbackDirective],
  templateUrl: './platform-operator-sessions.component.html', styleUrls: ['./identity-flow.component.css', './platform-operator-f04.component.css', './platform-operator-f04-premium.component.css']
})
export class PlatformOperatorSessionsComponent {
  readonly platform = inject(PlatformIdentityApiService);
  readonly busy = input(false);
  readonly feedback = input(''); readonly error = input('');
  readonly actionRequested = output<OperatorAction>();
  readonly loadSessionsRequested = output<void>();
  readonly rotateSessionRequested = output<void>();
  readonly revokeSessionRequested = output<string>();
  readonly sessionFilter = signal<'all' | 'recent' | 'expiring'>('all');
  readonly sessionRows = computed(() => this.platform.sessions().map(session => {
    const expiry = Date.parse(session.expiresAtUtc);
    const latest = Date.parse(session.lastSeenAtUtc);
    return {
      id: session.id,
      device: operatorDeviceLabel(session.device),
      reference: identityReferenceLabel(session.id),
      activity: identityDateLabel(session.lastSeenAtUtc, this.platform.profile()?.timeZone),
      expiry: identityDateLabel(session.expiresAtUtc, this.platform.profile()?.timeZone),
      recent: Number.isFinite(latest) && latest >= Date.now() - 86_400_000,
      expiring: Number.isFinite(expiry) && expiry <= Date.now() + 600_000
    };
  }));
  readonly filteredSessions = computed(() => this.sessionRows().filter(row =>
    this.sessionFilter() === 'all' || (this.sessionFilter() === 'recent' && row.recent) ||
    (this.sessionFilter() === 'expiring' && row.expiring)));
}
@Component({
  selector: 'cs-platform-operator-profile', standalone: true, changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [InstitutionalModalComponent, SecureFieldFeedbackDirective],
  templateUrl: './platform-operator-profile.component.html', styleUrls: ['./identity-flow.component.css', './platform-operator-f04.component.css', './platform-operator-f04-premium.component.css']
})
export class PlatformOperatorProfileComponent {
  readonly platform = inject(PlatformIdentityApiService);
  readonly busy = input(false);
  readonly feedback = input(''); readonly error = input('');
  readonly actionRequested = output<OperatorAction>();
  readonly loadProfileRequested = output<void>();
  readonly photoRequested = output<Event>();
  readonly photoFileName = signal('');
  readonly photoFileError = signal('');
  onPhotoFileChange(event: Event): void {
    const control = event.target;
    const file = control instanceof HTMLInputElement ? control.files?.item(0) : null;
    this.photoFileName.set(file?.name ?? '');
    this.photoFileError.set(!file ? ''
      : file.size > 2 * 1024 * 1024
        ? 'La imagen supera 2 MB. Elige un archivo más pequeño.'
        : !['image/png', 'image/jpeg', 'image/webp'].includes(file.type)
          ? 'Selecciona una imagen PNG, JPEG o WebP.'
          : '');
  }
  readonly profileName = computed(() => this.platform.profile()?.displayName.trim() || 'Nombre no configurado');
  readonly profileLanguage = computed(() => operatorLanguageLabel(this.platform.profile()?.language ?? ''));
  readonly profileZone = computed(() => identityTimeZoneLabel(this.platform.profile()?.timeZone));
}
@Component({
  selector: 'cs-platform-operator-monitor', standalone: true, changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './platform-operator-monitor.component.html', styleUrls: ['./identity-flow.component.css', './platform-operator-f04.component.css', './platform-operator-f04-premium.component.css']
})
export class PlatformOperatorMonitorComponent {
  readonly platform = inject(PlatformIdentityApiService);
  readonly busy = input(false);
  readonly feedback = input(''); readonly error = input('');
  readonly loadAbuseRequested = output<void>();
  readonly priorityFilter = signal<'all' | 'alta' | 'revision' | 'informativa'>('all');
  readonly signals = computed(() => operatorAbuseViews(this.platform.abuse()));
  readonly filteredSignals = computed(() => this.signals().filter(item =>
    this.priorityFilter() === 'all' || item.priority === this.priorityFilter()));
  readonly totalSignals = computed(() => this.signals().reduce((sum, item) => sum + item.count, 0));
}
@Component({
  selector: 'cs-platform-operator-security', standalone: true, changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [InstitutionalModalComponent, SecureFieldFeedbackDirective],
  templateUrl: './platform-operator-security.component.html', styleUrls: ['./identity-flow.component.css', './platform-operator-f04.component.css', './platform-operator-f04-premium.component.css']
})
export class PlatformOperatorSecurityComponent {
  readonly platform = inject(PlatformIdentityApiService);
  readonly busy = input(false);
  readonly feedback = input(''); readonly error = input('');
  readonly actionRequested = output<OperatorAction>();
}
@Component({
  selector: 'cs-platform-operator-cases', standalone: true, changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [InstitutionalModalComponent, SecureFieldFeedbackDirective],
  templateUrl: './platform-operator-cases.component.html', styleUrls: ['./identity-flow.component.css', './platform-operator-f04.component.css', './platform-operator-f04-premium.component.css']
})
export class PlatformOperatorCasesComponent {
  readonly platform = inject(PlatformIdentityApiService);
  readonly busy = input(false);
  readonly feedback = input(''); readonly error = input('');
  readonly actionRequested = output<OperatorAction>();
  readonly caseFilter = signal<'all' | 'pending' | 'blocked' | 'closed'>('all');
  readonly currentCase = computed(() => operatorCaseView(this.platform.case()));
  readonly visibleCase = computed(() => {
    const record = this.platform.case(), selection = this.caseFilter();
    if (!record) return null;
    if (selection === 'blocked' && !record.blocked) return null;
    if (selection === 'closed' && !record.consumed) return null;
    if (selection === 'pending' && (record.blocked || record.consumed)) return null;
    return this.currentCase();
  });
}
