import { TerminalSettingsDialog } from "./TerminalSettingsDialog";

export function DataSettingsDialog({ onClose }: { onClose: () => void }) {
  return <TerminalSettingsDialog onClose={onClose} initialTab="profile" />;
}
