import { AbstractControl, ValidationErrors } from '@angular/forms';

export function passwordsMatchValidator(control: AbstractControl): ValidationErrors | null {
  const newPassword = control.get('newPassword')?.value;
  const confirmation = control.get('confirmNewPassword')?.value;
  return newPassword && confirmation && newPassword !== confirmation ? { passwordMismatch: true } : null;
}
