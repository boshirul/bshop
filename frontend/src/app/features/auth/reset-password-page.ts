import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { passwordsMatchValidator } from '../profile/password-validation';

@Component({
  selector: 'app-reset-password-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule
  ],
  template: `
    <main class="auth-page single-card">
      <mat-card class="login-card" appearance="outlined">
        <mat-card-header>
          <mat-card-title>Choose a new password</mat-card-title>
          <mat-card-subtitle>Use a strong password you do not use elsewhere.</mat-card-subtitle>
        </mat-card-header>
        <mat-card-content>
          @if (success()) {
            <div class="success-message" role="status">Your password has been reset. Sign in with your new password.</div>
            <a class="forgot-link" routerLink="/auth/login">Back to sign in</a>
          } @else { <form [formGroup]="form" (ngSubmit)="submit()">
            <mat-form-field appearance="outline">
              <mat-label>New password</mat-label>
              <input matInput [type]="showPassword() ? 'text' : 'password'" formControlName="newPassword" autocomplete="new-password" />
              <button matButton matSuffix type="button" class="password-toggle" (click)="showPassword.set(!showPassword())" [attr.aria-label]="showPassword() ? 'Hide new password' : 'Show new password'">{{ showPassword() ? 'Hide' : 'Show' }}</button>
              <mat-hint>At least 8 characters with upper, lower, number, and symbol.</mat-hint>
              @if (form.controls.newPassword.touched && form.controls.newPassword.invalid) { <mat-error>Use 8+ characters with upper/lowercase, a number, and a symbol.</mat-error> }
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Confirm new password</mat-label>
              <input matInput [type]="showConfirmation() ? 'text' : 'password'" formControlName="confirmNewPassword" autocomplete="new-password" />
              <button matButton matSuffix type="button" class="password-toggle" (click)="showConfirmation.set(!showConfirmation())" [attr.aria-label]="showConfirmation() ? 'Hide password confirmation' : 'Show password confirmation'">{{ showConfirmation() ? 'Hide' : 'Show' }}</button>
              @if (form.controls.confirmNewPassword.touched && form.controls.confirmNewPassword.hasError('required')) { <mat-error>Confirm your new password.</mat-error> }
              @if (form.touched && form.hasError('passwordMismatch')) { <mat-error>The new passwords do not match.</mat-error> }
            </mat-form-field>

            @if (errorMessage()) {
              <div class="error-message" role="alert">{{ errorMessage() }}</div>
            }

            <button
              matButton="filled"
              type="submit"
              [disabled]="form.invalid || submitting()"
            >
              Save new password
            </button>
            <a class="forgot-link" routerLink="/auth/login">Back to sign in</a>
          </form> }
        </mat-card-content>
      </mat-card>
    </main>
  `,
  styleUrl: './auth-page.scss'
})
export class ResetPasswordPage implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private email = '';
  private token = '';

  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly success = signal(false);
  protected readonly showPassword = signal(false);
  protected readonly showConfirmation = signal(false);
  protected readonly form = this.formBuilder.nonNullable.group({
    newPassword: ['', [Validators.required, Validators.minLength(8), Validators.pattern(/(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).+/)]],
    confirmNewPassword: ['', Validators.required]
  }, { validators: passwordsMatchValidator });

  ngOnInit(): void {
    const fragment = new URLSearchParams(this.route.snapshot.fragment ?? '');
    this.email = fragment.get('email') ?? this.route.snapshot.queryParamMap.get('email') ?? '';
    this.token = fragment.get('token') ?? this.route.snapshot.queryParamMap.get('token') ?? '';
    if (!this.email || !this.token) this.errorMessage.set('This reset link is incomplete, invalid, or expired.');
    if (globalThis.history?.replaceState && globalThis.location) {
      globalThis.history.replaceState(null, '', globalThis.location.pathname);
    }
  }

  protected submit(): void {
    if (!this.email || !this.token) {
      this.errorMessage.set('This reset link is incomplete, invalid, or expired.');
      return;
    }
    if (this.form.invalid || this.submitting()) { this.form.markAllAsTouched(); return; }

    this.submitting.set(true);
    this.errorMessage.set('');
    const passwords = this.form.getRawValue();
    this.auth
      .resetPassword(this.email, this.token, passwords.newPassword, passwords.confirmNewPassword)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => { this.token = ''; this.form.reset(); this.success.set(true); },
        error: () =>
          this.errorMessage.set(
            'The password could not be reset. The link may be invalid, expired, or already used.'
          )
      });
  }
}
