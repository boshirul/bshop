import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { forkJoin } from 'rxjs';
import { OperationNotificationService } from '../../core/notifications/operation-notification.service';
import { UserManagementApiService } from './user-management-api.service';
import { PermissionSummary, RoleSummary, UserSummary } from './user-management.models';

@Component({
  selector: 'app-user-management-page',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTableModule
  ],
  template: `
    <header class="page-header">
      <div>
        <p class="eyebrow">ADMINISTRATION</p>
        <h1>Users & Roles</h1>
        <p>Create users, assign roles, and manage role permissions.</p>
      </div>
      <div class="actions">
        <button matButton="filled" type="button" (click)="beginCreateUser()">Add user</button>
        <button matButton type="button" (click)="beginCreateRole()">Add role</button>
      </div>
    </header>

    @if (error()) { <p class="error-message">{{ error() }}</p> }
    @if (message()) { <p class="success-message">{{ message() }}</p> }

    <section class="grid">
      <mat-card appearance="outlined">
        <mat-card-content>
          <h2>Users</h2>
          @if (editingUser()) {
            <form class="editor" (ngSubmit)="$event.preventDefault()">
              <mat-form-field appearance="outline"><mat-label>Full name</mat-label><input matInput [formControl]="fullName" /></mat-form-field>
              <mat-form-field appearance="outline"><mat-label>Email</mat-label><input matInput type="email" [formControl]="email" /></mat-form-field>
              @if (!editingUserId()) {
                <mat-form-field appearance="outline">
                  <mat-label>Password</mat-label>
                  <input matInput type="password" [formControl]="password" />
                  <mat-hint>At least 8 characters with uppercase, lowercase, number, and symbol.</mat-hint>
                  @if (password.touched && password.invalid) { <mat-error>Enter a password that meets the stated policy.</mat-error> }
                </mat-form-field>
              }
              <mat-form-field appearance="outline"><mat-label>Phone</mat-label><input matInput [formControl]="phone" /></mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>Roles</mat-label>
                <mat-select [formControl]="selectedRoles" multiple>
                  @for (role of roles(); track role.id) {
                    <mat-option [value]="role.name">{{ role.name }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>
              <mat-checkbox [formControl]="isActive">Active</mat-checkbox>
              <div class="actions">
                <button matButton="filled" type="button" (click)="saveUser()" [disabled]="saving() || fullName.invalid || email.invalid || (!editingUserId() && password.invalid)">{{ saving() ? 'Saving…' : 'Save user' }}</button>
                <button matButton type="button" (click)="cancelUser()">Cancel</button>
              </div>
            </form>
          }
          <div class="table-wrap">
            <table mat-table [dataSource]="users()">
              <ng-container matColumnDef="user"><th mat-header-cell *matHeaderCellDef>User</th><td mat-cell *matCellDef="let user"><strong>{{ user.fullName }}</strong><div class="code">{{ user.email }}</div></td></ng-container>
              <ng-container matColumnDef="roles"><th mat-header-cell *matHeaderCellDef>Roles</th><td mat-cell *matCellDef="let user">{{ user.roles.join(', ') || '—' }}</td></ng-container>
              <ng-container matColumnDef="status"><th mat-header-cell *matHeaderCellDef>Status</th><td mat-cell *matCellDef="let user"><span class="status" [class.inactive]="!user.isActive">{{ user.isActive ? 'Active' : 'Inactive' }}</span></td></ng-container>
              <ng-container matColumnDef="actions"><th mat-header-cell *matHeaderCellDef></th><td mat-cell *matCellDef="let user"><button matButton type="button" (click)="beginEditUser(user)">Edit</button><button matButton type="button" (click)="deactivate(user)">Deactivate</button></td></ng-container>
              <tr mat-header-row *matHeaderRowDef="userColumns"></tr>
              <tr mat-row *matRowDef="let row; columns: userColumns"></tr>
            </table>
          </div>
        </mat-card-content>
      </mat-card>

      <mat-card appearance="outlined">
        <mat-card-content>
          <h2>Roles & permissions</h2>
          @if (editingRole()) {
            <form class="editor" (ngSubmit)="saveRole()">
              <mat-form-field appearance="outline"><mat-label>Role name</mat-label><input matInput [formControl]="roleName" /></mat-form-field>
              <div class="permission-panel">
                @for (group of permissionGroups(); track group.group) {
                  <section>
                    <h3>{{ group.group }}</h3>
                    @for (permission of group.permissions; track permission.name) {
                      <mat-checkbox [checked]="rolePermissions().includes(permission.name)" (change)="togglePermission(permission.name, $event.checked)">
                        {{ permission.displayName }}
                        <span class="code">{{ permission.name }}</span>
                      </mat-checkbox>
                    }
                  </section>
                }
              </div>
              <div class="actions">
                <button matButton="filled" type="submit" [disabled]="roleName.invalid">Save role</button>
                <button matButton type="button" (click)="cancelRole()">Cancel</button>
              </div>
            </form>
          }
          <div class="role-list">
            @for (role of roles(); track role.id) {
              <article class="role-card">
                <div>
                  <strong>{{ role.name }}</strong>
                  <p>{{ role.permissions.length }} permission(s)</p>
                </div>
                <button matButton type="button" (click)="beginEditRole(role)">Edit permissions</button>
              </article>
            }
          </div>
        </mat-card-content>
      </mat-card>
    </section>
  `,
  styleUrl: './user-management.scss'
})
export class UserManagementPage implements OnInit {
  private readonly api = inject(UserManagementApiService);
  private readonly notifications = inject(OperationNotificationService);
  protected readonly saving = signal(false);

