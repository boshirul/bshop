import { HttpErrorResponse } from '@angular/common/http';
import { Observable, of, throwError } from 'rxjs';
import { describe, expect, it } from 'vitest';
import { OnlineStoreApiService } from './online-store-api.service';
import {
  ONLINE_CART_MAX_AGE_MS,
  ONLINE_CART_STORAGE_KEY,
  ONLINE_CHECKOUT_ATTEMPT_KEY,
  OnlineStoreCartService
} from './online-store-cart.service';
import { OnlineProductDetailItem, OnlineProductListItem } from './online-store.models';

const product: OnlineProductListItem = { id: 'battery', productCode: 'BAT-100', name: 'Solar Battery', brandName: 'BShop', unitSymbol: 'pcs', salePrice: 250, imageUrl: null, availableQuantity: 3 };
const variation: OnlineProductListItem = { ...product, id: 'battery-blue', productCode: 'BAT-100-B', name: 'Solar Battery · Blue' };

class MemoryStorage implements Storage {
  private readonly values = new Map<string, string>();
  get length(): number { return this.values.size; }
  clear(): void { this.values.clear(); }
  getItem(key: string): string | null { return this.values.get(key) ?? null; }
  key(index: number): string | null { return [...this.values.keys()][index] ?? null; }
  removeItem(key: string): void { this.values.delete(key); }
  setItem(key: string, value: string): void { this.values.set(key, value); }
}

class StorageEvents {
  private listener: EventListener | null = null;
  addEventListener(_type: string, listener: EventListener): void { this.listener = listener; }
  removeEventListener(): void { this.listener = null; }
  emit(key: string, newValue: string | null): void { this.listener?.({ key, newValue } as StorageEvent); }
}

function detail(item: OnlineProductListItem): OnlineProductDetailItem {
  return { ...item, categoryId: 'category', categoryName: 'Power', description: null, isWarrantyAvailable: false, warrantyMonths: null, media: [] };
}

function api(products = new Map([[product.id, product], [variation.id, variation]])): OnlineStoreApiService {
  return { product: (id: string): Observable<OnlineProductDetailItem> => {
    const item = products.get(id);
    return item ? of(detail(item)) : throwError(() => new HttpErrorResponse({ status: 404 }));
  } } as OnlineStoreApiService;
}

function stored(lines: { productId: string; quantity: number }[], savedAt = Date.now()): string {
  return JSON.stringify({ version: 1, savedAt, lines });
}

