import type { HTMLAttributes } from "react";

type IconProps = HTMLAttributes<HTMLSpanElement>;
const icon = (symbol: string) => function Icon({ className = "", ...props }: IconProps) {
  return <span aria-hidden="true" className={`inline-flex items-center justify-center leading-none ${className}`} {...props}>{symbol}</span>;
};

export const ArrowUpDown = icon("↕");
export const Bluetooth = icon("ᛒ");
export const Calendar = icon("▣");
export const ChevronRight = icon("›");
export const Clock = icon("◷");
export const Download = icon("↓");
export const Droplets = icon("◉");
export const FileSpreadsheet = icon("▦");
export const History = icon("↶");
export const Laptop = icon("▰");
export const Loader2 = icon("◌");
export const Minus = icon("−");
export const PauseCircle = icon("Ⅱ");
export const Radio = icon("◉");
export const RefreshCw = icon("↻");
export const Save = icon("▣");
export const Search = icon("⌕");
export const Settings = icon("⚙");
export const ShieldCheck = icon("✓");
export const Signal = icon("▥");
export const Square = icon("□");
export const Upload = icon("↑");
export const UserCheck = icon("✓");
export const Users = icon("●●");
export const UserX = icon("!");
export const WifiOff = icon("∅");
export const X = icon("×");
