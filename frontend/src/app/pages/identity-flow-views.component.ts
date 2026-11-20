import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { identityDateLabel, identityEventLabel, identityOutcomeLabel, identityReferenceLabel, identityFactorLabel, identityTimeZoneLabel } from '../identity/identity-presentation';
import { IdentityApiService } from '../identity/identity-api.service';
import { InstitutionalModalComponent } from '../ui/institutional-modal/institutional-modal.component';
import { InstitutionalCardComponent, InstitutionalStateComponent } from '../ui/institutional-system';

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
  imports: [InstitutionalModalComponent, InstitutionalCardComponent, InstitutionalStateComponent],
  templateUrl: './identity-workspace.component.html', styleUrl: './identity-flow.component.css'
})
export class IdentityWorkspaceComponent {
  readonly identity = inject(IdentityApiService);
  readonly showAllEvents = signal(false);
  readonly historyRequested = signal(false);
  readonly sessionsRequested = signal(false);
  readonly securityEventRows = computed(() => [...this.identity.securityEvents()]
    .sort((left, right) => Date.parse(right.atUtc) - Date.parse(left.atUtc))
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
    referenceLabel: identityReferenceLabel(session.id),
    factorLabel: identityFactorLabel(session.factor)
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

  requestHistory(): void {
    if (this.busy()) return;
    this.historyRequested.set(true);
    this.securityEventsRequested.emit();
  }
  requestSessions(): void {
    if (this.busy()) return;
    this.sessionsRequested.set(true);
    this.sessionRequested.emit('list');
  }

  readonly profileTimeZone = computed(() => identityTimeZoneLabel(this.identity.profile()?.timeZone));
  readonly profileInitials = computed(() => {
    const words = (this.identity.profile()?.displayName ?? '').trim().split(/\s+/).filter(Boolean);
    const letters = words.slice(0, 2).map(part => part.charAt(0).toLocaleUpperCase('es-HN')).join('');
    return letters || '•';
  });
  readonly profileLanguage = computed(() => {
    const code = this.identity.profile()?.language;
    return code === 'en' ? 'English' : code === 'es' ? 'Español' : code === 'es-HN' ? 'Español (Honduras)' : 'Sin configurar';
  });
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
