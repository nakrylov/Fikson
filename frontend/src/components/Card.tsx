import React from 'react';

export type CardProps = {
  children: React.ReactNode;
  title?: string;
  actions?: React.ReactNode;
};

export function Card({ children, title, actions }: CardProps) {
  return (
    <div className="bg-white border border-gray-200 rounded-lg shadow-sm p-4">
      {title || actions ? (
        <div className="flex justify-between items-center mb-3">
          {title ? <h3 className="text-lg font-semibold text-gray-800">{title}</h3> : <div />}
          {actions}
        </div>
      ) : null}
      {children}
    </div>
  );
}

