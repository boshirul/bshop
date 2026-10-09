import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-forgot-password-page',
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
          <mat-card-title>Reset your password</mat-card-title>
          <mat-card-subtitle>
            Enter your account email to request reset instructions.
          </mat-card-subtitle>
        </mat-card-header>
        <mat-card-content>
          <form [formGroup]="form" (ngSubmit)="submit()">
            <mat-form-field appearance="outline">
              <mat-label>Email</mat-label>
              <input matInput type="email" formControlName="email" />
            </mat-form-field>

            @if (message()) {
              <div class="success-message" role="status">{{ message() }}</div>
            }

            <button
              matButton="filled"
              type="submit"
              [disabled]="form.invalid || submitting()"
            >
              Send reset instructions
            </button>
            <a class="forgot-link" routerLink="/auth/login">Back to sign in</a>
          </form>
        </mat-card-content>
      </mat-card>
    </main>
  `,
  styleUrl: './auth-page.scss'
})
export class ForgotPasswordPage {
  private readonly formBuilder = inject(FormBuilder);
  private readonly auth = inject(AuthService);

  protected readonly submitting = signal(false);
  protected readonly message = signal('');
  protected readonly form = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email]]
  });

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.auth
      .forgotPassword(this.form.controls.email.value)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: (message) => this.message.set(message),
        error: () =>
          this.message.set(
            'If the account exists, password reset instructions will be sent.'
          )
      });
  }
}
