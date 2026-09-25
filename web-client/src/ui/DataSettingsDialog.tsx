import { TerminalSettingsDialog } from "./TerminalSettingsDialog";

export function DataSettingsDialog({ onClose, offlineReady }: { onClose: () => void; offlineReady?: boolean }) {
  return <TerminalSettingsDialog onClose={onClose} initialTab="profile" offlineReady={offlineReady} />;
}
