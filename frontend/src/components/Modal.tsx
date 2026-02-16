import React from 'react';

/**
 * Placeholder Modal component (minimal).
 * No styling/portal yet — just structure.
 */
export type ModalProps = {
  open: boolean;
  title?: string;
  onClose: () => void;
  children: React.ReactNode;
};

export function Modal({ open, title, onClose, children }: ModalProps) {
  if (!open) return null;

  return (
    <div role="dialog" aria-modal="true">
      <div>
        {title ? <h3>{title}</h3> : null}
        <button type="button" onClick={onClose}>
          Close
        </button>
      </div>
      <div>{children}</div>
    </div>
  );
}

