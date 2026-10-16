import '@angular/compiler';
import { HttpClient } from '@angular/common/http';
import { EnvironmentInjector, runInInjectionContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ProductMediaPreviewService } from './product-media-preview.service';

describe('ProductMediaPreviewService', () => {
  let injector: EnvironmentInjector;

  afterEach(() => TestBed.resetTestingModule());

  it('uses one authenticated HttpClient request for repeated media URLs', () => {
    const get = vi.fn(() => of(new Blob(['image'])));
    vi.stubGlobal('URL', { createObjectURL: vi.fn(() => 'blob:preview'), revokeObjectURL: vi.fn() });
    TestBed.configureTestingModule({ providers: [{ provide: HttpClient, useValue: { get } }] });
    injector = TestBed.inject(EnvironmentInjector);
    const service = runInInjectionContext(injector, () => new ProductMediaPreviewService());
    let first: string | null = null;
    let second: string | null = null;
    service.preview('/api/products/a/media/b/content').subscribe((url) => first = url);
    service.preview('/api/products/a/media/b/content').subscribe((url) => second = url);
    expect(first).toBe('blob:preview');
    expect(second).toBe('blob:preview');
    expect(get).toHaveBeenCalledTimes(1);
    vi.unstubAllGlobals();
  });

  it('returns a placeholder value when protected media cannot be read', () => {
    const get = vi.fn(() => throwError(() => new Error('forbidden')));
    TestBed.configureTestingModule({ providers: [{ provide: HttpClient, useValue: { get } }] });
    injector = TestBed.inject(EnvironmentInjector);
    const service = runInInjectionContext(injector, () => new ProductMediaPreviewService());
    let result: string | null | undefined;
    service.preview('/api/products/a/media/b/content').subscribe((url) => result = url);
    expect(result).toBeNull();
  });
});
