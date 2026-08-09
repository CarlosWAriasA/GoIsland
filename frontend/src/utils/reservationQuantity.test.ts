import { describe, expect, it } from 'vitest';
import { isValidReservationQuantity } from './reservationQuantity';

describe('isValidReservationQuantity', () => {
  it.each([1, 2, 100])('accepts %s people', (quantity) => {
    expect(isValidReservationQuantity(quantity)).toBe(true);
  });

  it.each([0, -1, 1.5, 101, Number.NaN])('rejects %s people', (quantity) => {
    expect(isValidReservationQuantity(quantity)).toBe(false);
  });
});
