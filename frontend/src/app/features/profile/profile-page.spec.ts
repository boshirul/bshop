import '@angular/compiler';
import { FormControl, FormGroup } from '@angular/forms';
import { describe, expect, it } from 'vitest';
import { passwordsMatchValidator } from './password-validation';

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
