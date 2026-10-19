import '@angular/compiler';
import { EnvironmentInjector, runInInjectionContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormBuilder } from '@angular/forms';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { ForgotPasswordPage } from './forgot-password-page';
import { ResetPasswordPage } from './reset-password-page';

describe('password recovery pages', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('validates the forgot-password email and always shows the generic response', () => {
    const forgotPassword = vi.fn(() => of('server detail'));
    TestBed.configureTestingModule({ providers: [FormBuilder, { provide: AuthService, useValue: { forgotPassword } }] });
    const injector = TestBed.inject(EnvironmentInjector);
    const page = runInInjectionContext(injector, () => new ForgotPasswordPage());
    (page as any).form.controls.email.setValue('invalid');
    (page as any).submit();
    expect(forgotPassword).not.toHaveBeenCalled();
    (page as any).form.controls.email.setValue('user@example.test');
    (page as any).submit();
    expect((page as any).message()).toBe('If an account exists for this email address, password reset instructions will be sent.');
  });

  it('captures fragment credentials, removes them from history, and rejects mismatched passwords', () => {
    const resetPassword = vi.fn(() => of(true));
    const replaceState = vi.spyOn(globalThis.history, 'replaceState');
    const page = createResetPage('email=user%40example.test&token=protected-token', resetPassword);
    page.ngOnInit();
    expect(replaceState).toHaveBeenCalled();
    (page as any).form.setValue({ newPassword: 'NewPassword2!', confirmNewPassword: 'Different3!' });
    (page as any).submit();
    expect(resetPassword).not.toHaveBeenCalled();
    replaceState.mockRestore();
  });

  it('submits a valid reset once and shows confirmation without signing in', () => {
    const resetPassword = vi.fn(() => of(true));
    const page = createResetPage('email=user%40example.test&token=protected-token', resetPassword);
    page.ngOnInit();
    (page as any).form.setValue({ newPassword: 'NewPassword2!', confirmNewPassword: 'NewPassword2!' });
    (page as any).submit();
    expect(resetPassword).toHaveBeenCalledWith('user@example.test', 'protected-token', 'NewPassword2!', 'NewPassword2!');
    expect((page as any).success()).toBe(true);
  });

  it('shows safe feedback for invalid, expired, or reused reset tokens', () => {
    const resetPassword = vi.fn(() => throwError(() => new Error('token detail')));
    const page = createResetPage('email=user%40example.test&token=bad-token', resetPassword);
    page.ngOnInit();
    (page as any).form.setValue({ newPassword: 'NewPassword2!', confirmNewPassword: 'NewPassword2!' });
    (page as any).submit();
    expect((page as any).errorMessage()).toContain('invalid, expired, or already used');
    expect((page as any).errorMessage()).not.toContain('token detail');
  });

  it('rejects an incomplete reset link before calling the API', () => {
    const resetPassword = vi.fn(() => of(true));
    const page = createResetPage(null, resetPassword);
    page.ngOnInit();
    expect((page as any).errorMessage()).toContain('incomplete');
    (page as any).submit();
    expect(resetPassword).not.toHaveBeenCalled();
  });

  function createResetPage(fragment: string | null, resetPassword: ReturnType<typeof vi.fn>): ResetPasswordPage {
    TestBed.configureTestingModule({ providers: [
      FormBuilder,
      { provide: AuthService, useValue: { resetPassword } },
      { provide: ActivatedRoute, useValue: { snapshot: { fragment, queryParamMap: convertToParamMap({}) } } }
    ] });
    const injector = TestBed.inject(EnvironmentInjector);
    return runInInjectionContext(injector, () => new ResetPasswordPage());
  }
});
