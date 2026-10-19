import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { Router } from '@angular/router';
import { ApiResponse } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { API_BASE_URL } from '../../core/http/api-base-url.token';
import { OperationNotificationService } from '../../core/notifications/operation-notification.service';
import { passwordsMatchValidator } from './password-validation';
import { ProfilePhotoEditorDialog } from './profile-photo-editor-dialog';
import { validateProfilePhoto } from './profile-photo-editor';

interface Profile { fullName: string; userName: string; email: string; isActive: boolean; roles: string[]; avatarUrl: string | null; }

@Component({
  selector: 'app-profile-page',
  imports: [ReactiveFormsModule, MatButtonModule, MatCardModule, MatFormFieldModule, MatInputModule, MatDialogModule],
  template: `
    <header class="page-header"><div><p class="eyebrow">ACCOUNT</p><h1>My profile</h1><p>Manage your sign-in security and profile photo.</p></div></header>
    @if (profile(); as item) { <section class="profile-grid">
      <mat-card appearance="outlined" class="transaction-panel"><mat-card-content>
        <div class="avatar">@if (avatar()) { <img [src]="avatar()" alt="Profile photo"/> } @else { {{ item.fullName.slice(0, 1) }} }</div>
        <h2>{{ item.fullName }}</h2><p>{{ item.email }}</p><p>Username: {{ item.userName }}</p><p>Roles: {{ item.roles.join(', ') || 'None' }}</p>
        <label class="transaction-primary" [class.disabled]="uploading()">{{ uploading() ? 'Uploading photo…' : 'Change profile photo' }}<input type="file" accept="image/jpeg,image/png,image/webp" hidden [disabled]="uploading()" (change)="selectPhoto($event)" /></label><p class="transaction-helper">JPEG, PNG, or WebP up to 2 MB. Crop output: 512 × 512.</p>
      </mat-card-content></mat-card>
      <mat-card appearance="outlined" class="transaction-panel"><mat-card-content><h2>Change password</h2>
        <p class="transaction-helper">Use at least 8 characters with uppercase, lowercase, a number, and a symbol.</p>
        <form [formGroup]="passwordForm" (ngSubmit)="changePassword()" novalidate>
          <mat-form-field appearance="outline"><mat-label>Current password</mat-label><input matInput [type]="showCurrentPassword() ? 'text' : 'password'" formControlName="currentPassword" autocomplete="current-password"/>
            <button matButton matSuffix type="button" class="password-visibility" (click)="showCurrentPassword.set(!showCurrentPassword())" [attr.aria-label]="showCurrentPassword() ? 'Hide current password' : 'Show current password'">{{ showCurrentPassword() ? 'Hide' : 'Show' }}</button>
            @if (passwordForm.controls.currentPassword.touched && passwordForm.controls.currentPassword.hasError('required')) { <mat-error>Enter your current password.</mat-error> }
          </mat-form-field>
          <mat-form-field appearance="outline"><mat-label>New password</mat-label><input matInput [type]="showNewPassword() ? 'text' : 'password'" formControlName="newPassword" autocomplete="new-password"/>
            <button matButton matSuffix type="button" class="password-visibility" (click)="showNewPassword.set(!showNewPassword())" [attr.aria-label]="showNewPassword() ? 'Hide new password' : 'Show new password'">{{ showNewPassword() ? 'Hide' : 'Show' }}</button>
            @if (passwordForm.controls.newPassword.touched && passwordForm.controls.newPassword.hasError('required')) { <mat-error>Enter a new password.</mat-error> }
            @if (passwordForm.controls.newPassword.touched && (passwordForm.controls.newPassword.hasError('minlength') || passwordForm.controls.newPassword.hasError('pattern'))) { <mat-error>Use 8+ characters with upper/lowercase, a number, and a symbol.</mat-error> }
          </mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Confirm new password</mat-label><input matInput [type]="showConfirmation() ? 'text' : 'password'" formControlName="confirmNewPassword" autocomplete="new-password"/>
            <button matButton matSuffix type="button" class="password-visibility" (click)="showConfirmation.set(!showConfirmation())" [attr.aria-label]="showConfirmation() ? 'Hide confirmation password' : 'Show confirmation password'">{{ showConfirmation() ? 'Hide' : 'Show' }}</button>
            @if (passwordForm.controls.confirmNewPassword.touched && passwordForm.controls.confirmNewPassword.hasError('required')) { <mat-error>Confirm your new password.</mat-error> }
            @if (passwordForm.touched && passwordForm.hasError('passwordMismatch')) { <mat-error>The new passwords do not match.</mat-error> }
          </mat-form-field>
          @if (passwordForm.invalid && (passwordForm.touched || submitted())) { <p class="transaction-helper" role="status">Complete the password requirements above to enable Change password.</p> }
          <button matButton="filled" class="transaction-primary" type="submit" [disabled]="passwordForm.invalid || saving()">{{ saving() ? 'Changing password…' : 'Change password' }}</button>
        </form>
      </mat-card-content></mat-card>
    </section> }
  `,
  styles: `:host{display:block}.profile-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:1rem}.avatar{width:5rem;height:5rem;border-radius:50%;display:grid;place-items:center;background:var(--bshop-color-primary);color:var(--bshop-color-text);font-size:2rem;overflow:hidden}.avatar img{width:100%;height:100%;object-fit:cover}.transaction-panel mat-form-field{display:block}.password-visibility{font-size:.78rem;min-width:auto;padding-inline:.35rem}@media(max-width:700px){.profile-grid{grid-template-columns:1fr}}`
})
export class ProfilePage implements OnInit, OnDestroy {
  private readonly http = inject(HttpClient);
  private readonly api = inject(API_BASE_URL);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notifications = inject(OperationNotificationService);
  private readonly dialog = inject(MatDialog);
  private avatarObjectUrl: string | null = null;
  protected readonly profile = signal<Profile | null>(null);
  protected readonly avatar = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly uploading = signal(false);
  protected readonly submitted = signal(false);
  protected readonly showCurrentPassword = signal(false);
  protected readonly showNewPassword = signal(false);
  protected readonly showConfirmation = signal(false);
  protected readonly passwordForm = new FormGroup({
    currentPassword: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    newPassword: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(8), Validators.pattern(/(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).+/)] }),
    confirmNewPassword: new FormControl('', { nonNullable: true, validators: [Validators.required] })
  }, { validators: passwordsMatchValidator });

  ngOnInit(): void { this.load(); }
  ngOnDestroy(): void { if (this.avatarObjectUrl) URL.revokeObjectURL(this.avatarObjectUrl); }

  protected selectPhoto(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    const validationError = validateProfilePhoto(file);
    if (validationError) { this.notifications.error(validationError); return; }
    this.dialog.open(ProfilePhotoEditorDialog, {
      data: file,
      width: 'min(44rem, calc(100vw - 1rem))',
      maxWidth: 'calc(100vw - 1rem)',
      disableClose: false,
      autoFocus: false
    }).afterClosed().subscribe(cropped => { if (cropped) this.uploadPhoto(cropped); });
  }

  private uploadPhoto(file: File): void {
    if (this.uploading()) return;
    this.uploading.set(true);
    const body = new FormData(); body.append('file', file);
    const progress = this.notifications.loading('Uploading profile photo…');
    this.http.post<ApiResponse<string>>(`${this.api}/auth/profile/photo`, body).subscribe({
      next: (response) => { progress.dismiss(); this.uploading.set(false); if (!response.succeeded) { this.notifications.error(response.errors[0] ?? 'Photo could not be updated.'); return; } this.auth.markAvatarChanged(); this.loadAvatar(); this.notifications.success('Profile photo updated.'); },
      error: (error) => { progress.dismiss(); this.uploading.set(false); this.notifications.error(this.describeError(error, 'Photo could not be updated.')); }
    });
  }

  protected changePassword(): void {
    this.submitted.set(true);
    if (this.passwordForm.invalid || this.saving()) { this.passwordForm.markAllAsTouched(); return; }
    this.saving.set(true);
    const progress = this.notifications.loading('Changing password…');
    this.http.post<ApiResponse<boolean>>(`${this.api}/auth/profile/change-password`, this.passwordForm.getRawValue()).subscribe({
      next: (response) => { progress.dismiss(); this.saving.set(false); if (!response.succeeded) { this.notifications.error(response.errors[0] ?? 'Password could not be changed.'); return; } this.passwordForm.reset({ currentPassword: '', newPassword: '', confirmNewPassword: '' }); this.auth.clearSession(); void this.router.navigate(['/auth/login'], { queryParams: { passwordChanged: '1' } }); },
      error: (error) => { progress.dismiss(); this.saving.set(false); this.notifications.error(this.describeError(error, 'Password could not be changed.')); }
    });
  }

  private load(): void {
    this.http.get<ApiResponse<Profile>>(`${this.api}/auth/profile`).subscribe({
      next: (response) => { if (!response.data) { this.notifications.error('Profile could not be loaded.'); return; } this.profile.set(response.data); if (response.data.avatarUrl) this.loadAvatar(); },
      error: () => this.notifications.error('Profile could not be loaded.')
    });
  }

  private loadAvatar(): void {
    this.http.get(`${this.api}/auth/profile/photo/content`, { responseType: 'blob' }).subscribe({ next: (file) => { if (this.avatarObjectUrl) URL.revokeObjectURL(this.avatarObjectUrl); this.avatarObjectUrl = URL.createObjectURL(file); this.avatar.set(this.avatarObjectUrl); } });
  }

  private describeError(error: unknown, fallback: string): string {
    return error instanceof HttpErrorResponse && Array.isArray(error.error?.errors) && error.error.errors[0] ? error.error.errors[0] : fallback;
  }
}