describe('OnlineStoreCartService', () => {
  it('persists additions and restores current server product data after reinitialization', () => {
    const storage = new MemoryStorage();
    const first = new OnlineStoreCartService(api(), storage, null);
    first.add(product); first.add(product);

    const changed = { ...product, salePrice: 300 };
    const restored = new OnlineStoreCartService(api(new Map([[product.id, changed]])), storage, null);
    expect(restored.lines()).toHaveLength(1);
    expect(restored.lines()[0]).toMatchObject({ ...changed, quantity: 2 });
    expect(restored.subtotal()).toBe(600);
  });

  it('retains one line per product identity while keeping variations separate', () => {
    const cart = new OnlineStoreCartService(api(), new MemoryStorage(), null);
    cart.add(product); cart.add(product); cart.add(variation);
    expect(cart.lines().map(line => line.id)).toEqual(['battery', 'battery-blue']);
    expect(cart.itemCount()).toBe(3);
  });

  it('persists quantity changes and product removal', () => {
    const storage = new MemoryStorage();
    const cart = new OnlineStoreCartService(api(), storage, null);
    cart.add(product); cart.setQuantity(product.id, 3);
    expect(JSON.parse(storage.getItem(ONLINE_CART_STORAGE_KEY)!).lines).toEqual([{ productId: 'battery', quantity: 3 }]);
    cart.remove(product.id);
    expect(storage.getItem(ONLINE_CART_STORAGE_KEY)).toBeNull();
  });

  it('clears persisted cart and checkout attempt only on explicit success clearing', () => {
    const storage = new MemoryStorage();
    const cart = new OnlineStoreCartService(api(), storage, null);
    cart.add(product);
    const key = cart.checkoutAttempt('same-cart');
    expect(key).toBe(cart.checkoutAttempt('same-cart'));
    expect(storage.getItem(ONLINE_CART_STORAGE_KEY)).not.toBeNull();
    expect(storage.getItem(ONLINE_CHECKOUT_ATTEMPT_KEY)).not.toBeNull();
    cart.clear();
    expect(storage.getItem(ONLINE_CART_STORAGE_KEY)).toBeNull();
    expect(storage.getItem(ONLINE_CHECKOUT_ATTEMPT_KEY)).toBeNull();
  });

  it('preserves cart and idempotency key after a failed or interrupted checkout', () => {
    const storage = new MemoryStorage();
    const cart = new OnlineStoreCartService(api(), storage, null);
    cart.add(product);
    const key = cart.checkoutAttempt('cart-signature');
    const refreshed = new OnlineStoreCartService(api(), storage, null);
    expect(refreshed.lines()).toHaveLength(1);
    expect(refreshed.checkoutAttempt('cart-signature')).toBe(key);
  });

  it('invalidates checkout idempotency when cart contents change', () => {
    const storage = new MemoryStorage();
    const cart = new OnlineStoreCartService(api(), storage, null);
    cart.add(product);
    const firstKey = cart.checkoutAttempt('one');
    cart.setQuantity(product.id, 2);
    expect(cart.checkoutAttempt('two')).not.toBe(firstKey);
  });

  it('discards corrupted, incompatible, and expired storage', () => {
    for (const raw of ['{broken', JSON.stringify({ version: 2, savedAt: Date.now(), lines: [] }), stored([{ productId: 'battery', quantity: 1 }], Date.now() - ONLINE_CART_MAX_AGE_MS - 1)]) {
      const storage = new MemoryStorage(); storage.setItem(ONLINE_CART_STORAGE_KEY, raw);
      const cart = new OnlineStoreCartService(api(), storage, null);
      expect(cart.lines()).toEqual([]);
      expect(storage.getItem(ONLINE_CART_STORAGE_KEY)).toBeNull();
    }
  });

  it('continues in memory when browser storage throws', () => {
    const failing = { getItem: () => { throw new Error('denied'); }, setItem: () => { throw new Error('denied'); }, removeItem: () => { throw new Error('denied'); } } as unknown as Storage;
    const cart = new OnlineStoreCartService(api(), failing, null);
    expect(() => cart.add(product)).not.toThrow();
    expect(cart.itemCount()).toBe(1);
  });

  it('removes unavailable products and adjusts quantities to current stock', () => {
    const storage = new MemoryStorage();
    storage.setItem(ONLINE_CART_STORAGE_KEY, stored([{ productId: 'missing', quantity: 1 }, { productId: 'battery', quantity: 9 }]));
    const cart = new OnlineStoreCartService(api(), storage, null);
    expect(cart.lines()).toHaveLength(1);
    expect(cart.lines()[0]).toMatchObject({ ...product, quantity: 3 });
    expect(cart.lastNotice()).toContain('unavailable');
    expect(cart.lastNotice()).toContain('adjusted');
  });

  it('applies revalidated current prices and stock before checkout', () => {
    const storage = new MemoryStorage();
    const products = new Map([[product.id, product]]);
    const cart = new OnlineStoreCartService(api(products), storage, null);
    cart.add(product); cart.setQuantity(product.id, 3);
    products.set(product.id, { ...product, salePrice: 275, availableQuantity: 2 });
    let valid = false; cart.revalidate().subscribe(value => valid = value);
    expect(valid).toBe(true);
    expect(cart.lines()[0]).toMatchObject({ salePrice: 275, availableQuantity: 2, quantity: 2 });
  });

  it('synchronizes cart changes received from another tab', () => {
    const storage = new MemoryStorage();
    const events = new StorageEvents();
    const cart = new OnlineStoreCartService(api(), storage, events);
    const value = stored([{ productId: variation.id, quantity: 2 }]);
    storage.setItem(ONLINE_CART_STORAGE_KEY, value); events.emit(ONLINE_CART_STORAGE_KEY, value);
    expect(cart.lines()).toHaveLength(1);
    expect(cart.lines()[0]).toMatchObject({ ...variation, quantity: 2 });
    expect(cart.lastNotice()).toContain('another tab');
  });
});
