import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  dateTimeLocalToDate, getDefaultDateTimeLocal, getMinDateTimeLocal, isWithinBookingWindow, isoToDateTimeLocalValue,
} from './dateTimeLocal';

const TIME_ZONE = 'America/Santo_Domingo';

describe('dateTimeLocal', () => {
  beforeEach(() => {
    // 10 de agosto de 2026, 08:58 en Santo Domingo (UTC-4).
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-08-10T12:58:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('interprets the input value in the experience time zone', () => {
    expect(dateTimeLocalToDate('2026-08-15T09:00', TIME_ZONE).toISOString())
      .toBe('2026-08-15T13:00:00.000Z');
  });

  it('formats an instant in the experience time zone', () => {
    expect(isoToDateTimeLocalValue('2026-08-15T13:00:00Z', TIME_ZONE)).toBe('2026-08-15T09:00');
  });

  it('rejects a visit inside the booking cutoff', () => {
    expect(isWithinBookingWindow('2026-08-10T09:00', TIME_ZONE)).toBe(false);
  });

  it('accepts a visit past the booking cutoff', () => {
    expect(isWithinBookingWindow('2026-08-10T09:30', TIME_ZONE)).toBe(true);
  });

  it('rejects a visit more than a year ahead', () => {
    expect(isWithinBookingWindow('2027-09-10T09:00', TIME_ZONE)).toBe(false);
  });

  it('offers the earliest bookable value as minimum', () => {
    expect(getMinDateTimeLocal(TIME_ZONE)).toBe('2026-08-10T09:28');
  });

  it('defaults to tomorrow at 10:00 in the experience time zone', () => {
    expect(getDefaultDateTimeLocal(TIME_ZONE)).toBe('2026-08-11T10:00');
  });

  it('keeps the experience time zone even when the browser is elsewhere', () => {
    // 09:00 en Madrid es medianoche pasada en Santo Domingo del mismo día.
    expect(dateTimeLocalToDate('2026-08-15T09:00', 'Europe/Madrid').toISOString())
      .toBe('2026-08-15T07:00:00.000Z');
  });
});
