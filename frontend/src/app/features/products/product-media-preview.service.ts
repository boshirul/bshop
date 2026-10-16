import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { catchError, map, Observable, of, shareReplay, tap } from 'rxjs';

/** Resolves protected product-media endpoints to bounded cached object URLs. */
@Injectable({ providedIn: 'root' })
export class ProductMediaPreviewService {
  private readonly http = inject(HttpClient);
  private readonly cache = new Map<string, string | null>();
  private readonly pending = new Map<string, Observable<string | null>>();
  private readonly maximumCachedPreviews = 80;

  preview(url: string | null): Observable<string | null> {
    if (!url) return of(null);

    const cached = this.cache.get(url);
    if (cached !== undefined) {
      this.cache.delete(url);
      this.cache.set(url, cached);
      return of(cached);
    }

    const requestInFlight = this.pending.get(url);
    if (requestInFlight) return requestInFlight;

    const request = this.http.get(url, { responseType: 'blob' }).pipe(
      map((file) => URL.createObjectURL(file)),
      catchError(() => of(null)),
      tap((objectUrl) => {
        this.pending.delete(url);
        this.cache.set(url, objectUrl);
        this.trimCache();
      }),
      shareReplay({ bufferSize: 1, refCount: false })
    );
    this.pending.set(url, request);
    return request;
  }

  private trimCache(): void {
    while (this.cache.size > this.maximumCachedPreviews) {
      const oldest = this.cache.entries().next().value as [string, string | null] | undefined;
      if (!oldest) return;
      this.cache.delete(oldest[0]);
      if (oldest[1]) URL.revokeObjectURL(oldest[1]);
    }
  }
}
