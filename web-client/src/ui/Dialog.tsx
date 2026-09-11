import { useEffect, useRef, type ReactNode } from "react";
import { X } from "lucide-react";
export function Dialog({
  title,
  subtitle,
  size = "default",
  className = "",
  onClose,
  children,
}: {
  title: string;
  subtitle?: string;
  size?: "compact" | "default" | "wide";
  className?: string;
  onClose: () => void;
  children: ReactNode;
}) {
  const ref = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    const previous = document.activeElement as HTMLElement | null;
    const dialog = ref.current;
    dialog?.showModal();
    return () => {
      dialog?.close();
      previous?.focus();
    };
  }, []);
  return (
    <dialog
      ref={ref}
      className={`hallzee-modal-window modal-${size} ${className}`}
      onCancel={(event) => {
        event.preventDefault();
        onClose();
      }}
      aria-label={title}
    >
      <header className="dialog-header">
        <div className="dialog-title-group">
          <h2>{title}</h2>
          {subtitle && <p className="dialog-subtitle">{subtitle}</p>}
        </div>
        <button className="dialog-close-btn" aria-label="Close dialog" onClick={onClose}>
          <X size={18} />
        </button>
      </header>
      <div className="dialog-content">{children}</div>
    </dialog>
  );
}
