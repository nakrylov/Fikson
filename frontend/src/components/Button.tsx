import React from 'react';

/**
 * Placeholder Button component (minimal).
 * Replace with a real design system later.
 */
export type ButtonProps = React.ButtonHTMLAttributes<HTMLButtonElement> & {
  label?: string;
};

export function Button({ label, children, ...rest }: ButtonProps) {
  return (
    <button {...rest}>
      {label ?? children}
    </button>
  );
}

