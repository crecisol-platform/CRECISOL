import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { identityDateLabel, identityReferenceLabel } from '../identity/identity-presentation';
import { PlatformIdentityApiService } from '../identity/platform-identity-api.service';
import { InstitutionalModalComponent } from '../ui/institutional-modal/institutional-modal.component';

type OperatorAction = { event: Event; action: string };

@Component({
  selector: 'cs-platform-operator-sessions', standalone: true, changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [InstitutionalModalComponent],
  templateUrl: './platform-operator-sessions.component.html', styleUrl: './identity-flow.component.css'
})
export class PlatformOperatorSessionsComponent {
  readonly platform = inject(PlatformIdentityApiService);
  readonly sessionRows = computed(() => this.platform.sessions().map(session => ({
    ...session,
    lastActivityLabel: identityDateLabel(session.lastSeenAtUtc, this.platform.profile()?.timeZone),
    expiryLabel: identityDateLabel(session.expiresAtUtc, this.platform.profile()?.timeZone),
    referenceLabel: identityReferenceLabel(session.id)
  })));
  readonly busy = input(false);
  readonly feedback = input(''); readonly error = input('');
  readonly actionRequested = output<OperatorAction>();
  readonly loadSessionsRequested = output<void>();
  readonly rotateSessionRequested = output<void>();
  readonly revokeSessionRequested = output<string>();
}
@Component({
  selector: 'cs-platform-operator-profile', standalone: true, changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [InstitutionalModalComponent],
  templateUrl: './platform-operator-profile.component.html', styleUrl: './identity-flow.component.css'
})
export class PlatformOperatorProfileComponent {
  readonly platform = inject(PlatformIdentityApiService);
  readonly busy = input(false);
  readonly feedback = input(''); readonly error = input('');
  readonly actionRequested = output<OperatorAction>();
  readonly loadProfileRequested = output<void>();
  readonly photoRequested = output<Event>();
}
@Component({
  selector: 'cs-platform-operator-monitor', standalone: true, changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './platform-operator-monitor.component.html', styleUrl: './identity-flow.component.css'
})
export class PlatformOperatorMonitorComponent {
  readonly platform = inject(PlatformIdentityApiService);
  readonly busy = input(false);
  readonly feedback = input(''); readonly error = input('');
  readonly loadAbuseRequested = output<void>();
}
@Component({
  selector: 'cs-platform-operator-security', standalone: true, changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [InstitutionalModalComponent],
  templateUrl: './platform-operator-security.component.html', styleUrl: './identity-flow.component.css'
})
export class PlatformOperatorSecurityComponent {
  readonly platform = inject(PlatformIdentityApiService);
  readonly busy = input(false);
  readonly feedback = input(''); readonly error = input('');
  readonly actionRequested = output<OperatorAction>();
}
@Component({
  selector: 'cs-platform-operator-cases', standalone: true, changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [InstitutionalModalComponent],
  templateUrl: './platform-operator-cases.component.html', styleUrl: './identity-flow.component.css'
})
export class PlatformOperatorCasesComponent {
  readonly platform = inject(PlatformIdentityApiService);
  readonly busy = input(false);
  readonly feedback = input(''); readonly error = input('');
  readonly actionRequested = output<OperatorAction>();
}
