export const isValidReservationQuantity = (quantity: number) =>
  Number.isInteger(quantity) && quantity >= 1 && quantity <= 100;
