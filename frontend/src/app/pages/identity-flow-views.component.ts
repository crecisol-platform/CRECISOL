import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { identityDateLabel, identityEventLabel, identityOutcomeLabel, identityReferenceLabel } from '../identity/identity-presentation';
import { IdentityApiService } from '../identity/identity-api.service';
import { InstitutionalModalComponent } from '../ui/institutional-modal/institutional-modal.component';

@Component({
  selector: 'cs-identity-guest', standalone: true, changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './identity-guest.component.html', styleUrl: './identity-flow.component.css'
})
export class IdentityGuestComponent {
  readonly identity = inject(IdentityApiService);
  readonly passkeyAvailable = typeof window !== 'undefined' && window.isSecureContext &&
    typeof PublicKeyCredential !== 'undefined' &&
    typeof PublicKeyCredential.parseRequestOptionsFromJSON === 'function' &&
    !!navigator.credentials;
  readonly busy = input(false);
  readonly view = input('ingresar');
  readonly loginRequested = output<Event>();
  readonly registerRequested = output<Event>();
  readonly confirmRequested = output<Event>();
  readonly securityActionRequested = output<{ event: Event; action: string }>();
  readonly passkeyLoginRequested = output<void>();
  readonly startPairingRequested = output<void>();
  readonly completePairingRequested = output<void>();
}

@Component({
  selector: 'cs-identity-mfa', standalone: true, changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './identity-mfa.component.html', styleUrl: './identity-flow.component.css'
})
export class IdentityMfaComponent {
  readonly identity = inject(IdentityApiService);
  readonly busy = input(false);
  readonly enrollRequested = output<void>();
  readonly verifyRequested = output<Event>();
}

@Component({
  selector: 'cs-identity-workspace', standalone: true, changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [InstitutionalModalComponent],
  templateUrl: './identity-workspace.component.html', styleUrl: './identity-flow.component.css'
})
export class IdentityWorkspaceComponent {
  readonly identity = inject(IdentityApiService);
  readonly showAllEvents = signal(false);
  readonly securityEventRows = computed(() => this.identity.securityEvents()
    .slice(0, this.showAllEvents() ? 50 : 8)
    .map(event => ({
      ...event,
      dateLabel: identityDateLabel(event.atUtc, this.identity.profile()?.timeZone),
      actionLabel: identityEventLabel(event.action),
      outcomeLabel: identityOutcomeLabel(event.outcome)
    })));
  readonly sessionRows = computed(() => this.identity.sessions().map(session => ({
    ...session,
    lastActivityLabel: identityDateLabel(session.lastSeenAtUtc, this.identity.profile()?.timeZone),
    expiryLabel: identityDateLabel(session.expiresAtUtc, this.identity.profile()?.timeZone),
    referenceLabel: identityReferenceLabel(session.id)
  })));
  readonly passkeyRows = computed(() => this.identity.passkeys().map(key => ({
    ...key,
    registrationLabel: identityDateLabel(key.createdAtUtc, this.identity.profile()?.timeZone),
    referenceLabel: identityReferenceLabel(key.id)
  })));

  readonly feedback = input(''); readonly error = input('');
  readonly view = input('resumen');
  readonly busy = input(false);
  readonly incomingPairToken = input('');
  readonly incomingPairFingerprint = input('');
  readonly securityActionRequested = output<{ event: Event; action: string }>();
  readonly uploadPhotoRequested = output<Event>();
  readonly loadProfileRequested = output<void>();
  readonly removePhotoRequested = output<void>();
  readonly securityEventsRequested = output<void>();
  readonly passkeyListRequested = output<void>();
  readonly sessionRequested = output<'list' | 'rotate'>();
  readonly revokeRequested = output<string>();
  readonly logoutRequested = output<void>();
}

@Component({
  selector: 'cs-identity-recovery', standalone: true, changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './identity-recovery.component.html', styleUrl: './identity-flow.component.css'
})
export class IdentityRecoveryComponent {
  readonly identity = inject(IdentityApiService);
  readonly busy = input(false);
  readonly securityActionRequested = output<{ event: Event; action: string }>();
}
