import React from 'react';

/**
 * Placeholder Table component (minimal).
 * For now, you can pass `rows` as JSX (or build a typed table later).
 */
export type TableProps = {
  headers?: string[];
  children: React.ReactNode;
};

export function Table({ headers, children }: TableProps) {
  return (
    <table>
      {headers ? (
        <thead>
          <tr>
            {headers.map((h) => (
              <th key={h}>{h}</th>
            ))}
          </tr>
        </thead>
      ) : null}
      <tbody>{children}</tbody>
    </table>
  );
}

