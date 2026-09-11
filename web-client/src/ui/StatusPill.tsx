import type { ReactNode } from "react";

export type StatusVariant = "ready" | "warning" | "busy" | "info" | "neutral";

export interface StatusPillProps {
  variant?: StatusVariant;
  className?: string;
  children: ReactNode;
}

export function StatusPill({
  variant = "info",
  className = "",
  children,
}: StatusPillProps) {
  return (
    <span className={`status-pill ${variant} ${className}`.trim()}>
      {children}
    </span>
  );
}
