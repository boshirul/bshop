import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Subject } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  OperationNotificationService,
  operationErrorMessage
} from './operation-notification.service';

describe('OperationNotificationService', () => {
  function setup() {
    const dismissed = new Subject<void>();
    const reference = {
      dismiss: vi.fn(),
      afterDismissed: () => dismissed.asObservable()
    };
    const snackBar = { openFromComponent: vi.fn(() => reference) };
    TestBed.configureTestingModule({ providers: [{ provide: MatSnackBar, useValue: snackBar }] });
    return { service: TestBed.inject(OperationNotificationService), snackBar, reference, dismissed };
  }

  it('opens feedback in the viewport overlay with accessible announcements', () => {
    const { service, snackBar } = setup();
    service.success('Saved.');

    expect(snackBar.openFromComponent).toHaveBeenCalledOnce();
    const options = (snackBar.openFromComponent.mock.calls as unknown[][])[0][1];
    expect(options).toMatchObject({
      horizontalPosition: 'end',
      verticalPosition: 'bottom',
      announcementMessage: 'Saved.',
      politeness: 'polite',
      panelClass: ['bshop-operation-notification-panel']
    });
  });

  it('suppresses duplicate feedback while the same notification is active', () => {
    const { service, snackBar, dismissed } = setup();
    service.error('Could not save.');
    service.error('Could not save.');
    expect(snackBar.openFromComponent).toHaveBeenCalledOnce();

    dismissed.next();
    service.error('Could not save.');
    expect(snackBar.openFromComponent).toHaveBeenCalledTimes(2);
  });

  it('replaces processing feedback with the confirmed result', () => {
    const { service, snackBar, reference } = setup();
    service.loading('Saving…');
    service.success('Saved.');
    expect(reference.dismiss).toHaveBeenCalledOnce();
    expect(snackBar.openFromComponent).toHaveBeenCalledTimes(2);
  });

  it('keeps controlled validation and a safe trace reference', () => {
    const error = new HttpErrorResponse({
      status: 409,
      error: { errors: ['The record already exists.'], traceId: '00-abcd:1234' }
    });
    expect(operationErrorMessage(error, 'Save failed.'))
      .toBe('The record already exists. Reference: 00-abcd:1234');
  });

  it('does not expose an unsafe trace value or unexpected server detail', () => {
    const error = new HttpErrorResponse({
      status: 500,
      error: { detail: 'password=secret', traceId: '<script>alert(1)</script>' }
    });
    expect(operationErrorMessage(error, 'The operation could not be completed.'))
      .toBe('The operation could not be completed.');
  });
});
