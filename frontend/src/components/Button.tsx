import React from 'react';

export type ButtonProps = {
  children: React.ReactNode;
  variant?: 'primary' | 'secondary' | 'danger';
  className?: string;
} & React.ButtonHTMLAttributes<HTMLButtonElement>;

const base = 'px-4 py-2 rounded-md transition disabled:opacity-50 disabled:cursor-not-allowed';

const variants = {
  primary: 'bg-blue-600 text-white hover:bg-blue-700 active:bg-blue-800',
  secondary: 'bg-white text-gray-700 border border-gray-300 hover:bg-gray-50',
  danger: 'bg-red-600 text-white hover:bg-red-700'
} as const;

export function Button({ children, variant = 'secondary', className, ...rest }: ButtonProps) {
  const classes = [base, variants[variant], className].filter(Boolean).join(' ');
  return (
    <button className={classes} {...rest}>
      {children}
    </button>
  );
}

