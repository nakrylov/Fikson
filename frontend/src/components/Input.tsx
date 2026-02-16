import React from 'react';

/**
 * Placeholder Input component (minimal).
 */
export type InputProps = React.InputHTMLAttributes<HTMLInputElement> & {
  label?: string;
};

export function Input({ label, ...rest }: InputProps) {
  return (
    <label style={{ display: 'block' }}>
      {label ? <div>{label}</div> : null}
      <input {...rest} />
    </label>
  );
}

