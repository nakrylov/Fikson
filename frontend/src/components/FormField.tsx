import React from 'react';

export type FormFieldProps = {
  label: string;
  error?: string;
  children: React.ReactNode;
};

export function FormField({ label, error, children }: FormFieldProps) {
  return (
    <div className="space-y-1">
      <label className="text-sm font-medium text-gray-700">{label}</label>
      {children}
      {error ? <div className="text-sm text-red-600">{error}</div> : null}
    </div>
  );
}

