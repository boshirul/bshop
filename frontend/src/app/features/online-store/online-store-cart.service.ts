import { HttpErrorResponse } from '@angular/common/http';
import { Inject, Injectable, InjectionToken, computed, signal } from '@angular/core';
import { Observable, catchError, forkJoin, map, of } from 'rxjs';
import { OnlineStoreApiService } from './online-store-api.service';
import { OnlineProductListItem } from './online-store.models';

export type OnlineStoreCartLine = OnlineProductListItem & { quantity: number };
interface StoredCart { version: 1; savedAt: number; lines: { productId: string; quantity: number }[]; }
interface StoredCheckoutAttempt { version: 1; createdAt: number; cartSignature: string; key: string; }
interface RestoredProduct { product: OnlineProductListItem | null; productId: string; quantity: number; unavailable: boolean; }

export const ONLINE_CART_STORAGE_KEY = 'bshop.online-cart.v1';
export const ONLINE_CHECKOUT_ATTEMPT_KEY = 'bshop.online-checkout-attempt.v1';
export const ONLINE_CART_MAX_AGE_MS = 30 * 24 * 60 * 60 * 1000;
const CHECKOUT_ATTEMPT_MAX_AGE_MS = 24 * 60 * 60 * 1000;
const CART_STORAGE = new InjectionToken<Storage | null>('Online cart storage', { factory: safeStorage });
const CART_STORAGE_EVENTS = new InjectionToken<Pick<Window, 'addEventListener' | 'removeEventListener'> | null>('Online cart storage events', { factory: safeWindow });

@Injectable({ providedIn: 'root' })
export class OnlineStoreCartService {
  readonly lines = signal<OnlineStoreCartLine[]>([]);
  readonly isDrawerOpen = signal(false);
  readonly lastNotice = signal('');
  readonly restoring = signal(true);
  readonly itemCount = computed(() => this.lines().reduce((count, line) => count + line.quantity, 0));
  readonly subtotal = computed(() => this.lines().reduce((sum, line) => sum + line.salePrice * line.quantity, 0));

  private mutationRevision = 0;
  private readonly storageListener = (event: StorageEvent): void => {
    if (event.key === ONLINE_CART_STORAGE_KEY) this.restore(event.newValue, true);
  };

  constructor(
    private readonly api: OnlineStoreApiService,
    @Inject(CART_STORAGE) private readonly storage: Storage | null,
    @Inject(CART_STORAGE_EVENTS) private readonly storageEvents: Pick<Window, 'addEventListener' | 'removeEventListener'> | null
  ) {
    this.storageEvents?.addEventListener('storage', this.storageListener as EventListener);
    this.restore(this.read(ONLINE_CART_STORAGE_KEY), false);
  }

  add(product: OnlineProductListItem): 'added' | 'limit' | 'unavailable' {
    if (product.availableQuantity <= 0) { this.lastNotice.set('This product is currently unavailable.'); return 'unavailable'; }
    let result: 'added' | 'limit' = 'added';
    this.lines.update(lines => {
      const existing = lines.find(line => line.id === product.id);
      if (existing) {
        if (existing.quantity >= product.availableQuantity) { result = 'limit'; return lines; }
        return lines.map(line => line.id === product.id ? { ...product, quantity: Math.min(line.quantity + 1, product.availableQuantity) } : line);
      }
      return [...lines, { ...product, quantity: 1 }];
    });
    this.changed();
    this.lastNotice.set(result === 'added' ? `${product.name} was added to your cart.` : `Only ${product.availableQuantity} ${product.unitSymbol} are currently available.`);
    return result;
  }

  setQuantity(id: string, requested: number): void {
    this.lines.update(lines => lines.map(line => line.id === id ? { ...line, quantity: Math.max(1, Math.min(Number.isFinite(requested) ? requested : 1, line.availableQuantity)) } : line));
    this.changed();
  }

  remove(id: string): void { this.lines.update(lines => lines.filter(line => line.id !== id)); this.changed(); }
  openDrawer(): void { this.isDrawerOpen.set(true); }
  closeDrawer(): void { this.isDrawerOpen.set(false); }

  clear(): void {
    this.mutationRevision++;
    this.lines.set([]);
    this.lastNotice.set('');
    this.removeStored(ONLINE_CART_STORAGE_KEY);
    this.removeStored(ONLINE_CHECKOUT_ATTEMPT_KEY);
  }

