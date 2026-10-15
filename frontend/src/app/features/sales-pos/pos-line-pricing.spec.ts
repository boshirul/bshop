import { describe, expect, it } from 'vitest';
import { priceForProductChange } from '../../shared/utils/line-product-pricing';

describe('priceForProductChange', () => {
  const products = [
    { id: 'product-a', salePrice: 100 },
    { id: 'product-b', salePrice: 250 }
  ];

  it('uses the selected product price for every newly selected cart line', () => {
    expect(priceForProductChange(undefined, 'product-a', products)).toBe(100);
    expect(priceForProductChange(undefined, 'product-b', products)).toBe(250);
  });

  it('replaces a previous product price but preserves an unchanged-line override', () => {
    expect(priceForProductChange('product-a', 'product-b', products)).toBe(250);
    expect(priceForProductChange('product-b', 'product-b', products)).toBeNull();
  });
});
