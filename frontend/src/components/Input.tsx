import React from 'react';

export type InputProps = React.InputHTMLAttributes<HTMLInputElement> & {
  error?: boolean;
};

const base = 'w-full px-3 py-2 border rounded-md outline-none transition';
const normalStyles = 'border-gray-300 focus:ring-2 focus:ring-blue-500';
const errorStyles = 'border-red-500 focus:ring-2 focus:ring-red-500';

export function Input({ error = false, className, ...rest }: InputProps) {
  const classes = [base, error ? errorStyles : normalStyles, className].filter(Boolean).join(' ');
  return <input className={classes} {...rest} />;
}

