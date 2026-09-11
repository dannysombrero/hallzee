import { durationLabel } from "../sync/TerminalClock";
import type { Decision } from "../storage/schema";
export interface ProjectionProps {
  fresh: boolean;
  occupiedCount: number;
  capacity: number;
  elapsedSeconds: number[];
  periodLabel: string;
  windowDecision: Decision;
}
export function ProjectionView(props: ProjectionProps) {
  return (
    <div
      className={`projection ${!props.fresh ? "unknown" : props.occupiedCount >= props.capacity ? "occupied" : "available"}`}
    >
      <span className="eyebrow">{props.periodLabel || "Hallzee"}</span>
      <strong>
        {!props.fresh ? "Status unknown" : `${props.occupiedCount} of ${props.capacity} passes out`}
      </strong>
      <span>
        {props.fresh && props.elapsedSeconds.length
          ? props.elapsedSeconds.map(durationLabel).join(" · ")
          : props.fresh
            ? "Pass available"
            : "Reconnect to refresh"}
      </span>
      <small>Current window: {props.windowDecision}</small>
    </div>
  );
}
