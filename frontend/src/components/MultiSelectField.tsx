import { ChevronDown } from 'lucide-react';
import { useCallback, useId, useState } from 'react';
import useDismissable from '../hooks/useDismissable';

interface MultiSelectFieldProps {
  label: string;
  options: readonly string[];
  value: string[];
  onChange: (value: string[]) => void;
  hint?: string;
  exclusiveOption?: string;
}

export const MultiSelectField = ({
  label,
  options,
  value,
  onChange,
  hint,
  exclusiveOption,
}: MultiSelectFieldProps) => {
  const generatedId = useId();
  const [open, setOpen] = useState(false);
  const close = useCallback(() => setOpen(false), []);
  const detailsRef = useDismissable<HTMLDetailsElement>(open, close);
  const labelId = `${generatedId}-label`;
  const hintId = `${generatedId}-hint`;
  const availableOptions = Array.from(new Set([...options, ...value]));

  const toggleOption = (option: string) => {
    if (value.includes(option)) {
      onChange(value.filter((selected) => selected !== option));
      return;
    }

    const selectedWithoutExclusive = exclusiveOption
      ? value.filter((selected) => selected !== exclusiveOption)
      : value;
    onChange(option === exclusiveOption
      ? [option]
      : [...selectedWithoutExclusive, option]);
  };

  return (
    <div className="field-group">
      <span className="field-label" id={labelId}>{label}</span>
      <details
        ref={detailsRef}
        className="multi-select"
        open={open}
        onToggle={(event) => setOpen(event.currentTarget.open)}
      >
        <summary
          className="text-field multi-select__summary"
          aria-labelledby={labelId}
          aria-describedby={hint ? hintId : undefined}
          aria-expanded={open}
          onKeyDown={(event) => {
            if (event.key === 'Enter' || event.key === ' ') {
              event.preventDefault();
              const detailsElement = event.currentTarget.closest('details');
              if (detailsElement) {
                detailsElement.open = !detailsElement.open;
              }
            }
          }}
        >
          {value.length === 0
            ? 'Selecciona una o varias opciones'
            : value.join(', ')}
          <ChevronDown className="multi-select__icon" size={18} aria-hidden="true" />
        </summary>
        <div
          className="multi-select__options"
          role="group"
          aria-labelledby={labelId}
          onKeyDown={(event) => {
            if (event.key === 'Enter') {
              event.preventDefault();
            }
          }}
        >
          {availableOptions.map((option) => (
            <label
              key={option}
              onKeyDown={(event) => {
                if (event.key === 'Enter') {
                  event.preventDefault();
                  toggleOption(option);
                }
              }}
            >
              <input
                type="checkbox"
                checked={value.includes(option)}
                onChange={() => toggleOption(option)}
                onKeyDown={(event) => {
                  if (event.key === 'Enter') {
                    event.preventDefault();
                    toggleOption(option);
                  }
                }}
              />
              <span>{option}</span>
            </label>
          ))}
        </div>
      </details>
      {hint && <span className="field-hint" id={hintId}>{hint}</span>}
    </div>
  );
};

export default MultiSelectField;
