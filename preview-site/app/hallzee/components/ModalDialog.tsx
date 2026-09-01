"use client";

import { useEffect, type ReactNode } from "react";
import { X, type IconProps } from "./Icons";

interface ModalDialogProps {
  title: string;
  subtitle?: string;
  icon: React.ComponentType<IconProps>;
  iconClassName?: string;
  maxWidthClass?: string;
  onClose: () => void;
  children: ReactNode;
}

export default function ModalDialog({
  title,
  subtitle,
  icon: Icon,
  iconClassName = "bg-gradient-to-tr from-sky-500 to-cyan-400 text-white",
  maxWidthClass = "max-w-3xl",
  onClose,
  children,
}: ModalDialogProps) {
  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        onClose();
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [onClose]);

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-6 overflow-y-auto animate-in fade-in duration-200"
      role="dialog"
      aria-modal="true"
    >
      {/* Backdrop */}
      <button
        type="button"
        aria-label="Close dialog overlay"
        tabIndex={-1}
        onClick={onClose}
        className="fixed inset-0 bg-sky-950/60 backdrop-blur-sm transition-opacity cursor-default"
      />

      {/* Dialog Box */}
      <div
        className={`relative z-10 bg-white border border-sky-200/80 rounded-3xl w-full ${maxWidthClass} shadow-[0_25px_50px_-12px_rgba(2,132,199,0.25)] overflow-hidden flex flex-col max-h-[90vh] my-auto animate-in zoom-in-95 duration-200`}
      >
        {/* Modal Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-sky-100 bg-gradient-to-r from-sky-50/50 via-white to-sky-50/30">
          <div className="flex items-center gap-3.5">
            <div
              className={`h-10 w-10 rounded-2xl flex items-center justify-center shadow-md shadow-sky-500/20 shrink-0 ${iconClassName}`}
            >
              <Icon className="w-5 h-5" />
            </div>
            <div>
              <h2 className="font-extrabold text-lg text-slate-900 leading-tight">{title}</h2>
              {subtitle && <p className="text-xs font-medium text-slate-500 mt-0.5">{subtitle}</p>}
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Close dialog"
            className="h-8 w-8 rounded-full bg-slate-100 hover:bg-slate-200 text-slate-500 hover:text-slate-800 flex items-center justify-center transition-colors cursor-pointer"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Modal Content */}
        <div className="p-6 overflow-y-auto flex-1">{children}</div>
      </div>
    </div>
  );
}
