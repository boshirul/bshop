import { describe, expect, it } from 'vitest';
import { OnlineStoreCartService } from './online-store-cart.service';

const product = { id: 'battery', productCode: 'BAT-100', name: 'Solar Battery', brandName: 'BShop', unitSymbol: 'pcs', salePrice: 250, imageUrl: null, availableQuantity: 3 };

describe('OnlineStoreCartService', () => {
  it('counts item quantities and retains one line per product identity', () => {
    const cart = new OnlineStoreCartService();
    cart.add(product); cart.add(product);
    cart.add({ ...product, id: 'panel', name: 'Solar Panel', salePrice: 500 });
    expect(cart.lines()).toHaveLength(2);
    expect(cart.itemCount()).toBe(3);
    expect(cart.subtotal()).toBe(1000);
  });

  it('does not add unavailable products or exceed current available quantity', () => {
    const cart = new OnlineStoreCartService();
    expect(cart.add({ ...product, availableQuantity: 0 })).toBe('unavailable');
    cart.add(product); cart.add(product); cart.add(product);
    expect(cart.add(product)).toBe('limit');
    expect(cart.lines()[0].quantity).toBe(3);
  });

  it('updates quantities, removes lines, and controls the cart drawer', () => {
    const cart = new OnlineStoreCartService();
    cart.add(product); cart.setQuantity(product.id, 99);
    expect(cart.lines()[0].quantity).toBe(3);
    cart.openDrawer(); expect(cart.isDrawerOpen()).toBe(true);
    cart.remove(product.id); expect(cart.itemCount()).toBe(0);
    cart.closeDrawer(); expect(cart.isDrawerOpen()).toBe(false);
  });
});
