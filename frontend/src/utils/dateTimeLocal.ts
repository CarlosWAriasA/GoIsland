const pad = (value: number) => String(value).padStart(2, '0');

const toDateTimeLocalValue = (date: Date) =>
  `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;

export const isoToDateTimeLocalValue = (iso: string) => toDateTimeLocalValue(new Date(iso));

export const getDefaultDateTimeLocal = () => {
  const tomorrow = new Date();
  tomorrow.setDate(tomorrow.getDate() + 1);
  tomorrow.setHours(10, 0, 0, 0);
  return toDateTimeLocalValue(tomorrow);
};

export const getMinDateTimeLocal = () => {
  const now = new Date();
  now.setMinutes(now.getMinutes() + 5);
  return toDateTimeLocalValue(now);
};
