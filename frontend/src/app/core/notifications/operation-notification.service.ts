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

  success(message: string): void {
    this.open(message, 'success', 5_000, 'polite');
  }

  error(message: string): void {
    this.open(message, 'error', 9_000, 'assertive');
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
    this.snackBar.dismiss();
    return this.snackBar.openFromComponent(OperationNotificationComponent, {
      data: { message, tone },
      duration,
      horizontalPosition: 'end',
      verticalPosition: 'bottom',
      announcementMessage: message,
      politeness,
      panelClass: ['bshop-operation-notification-panel']
    });
  }
}
