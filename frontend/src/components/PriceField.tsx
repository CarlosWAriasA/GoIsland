import { CircleDollarSign } from 'lucide-react';
import type { ComponentProps } from 'react';
import Input from './Input';

type PriceFieldProps = Omit<ComponentProps<typeof Input>, 'type' | 'icon'>;

export const PriceField = (props: PriceFieldProps) => (
  <Input
    type="number"
    min="0"
    step="0.01"
    inputMode="decimal"
    icon={<CircleDollarSign size={18} />}
    {...props}
  />
);

export default PriceField;
