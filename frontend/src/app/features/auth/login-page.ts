import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-login-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule
  ],
  template: `
    <main class="auth-page">
      <section class="brand-panel" aria-labelledby="brand-heading">
        <div class="brand-wordmark" aria-label="BShop">
          <span class="brand-mark" aria-hidden="true">B</span><span>BShop</span>
        </div>
        <div class="brand-content">
          <p class="eyebrow">RETAIL OPERATIONS</p>
          <h1 id="brand-heading">One shop.<br /><span>One clear view.</span></h1>
          <p class="brand-copy">
            Inventory, POS, purchasing, customer dues, warranties, and online
            orders—kept together in one clear, capable workspace.
          </p>
          <ul class="feature-list" aria-label="BShop features">
            <li><span aria-hidden="true">▣</span> Manage inventory</li>
            <li><span aria-hidden="true">⌁</span> POS and sales</li>
            <li><span aria-hidden="true">◎</span> Customer dues</li>
            <li><span aria-hidden="true">◇</span> Warranty tracking</li>
          </ul>
        </div>
      </section>

      <mat-card class="login-card" appearance="outlined">
        <mat-card-header>
          <div class="login-wordmark" aria-label="BShop">
            <span class="brand-mark" aria-hidden="true">B</span><span>BShop</span>
          </div>
          <mat-card-title>Welcome to BShop</mat-card-title>
          <mat-card-subtitle>Sign in to continue</mat-card-subtitle>
        </mat-card-header>

        <mat-card-content>
          <form [formGroup]="form" (ngSubmit)="submit()">
            <mat-form-field appearance="outline">
              <mat-label>Email</mat-label>
              <input
                matInput
                type="email"
                formControlName="email"
                autocomplete="username"
              />
              @if (form.controls.email.touched && form.controls.email.invalid) {
                <mat-error>Enter a valid email address.</mat-error>
              }
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Password</mat-label>
              <input
                matInput
                [type]="hidePassword() ? 'password' : 'text'"
                formControlName="password"
                autocomplete="current-password"
              />
              <button
                class="password-toggle"
                type="button"
                matSuffix
                [attr.aria-label]="hidePassword() ? 'Show password' : 'Hide password'"
                [attr.aria-pressed]="!hidePassword()"
                (click)="hidePassword.set(!hidePassword())"
              >
                @if (hidePassword()) {
                  <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M2.5 12s3.3-6 9.5-6 9.5 6 9.5 6-3.3 6-9.5 6-9.5-6-9.5-6Z"/><circle cx="12" cy="12" r="2.6"/></svg>
                } @else {
                  <svg viewBox="0 0 24 24" aria-hidden="true"><path d="m3 3 18 18M10.6 6.2A10.7 10.7 0 0 1 12 6c6.2 0 9.5 6 9.5 6a17 17 0 0 1-3.1 3.7M6.3 6.4A17.1 17.1 0 0 0 2.5 12s3.3 6 9.5 6c1.1 0 2.1-.2 3-.5"/><path d="M9.9 9.9a3 3 0 0 0 4.2 4.2"/></svg>
                }
              </button>
              @if (form.controls.password.touched && form.controls.password.invalid) {
                <mat-error>Password is required.</mat-error>
              }
            </mat-form-field>

            @if (errorMessage()) {
              <div class="error-message" role="alert">{{ errorMessage() }}</div>
            }

            <button
              matButton
              class="sign-in-button"
              type="submit"
              [disabled]="form.invalid || submitting()"
            >
              @if (submitting()) {
                <span class="button-spinner" aria-hidden="true"></span> Signing in…
              } @else {
                Sign in <span aria-hidden="true">→</span>
              }
            </button>

            <a class="forgot-link" routerLink="/auth/forgot-password">
              Forgot your password?
            </a>
          </form>
        </mat-card-content>
      </mat-card>
    </main>
  `,
  styleUrl: './auth-page.scss'
})
export class LoginPage {
  private readonly formBuilder = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly submitting = signal(false);
  protected readonly hidePassword = signal(true);
  protected readonly errorMessage = signal('');
  protected readonly form = this.formBuilder.nonNullable.group({
    email: ['admin@khanshop.local', [Validators.required, Validators.email]],
    password: ['', Validators.required]
  });

  protected submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set('');
    this.auth
      .login(this.form.getRawValue())
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => {
          const returnUrl =
            this.route.snapshot.queryParamMap.get('returnUrl') ?? '/dashboard';
          this.router.navigateByUrl(returnUrl);
        },
        error: (error: unknown) => this.errorMessage.set(this.describeError(error))
      });
  }

  private describeError(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      const errors = error.error?.errors;
      if (Array.isArray(errors) && errors.length > 0) {
        return errors[0];
      }
    }

    return 'Unable to sign in. Check your details and try again.';
  }
}
