export type PaymentStatus = 'Pending' | 'Paid' | 'Failed' | 'RefundPending' | 'Refunded';

export interface Payment {
  id: number;
  reservationId: number;
  provider: string;
  providerPaymentId: string | null;
  currency: string;
  subtotalAmount: number;
  serviceFeeAmount: number;
  totalAmount: number;
  status: PaymentStatus;
  failureCode: string | null;
  paidAt: string | null;
  refundedAmount: number | null;
  createdAt: string;
  updatedAt: string;
}

export interface PaymentCheckout {
  payment: Payment;
  clientSecret: string | null;
}

export interface PaymentQuote {
  currency: string;
  unitPrice: number;
  quantity: number;
  subtotalAmount: number;
  serviceFeeAmount: number;
  totalAmount: number;
}