  protected readonly users = signal<UserSummary[]>([]);
  protected readonly roles = signal<RoleSummary[]>([]);
  protected readonly permissions = signal<PermissionSummary[]>([]);
  protected readonly error = signal('');
  protected readonly message = signal('');
  protected readonly editingUser = signal(false);
  protected readonly editingUserId = signal<string | null>(null);
  protected readonly editingRole = signal(false);
  protected readonly editingRoleId = signal<string | null>(null);
  protected readonly rolePermissions = signal<string[]>([]);
  protected readonly userColumns = ['user', 'roles', 'status', 'actions'];

  protected readonly fullName = new FormControl('', { nonNullable: true, validators: [Validators.required] });
  protected readonly email = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] });
  protected readonly password = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(8)] });
  protected readonly phone = new FormControl('', { nonNullable: true });
  protected readonly selectedRoles = new FormControl<string[]>([], { nonNullable: true });
  protected readonly isActive = new FormControl(true, { nonNullable: true });
  protected readonly roleName = new FormControl('', { nonNullable: true, validators: [Validators.required] });
  protected readonly permissionGroups = computed(() => {
    const groups = new Map<string, PermissionSummary[]>();
    for (const permission of this.permissions()) {
      groups.set(permission.group, [...(groups.get(permission.group) ?? []), permission]);
    }
    return [...groups.entries()].map(([group, permissions]) => ({ group, permissions }));
  });

  ngOnInit(): void { this.load(); }

  protected load(): void {
    forkJoin({ users: this.api.users(), roles: this.api.roles(), permissions: this.api.permissions() })
      .subscribe({
        next: value => { this.users.set(value.users); this.roles.set(value.roles); this.permissions.set(value.permissions); this.error.set(''); },
        error: error => this.fail(error, 'User management data could not be loaded.')
      });
  }

  protected beginCreateUser(): void {
    this.editingUserId.set(null); this.editingUser.set(true);
    this.fullName.setValue(''); this.email.setValue(''); this.password.setValue(''); this.phone.setValue(''); this.selectedRoles.setValue([]); this.isActive.setValue(true);
  }

  protected beginEditUser(user: UserSummary): void {
    this.editingUserId.set(user.id); this.editingUser.set(true);
    this.fullName.setValue(user.fullName); this.email.setValue(user.email); this.password.setValue('Password1!'); this.phone.setValue(user.phoneNumber ?? ''); this.selectedRoles.setValue([...user.roles]); this.isActive.setValue(user.isActive);
  }

  protected cancelUser(): void { this.editingUser.set(false); this.editingUserId.set(null); }

  protected saveUser(): void {
    if (this.saving()) return;
    this.saving.set(true);
    const id = this.editingUserId();
    const request = {
      fullName: this.fullName.value,
      email: this.email.value,
      phoneNumber: this.phone.value || null,
      isActive: this.isActive.value,
      roles: this.selectedRoles.value
    };
    const action = id
      ? this.api.updateUser(id, request)
      : this.api.createUser({ ...request, password: this.password.value });
    action.subscribe({ next: () => { this.saving.set(false); this.message.set('User saved.'); this.notifications.success(this.message()); this.cancelUser(); this.load(); }, error: error => { this.saving.set(false); this.fail(error, 'User could not be saved.', true); } });
  }

  protected deactivate(user: UserSummary): void {
    if (this.saving()) return;
    this.saving.set(true);
    this.api.deactivateUser(user.id).subscribe({ next: () => { this.saving.set(false); this.message.set('User deactivated.'); this.notifications.success(this.message()); this.load(); }, error: error => { this.saving.set(false); this.fail(error, 'User could not be deactivated.', true); } });
  }

  protected beginCreateRole(): void {
    this.editingRoleId.set(null); this.editingRole.set(true); this.roleName.setValue(''); this.rolePermissions.set([]);
  }

  protected beginEditRole(role: RoleSummary): void {
    this.editingRoleId.set(role.id); this.editingRole.set(true); this.roleName.setValue(role.name); this.rolePermissions.set([...role.permissions]);
  }

  protected cancelRole(): void { this.editingRole.set(false); this.editingRoleId.set(null); }

  protected saveRole(): void {
    if (this.saving()) return;
    this.saving.set(true);
    const id = this.editingRoleId();
    const action = id ? this.api.updateRole(id, this.roleName.value) : this.api.createRole(this.roleName.value);
    action.subscribe({
      next: role => {
        this.api.updateRolePermissions(role.id, this.rolePermissions()).subscribe({
          next: () => { this.saving.set(false); this.message.set('Role saved.'); this.notifications.success(this.message()); this.cancelRole(); this.load(); },
          error: error => { this.saving.set(false); this.fail(error, 'Role permissions could not be saved.', true); }
        });
      },
      error: error => { this.saving.set(false); this.fail(error, 'Role could not be saved.', true); }
    });
  }

  protected togglePermission(permission: string, checked: boolean): void {
    const current = new Set(this.rolePermissions());
    if (checked) current.add(permission); else current.delete(permission);
    this.rolePermissions.set([...current].sort());
  }

  private fail(error: unknown, fallback: string, notify = false): void {
    const message = error instanceof HttpErrorResponse && error.error?.errors?.[0] ? error.error.errors[0] : fallback;
    this.error.set(message);
    if (notify) this.notifications.apiError(error, message);
  }
}
