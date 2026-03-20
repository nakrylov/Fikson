import React from 'react';

export type Column<T> = {
  key: keyof T | string;
  title: string;
  render?: (row: T) => React.ReactNode;
};

export type TableProps<T> = {
  columns: Column<T>[];
  data: T[];
  emptyText?: string;
};

export function Table<T>(props: TableProps<T>) {
  const { columns, data, emptyText } = props;

  return (
    <div className="w-full border border-gray-200 rounded-lg overflow-hidden">
      <table className="w-full">
        <thead>
          <tr className="bg-gray-50">
            {columns.map((column) => (
              <th key={String(column.key)} className="text-left text-sm font-medium text-gray-600 p-3">
                {column.title}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {data.length === 0 ? (
            <tr className="border-t hover:bg-gray-50">
              <td colSpan={columns.length} className="p-4 text-center text-gray-500">
                {emptyText ?? 'No data'}
              </td>
            </tr>
          ) : (
            data.map((row, rowIndex) => (
              <tr key={rowIndex} className="border-t hover:bg-gray-50">
                {columns.map((column) => (
                  <td key={String(column.key)} className="p-3 text-sm text-gray-800">
                    {column.render ? column.render(row) : ((row as Record<string, React.ReactNode>)[String(column.key)] ?? '')}
                  </td>
                ))}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}