  checkoutAttempt(cartSignature: string): string {
    const stored = this.parseAttempt(this.read(ONLINE_CHECKOUT_ATTEMPT_KEY));
    if (stored && stored.cartSignature === cartSignature) return stored.key;
    const attempt: StoredCheckoutAttempt = {
      version: 1,
      createdAt: Date.now(),
      cartSignature,
      key: globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(36).slice(2)}-${Math.random().toString(36).slice(2)}`
    };
    this.write(ONLINE_CHECKOUT_ATTEMPT_KEY, JSON.stringify(attempt));
    return attempt.key;
  }

  revalidate(): Observable<boolean> {
    const snapshot = this.lines().map(line => ({ productId: line.id, quantity: line.quantity }));
    if (!snapshot.length) return of(false);
    return this.resolve(snapshot).pipe(map(results => {
      if (results.some(result => !result.product && !result.unavailable)) {
        this.lastNotice.set('Your cart could not be refreshed. Check your connection and try again.');
        return false;
      }
      this.applyResolved(results, false);
      return this.lines().length > 0;
    }));
  }

  private changed(): void { this.mutationRevision++; this.removeStored(ONLINE_CHECKOUT_ATTEMPT_KEY); this.persist(); }

  private restore(raw: string | null, external: boolean): void {
    const stored = this.parseCart(raw);
    if (!stored) {
      if (raw !== null) this.removeStored(ONLINE_CART_STORAGE_KEY);
      if (external) this.lines.set([]);
      this.restoring.set(false);
      return;
    }
    const revision = this.mutationRevision;
    this.restoring.set(true);
    this.resolve(stored.lines).subscribe(results => {
      if (!external && revision !== this.mutationRevision) { this.restoring.set(false); return; }
      if (results.some(result => !result.product && !result.unavailable)) {
        this.lastNotice.set('Your saved cart could not be refreshed. Check your connection and reload the page.');
        this.restoring.set(false);
        return;
      }
      this.applyResolved(results, external);
      this.restoring.set(false);
    });
  }

  private resolve(lines: StoredCart['lines']): Observable<RestoredProduct[]> {
    const unique = [...new Map(lines.map(line => [line.productId, line])).values()];
    if (!unique.length) return of([]);
    return forkJoin(unique.map(line => this.api.product(line.productId).pipe(
      map(product => ({ product, productId: line.productId, quantity: line.quantity, unavailable: false })),
      catchError(error => of({ product: null, productId: line.productId, quantity: line.quantity, unavailable: error instanceof HttpErrorResponse && (error.status === 404 || error.status === 410) }))
    )));
  }

  private applyResolved(results: RestoredProduct[], external: boolean): void {
    const current = external ? [] : this.lines();
    const restored: OnlineStoreCartLine[] = [];
    let unavailable = 0;
    let adjusted = 0;
    for (const result of results) {
      if (!result.product) { if (result.unavailable) unavailable++; continue; }
      if (result.product.availableQuantity <= 0) { unavailable++; continue; }
      const quantity = Math.min(Math.max(1, Math.trunc(result.quantity)), result.product.availableQuantity);
      if (quantity !== result.quantity) adjusted++;
      restored.push({ ...result.product, quantity });
    }
    this.lines.set(external ? restored : [...restored, ...current.filter(line => !restored.some(item => item.id === line.id))]);
    if (unavailable || adjusted) {
      this.lastNotice.set([unavailable ? `${unavailable} unavailable item(s) were removed.` : '', adjusted ? `${adjusted} item quantity/quantities were adjusted to current stock.` : ''].filter(Boolean).join(' '));
    } else if (external) this.lastNotice.set('Your cart was updated in another tab.');
    if (!external || unavailable || adjusted) this.persist();
  }

  private persist(): void {
    if (!this.lines().length) { this.removeStored(ONLINE_CART_STORAGE_KEY); return; }
    const stored: StoredCart = { version: 1, savedAt: Date.now(), lines: this.lines().map(line => ({ productId: line.id, quantity: line.quantity })) };
    this.write(ONLINE_CART_STORAGE_KEY, JSON.stringify(stored));
  }

  private parseCart(raw: string | null): StoredCart | null {
    try {
      const value = raw ? JSON.parse(raw) as Partial<StoredCart> : null;
      if (!value || value.version !== 1 || typeof value.savedAt !== 'number' || Date.now() - value.savedAt > ONLINE_CART_MAX_AGE_MS || !Array.isArray(value.lines)) return null;
      const lines = value.lines.filter(line => line && typeof line.productId === 'string' && line.productId && Number.isFinite(line.quantity) && line.quantity > 0)
        .map(line => ({ productId: line.productId, quantity: Math.max(1, Math.trunc(line.quantity)) }));
      return { version: 1, savedAt: value.savedAt, lines };
    } catch { return null; }
  }

  private parseAttempt(raw: string | null): StoredCheckoutAttempt | null {
    try {
      const value = raw ? JSON.parse(raw) as Partial<StoredCheckoutAttempt> : null;
      return value?.version === 1 && typeof value.createdAt === 'number' && Date.now() - value.createdAt <= CHECKOUT_ATTEMPT_MAX_AGE_MS && typeof value.cartSignature === 'string' && typeof value.key === 'string' ? value as StoredCheckoutAttempt : null;
    } catch { return null; }
  }

  private read(key: string): string | null { try { return this.storage?.getItem(key) ?? null; } catch { return null; } }
  private write(key: string, value: string): void { try { this.storage?.setItem(key, value); } catch { /* Storage is optional. */ } }
  private removeStored(key: string): void { try { this.storage?.removeItem(key); } catch { /* Storage is optional. */ } }
}

function safeStorage(): Storage | null { try { return globalThis.localStorage ?? null; } catch { return null; } }
function safeWindow(): Window | null { return typeof window === 'undefined' ? null : window; }
