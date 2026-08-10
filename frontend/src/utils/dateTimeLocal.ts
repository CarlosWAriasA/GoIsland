export const DEFAULT_TIME_ZONE = 'America/Santo_Domingo';

// Debe coincidir con Reservations:Expiration:BookingCutoffMinutes del backend.
export const BOOKING_LEAD_MINUTES = 30;

const getZonedParts = (date: Date, timeZone: string) => {
  const parts = Object.fromEntries(
    new Intl.DateTimeFormat('en-CA', {
      timeZone,
      year: 'numeric', month: '2-digit', day: '2-digit',
      hour: '2-digit', minute: '2-digit', second: '2-digit', hourCycle: 'h23',
    }).formatToParts(date)
      .filter((part) => part.type !== 'literal')
      .map((part) => [part.type, part.value]),
  );
  return {
    year: Number(parts.year), month: Number(parts.month), day: Number(parts.day),
    hour: Number(parts.hour), minute: Number(parts.minute), second: Number(parts.second),
  };
};

const format = ({ year, month, day, hour, minute }: ReturnType<typeof getZonedParts>) => {
  const pad = (value: number) => String(value).padStart(2, '0');
  return `${year}-${pad(month)}-${pad(day)}T${pad(hour)}:${pad(minute)}`;
};

const toDateTimeLocalValue = (date: Date, timeZone: string) => format(getZonedParts(date, timeZone));

export const isoToDateTimeLocalValue = (iso: string, timeZone = DEFAULT_TIME_ZONE) =>
  toDateTimeLocalValue(new Date(iso), timeZone);

/** Convierte el valor de un input datetime-local, leído en la zona horaria indicada, a un instante real. */
export const dateTimeLocalToDate = (value: string, timeZone = DEFAULT_TIME_ZONE) => {
  const [datePart, timePart] = value.split('T');
  if (!datePart || !timePart) return new Date(NaN);
  const [year, month, day] = datePart.split('-').map(Number);
  const [hour, minute] = timePart.split(':').map(Number);
  const expectedUtc = Date.UTC(year, month - 1, day, hour, minute);
  let candidate = new Date(expectedUtc);
  for (let attempt = 0; attempt < 2; attempt += 1) {
    const parts = getZonedParts(candidate, timeZone);
    const representedUtc = Date.UTC(
      parts.year, parts.month - 1, parts.day, parts.hour, parts.minute, parts.second,
    );
    candidate = new Date(candidate.getTime() + expectedUtc - representedUtc);
  }
  return candidate;
};

export const getDefaultDateTimeLocal = (timeZone = DEFAULT_TIME_ZONE) => {
  const parts = getZonedParts(new Date(), timeZone);
  const tomorrow = new Date(Date.UTC(parts.year, parts.month - 1, parts.day + 1));
  return format({
    year: tomorrow.getUTCFullYear(),
    month: tomorrow.getUTCMonth() + 1,
    day: tomorrow.getUTCDate(),
    hour: 10, minute: 0, second: 0,
  });
};

/** Primer momento reservable: el backend rechaza cualquier horario dentro del margen de anticipación. */
export const getMinDateTimeLocal = (timeZone = DEFAULT_TIME_ZONE) => {
  const earliest = new Date(Date.now() + BOOKING_LEAD_MINUTES * 60_000);
  return toDateTimeLocalValue(earliest, timeZone);
};

export const isWithinBookingWindow = (value: string, timeZone = DEFAULT_TIME_ZONE) => {
  const selected = dateTimeLocalToDate(value, timeZone);
  if (Number.isNaN(selected.getTime())) return false;
  const now = Date.now();
  return selected.getTime() >= now + BOOKING_LEAD_MINUTES * 60_000
    && selected.getTime() <= new Date(now).setFullYear(new Date(now).getFullYear() + 1);
};
