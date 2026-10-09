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
        </mat-card-header>
        <mat-card-content>
          <form [formGroup]="form" (ngSubmit)="submit()">
            <mat-form-field appearance="outline">
              <mat-label>New password</mat-label>
              <input matInput type="password" formControlName="password" />
              <mat-hint>At least 8 characters with upper, lower, number, and symbol.</mat-hint>
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
          </form>
        </mat-card-content>
      </mat-card>
    </main>
  `,
  styleUrl: './auth-page.scss'
})
export class ResetPasswordPage {
  private readonly formBuilder = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly form = this.formBuilder.nonNullable.group({
    password: ['', [Validators.required, Validators.minLength(8)]]
  });

  protected submit(): void {
    const email = this.route.snapshot.queryParamMap.get('email');
    const token = this.route.snapshot.queryParamMap.get('token');
    if (!email || !token) {
      this.errorMessage.set('This reset link is incomplete or invalid.');
      return;
    }

    this.submitting.set(true);
    this.auth
      .resetPassword(email, token, this.form.controls.password.value)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => this.router.navigate(['/auth/login']),
        error: () =>
          this.errorMessage.set(
            'The password could not be reset. The link may have expired.'
          )
      });
  }
}
