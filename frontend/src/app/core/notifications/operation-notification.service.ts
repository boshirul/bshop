import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, Injectable } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_SNACK_BAR_DATA, MatSnackBar, MatSnackBarRef } from '@angular/material/snack-bar';

export type OperationNotificationTone = 'success' | 'error' | 'warning' | 'loading';

interface OperationNotificationData {
  message: string;
  tone: OperationNotificationTone;
}

@Component({
  selector: 'app-operation-notification',
  imports: [MatButtonModule],
  template: `
    <div class="operation-notification" [class]="'operation-notification operation-notification--' + data.tone">
      <span class="operation-notification__message">{{ data.message }}</span>
      @if (data.tone === 'loading') { <span class="operation-notification__progress" aria-hidden="true"></span> }
      @if (data.tone !== 'loading') { <button matButton type="button" (click)="dismiss()">Dismiss</button> }
    </div>
  `
})
export class OperationNotificationComponent {
  readonly data = inject<OperationNotificationData>(MAT_SNACK_BAR_DATA);
  private readonly reference = inject(MatSnackBarRef<OperationNotificationComponent>);

  dismiss(): void {
    this.reference.dismiss();
  }
}

/** Application-wide, viewport-fixed feedback for completed or failed operations. */
@Injectable({ providedIn: 'root' })
export class OperationNotificationService {
  private readonly snackBar = inject(MatSnackBar);
  private activeKey: string | null = null;
  private activeReference: MatSnackBarRef<OperationNotificationComponent> | null = null;

  success(message: string): void {
    this.open(message, 'success', 5_000, 'polite');
  }

  error(message: string): void {
    this.open(message, 'error', 9_000, 'assertive');
  }

  apiError(error: unknown, fallback: string): string {
    const message = operationErrorMessage(error, fallback);
    this.error(message);
    return message;
  }

  warning(message: string): void {
    this.open(message, 'warning', 7_000, 'polite');
  }

  loading(message: string): MatSnackBarRef<OperationNotificationComponent> {
    return this.open(message, 'loading', undefined, 'polite');
  }

  private open(
    message: string,
    tone: OperationNotificationTone,
    duration: number | undefined,
    politeness: 'assertive' | 'polite'
  ): MatSnackBarRef<OperationNotificationComponent> {
    const normalizedMessage = message.trim();
    const key = `${tone}:${normalizedMessage}`;
    if (this.activeKey === key && this.activeReference) {
      return this.activeReference;
    }

    this.activeReference?.dismiss();
    const reference = this.snackBar.openFromComponent(OperationNotificationComponent, {
      data: { message: normalizedMessage, tone },
      duration,
      horizontalPosition: 'end',
      verticalPosition: 'bottom',
      announcementMessage: normalizedMessage,
      politeness,
      panelClass: ['bshop-operation-notification-panel']
    });
    this.activeKey = key;
    this.activeReference = reference;
    reference.afterDismissed().subscribe(() => {
      if (this.activeReference === reference) {
        this.activeKey = null;
        this.activeReference = null;
      }
    });
    return reference;
  }
}

/** Converts API failures to controlled user-facing text and retains a safe support reference. */
export function operationErrorMessage(error: unknown, fallback: string): string {
  if (!(error instanceof HttpErrorResponse)) {
    return fallback;
  }

  const errors = error.error?.errors;
  const controlled = Array.isArray(errors) && typeof errors[0] === 'string'
    ? errors[0].trim()
    : '';
  const base = controlled || fallback;
  const rawTrace = error.error?.traceId;
  const traceId = typeof rawTrace === 'string' && /^[a-zA-Z0-9._:-]{1,100}$/.test(rawTrace)
    ? rawTrace
    : null;
  return traceId ? `${base} Reference: ${traceId}` : base;
}
