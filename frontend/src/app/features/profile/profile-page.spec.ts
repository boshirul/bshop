import '@angular/compiler';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { EnvironmentInjector, runInInjectionContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { Router } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { API_BASE_URL } from '../../core/http/api-base-url.token';
import { OperationNotificationService } from '../../core/notifications/operation-notification.service';
import { passwordsMatchValidator } from './password-validation';
import { ProfilePage } from './profile-page';

describe('passwordsMatchValidator', () => {
  const form = (newPassword: string, confirmation: string) => new FormGroup({
    newPassword: new FormControl(newPassword),
    confirmNewPassword: new FormControl(confirmation)
  }, { validators: passwordsMatchValidator });

  it('marks only non-empty mismatched passwords invalid', () => {
    expect(form('Password1!', 'Different1!').hasError('passwordMismatch')).toBe(true);
    expect(form('Password1!', 'Password1!').hasError('passwordMismatch')).toBe(false);
    expect(form('', '').hasError('passwordMismatch')).toBe(false);
  });
});

describe('ProfilePage photo flow', () => {
  afterEach(() => { TestBed.resetTestingModule(); vi.unstubAllGlobals(); });

  function createPage(dialogResult: File | undefined, postResult: 'success' | 'failure' = 'success') {
    const post = postResult === 'success'
      ? vi.fn((_url: string, _body: unknown) => of({ succeeded: true, data: '/api/auth/profile/photo/content', errors: [] }))
      : vi.fn((_url: string, _body: unknown) => throwError(() => new HttpErrorResponse({ status: 400, error: { errors: ['Upload failed.'] } })));
    const get = vi.fn(() => of(new Blob(['server-avatar'], { type: 'image/webp' })));
    const open = vi.fn(() => ({ afterClosed: () => of(dialogResult) }));
    const notifications = { error: vi.fn(), success: vi.fn(), loading: vi.fn(() => ({ dismiss: vi.fn() })) };
    const auth = { markAvatarChanged: vi.fn(), clearSession: vi.fn() };
    TestBed.configureTestingModule({ providers: [
      { provide: HttpClient, useValue: { post, get } },
      { provide: API_BASE_URL, useValue: '/api' },
      { provide: AuthService, useValue: auth },
      { provide: Router, useValue: { navigate: vi.fn() } },
      { provide: OperationNotificationService, useValue: notifications },
      { provide: MatDialog, useValue: { open } }
    ] });
    const injector = TestBed.inject(EnvironmentInjector);
    const page = runInInjectionContext(injector, () => new ProfilePage());
    return { page, post, get, open, notifications, auth };
  }

  function selection(file: File): Event {
    return { target: { files: [file], value: 'C:\\fakepath\\avatar.jpg' } } as unknown as Event;
  }

  it('opens the editor for a valid image and uploads only after save confirmation', () => {
    const cropped = new File(['cropped'], 'profile-photo.webp', { type: 'image/webp' });
    const fixture = createPage(cropped);
    vi.stubGlobal('URL', { createObjectURL: vi.fn(() => 'blob:server'), revokeObjectURL: vi.fn() });
    (fixture.page as any).selectPhoto(selection(new File(['original'], 'avatar.jpg', { type: 'image/jpeg' })));
    expect(fixture.open).toHaveBeenCalledTimes(1);
    expect(fixture.post).toHaveBeenCalledTimes(1);
    const body = fixture.post.mock.calls[0][1] as FormData;
    expect(body.get('file')).toBe(cropped);
  });

  it('cancels without uploading', () => {
    const fixture = createPage(undefined);
    (fixture.page as any).selectPhoto(selection(new File(['original'], 'avatar.jpg', { type: 'image/jpeg' })));
    expect(fixture.open).toHaveBeenCalledTimes(1);
    expect(fixture.post).not.toHaveBeenCalled();
  });

  it('preserves the existing avatar when upload fails', () => {
    const cropped = new File(['cropped'], 'profile-photo.webp', { type: 'image/webp' });
    const fixture = createPage(cropped, 'failure');
    (fixture.page as any).avatar.set('blob:existing');
    (fixture.page as any).selectPhoto(selection(new File(['original'], 'avatar.jpg', { type: 'image/jpeg' })));
    expect((fixture.page as any).avatar()).toBe('blob:existing');
    expect(fixture.auth.markAvatarChanged).not.toHaveBeenCalled();
    expect(fixture.notifications.error).toHaveBeenCalledWith('Upload failed.');
  });

  it('updates the displayed avatar and header signal only after successful upload', () => {
    const cropped = new File(['cropped'], 'profile-photo.webp', { type: 'image/webp' });
    const fixture = createPage(cropped);
    const revoke = vi.fn();
    vi.stubGlobal('URL', { createObjectURL: vi.fn(() => 'blob:updated'), revokeObjectURL: revoke });
    (fixture.page as any).avatar.set('blob:existing');
    (fixture.page as any).avatarObjectUrl = 'blob:existing';
    (fixture.page as any).selectPhoto(selection(new File(['original'], 'avatar.jpg', { type: 'image/jpeg' })));
    expect((fixture.page as any).avatar()).toBe('blob:updated');
    expect(revoke).toHaveBeenCalledWith('blob:existing');
    expect(fixture.auth.markAvatarChanged).toHaveBeenCalledTimes(1);
    expect(fixture.notifications.success).toHaveBeenCalledWith('Profile photo updated.');
  });

  it('prevents duplicate upload submissions', () => {
    const response = new Subject<unknown>();
    const fixture = createPage(undefined);
    fixture.post.mockImplementation(() => response as never);
    const cropped = new File(['cropped'], 'profile-photo.webp', { type: 'image/webp' });
    (fixture.page as any).uploadPhoto(cropped);
    (fixture.page as any).uploadPhoto(cropped);
    expect(fixture.post).toHaveBeenCalledTimes(1);
  });
});
