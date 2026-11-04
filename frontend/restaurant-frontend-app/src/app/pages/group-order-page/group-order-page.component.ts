import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { forkJoin, interval, Subscription } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ParticipantCardComponent } from '@app/components/group-orders/participant-card/participant-card.component';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { GroupStatusResponse } from '@app/models/group-orders/group-status.model';
import { Participant } from '@app/models/group-orders/participant.model';
import { consolidatePreOrder } from '@app/shared/helpers/consolidate-pre-order';
import { ModalComponent } from '@app/shared/custom-components/modal/modal.component';

@Component({
  selector: 'app-group-order-page',
  imports: [CurrencyPipe, DatePipe, ParticipantCardComponent, ModalComponent],
  templateUrl: './group-order-page.component.html',
  styleUrl: './group-order-page.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class GroupOrderPageComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private readonly groupOrdersService = inject(GroupOrdersService);
  private readonly toastService = inject(ToastService);
  private syncSubscription: Subscription | null = null;
  private refreshInFlight = false;

  readonly invite = signal<GuestInvite | null>(null);
  readonly sessions = signal<GroupSessionsResponse | null>(null);
  readonly groupStatus = signal<GroupStatusResponse | null>(null);
  readonly ownerSessionId = signal<string | null>(null);
  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly dialogOpen = signal(false);
  readonly submitting = signal(false);
  readonly submitted = signal(false);
  readonly updatedAt = signal<Date | null>(null);
  readonly refreshError = signal<string | null>(null);
  readonly removeTarget = signal<Participant | null>(null);
  readonly removing = signal(false);

  readonly participants = computed(() => this.sessions()?.participants ?? []);
  readonly consolidatedOrder = computed(() => consolidatePreOrder(this.participants()));
  readonly pendingParticipants = computed(() => this.participants().filter((participant) => !participant.isSubmitted).length);
  readonly maxGuestCount = computed(() => this.invite()?.MaxGuestCount ?? this.groupStatus()?.maxCapacity ?? 0);
  readonly reservationId = this.route.snapshot.paramMap.get('reservationId') ?? '';
  readonly isSubmitted = computed(() => this.submitted() || this.groupStatus()?.status === 'Submitted');

  ngOnInit(): void {
    this.loadGroup();
  }

  loadGroup(): void {
    this.stopSync();
    this.loading.set(true);
    this.errorMessage.set(null);
    this.refreshError.set(null);
    this.invite.set(null);
    this.sessions.set(null);
    this.groupStatus.set(null);
    this.ownerSessionId.set(null);

    if (!this.reservationId) {
      this.loading.set(false);
      this.errorMessage.set('Group reservation not found');
      return;
    }

    this.groupOrdersService.getGroupInvite$(this.reservationId).subscribe({
      next: (invite) => {
        this.invite.set(invite);
        forkJoin({
          sessions: this.groupOrdersService.getSessions$(invite.GroupReservationId),
          ownerSessionId: this.groupOrdersService.getOwnerSessionId$(this.reservationId),
          groupStatus: this.groupOrdersService.getStatus$(invite.GroupReservationId),
        }).subscribe({
          next: ({ sessions, ownerSessionId, groupStatus }) => {
            this.sessions.set(sessions);
            this.ownerSessionId.set(ownerSessionId);
            this.groupStatus.set(groupStatus);
            this.submitted.set(groupStatus.status === 'Submitted');
            this.updatedAt.set(new Date());
            this.loading.set(false);
            this.startSync();
          },
          error: (error: unknown) => this.showLoadError(error),
        });
      },
      error: (error: unknown) => this.showLoadError(error),
    });
  }

  openSubmitDialog(): void {
    if (!this.canSubmit()) {
      return;
    }
    this.dialogOpen.set(true);
  }

  closeSubmitDialog(): void {
    if (!this.submitting()) {
      this.dialogOpen.set(false);
    }
  }

  submitOrder(): void {
    const groupReservationId = this.invite()?.GroupReservationId;
    if (!groupReservationId || !this.canSubmit() || this.submitting()) {
      return;
    }

    this.submitting.set(true);
    this.groupOrdersService.submit$(groupReservationId, this.reservationId).subscribe({
      next: () => {
        this.submitted.set(true);
        this.stopSync();
        this.dialogOpen.set(false);
        this.toastService.success('Pre-order sent to the restaurant');
      },
      error: () => this.submitting.set(false),
      complete: () => this.submitting.set(false),
    });
  }

  canSubmit(): boolean {
    return !this.loading() && !this.isSubmitted() && this.consolidatedOrder().items.length > 0;
  }

  refreshGroup(): void {
    const groupReservationId = this.invite()?.GroupReservationId;
    if (!groupReservationId || this.refreshInFlight || this.isSubmitted()) {
      return;
    }

    this.refreshInFlight = true;
    forkJoin({
      sessions: this.groupOrdersService.getSessions$(groupReservationId, { silent: true }),
      groupStatus: this.groupOrdersService.getStatus$(groupReservationId, { silent: true }),
    }).subscribe({
      next: ({ sessions, groupStatus }) => {
        this.sessions.set(sessions);
        this.groupStatus.set(groupStatus);
        this.updatedAt.set(new Date());
        this.refreshError.set(null);
        this.submitted.set(groupStatus.status === 'Submitted');
        if (groupStatus.status !== 'Open') {
          this.stopSync();
        }
      },
      error: () => {
        const time = this.updatedAt();
        this.refreshError.set(`Couldn't refresh. Showing data from ${time?.toLocaleTimeString() ?? 'the last update'}.`);
        this.refreshInFlight = false;
      },
      complete: () => {
        this.refreshInFlight = false;
      },
    });
  }

  requestRemove(participant: Participant): void {
    if (participant.type !== 'Guest' || this.isSubmitted() || this.removing()) {
      return;
    }
    this.removeTarget.set(participant);
  }

  closeRemoveDialog(): void {
    if (!this.removing()) {
      this.removeTarget.set(null);
    }
  }

  confirmRemove(): void {
    const participant = this.removeTarget();
    const groupReservationId = this.invite()?.GroupReservationId;
    if (!participant || !groupReservationId || this.removing() || this.isSubmitted()) {
      return;
    }

    this.removing.set(true);
    this.groupOrdersService.removeSession$(groupReservationId, participant.id).subscribe({
      next: (sessions) => {
        this.sessions.set(sessions);
        this.removeTarget.set(null);
      },
      error: () => this.removing.set(false),
      complete: () => this.removing.set(false),
    });
  }

  private showLoadError(error: unknown): void {
    this.loading.set(false);
    this.errorMessage.set(error instanceof HttpErrorResponse && error.status === 404
      ? 'Group reservation not found'
      : 'Could not load group reservation');
  }

  private startSync(): void {
    if (this.groupStatus()?.status !== 'Open' || this.isSubmitted() || this.syncSubscription) {
      return;
    }
    this.syncSubscription = interval(15_000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.refreshGroup());
  }

  private stopSync(): void {
    this.syncSubscription?.unsubscribe();
    this.syncSubscription = null;
  }
}
