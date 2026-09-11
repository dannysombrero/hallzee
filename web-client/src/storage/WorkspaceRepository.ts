import { LocalDatabase, request } from "./LocalDatabase";
import {
  defaultPolicy,
  type Workspace,
  type Policy,
  type BellPeriod,
  type ScheduleException,
} from "./schema";
import { validatePolicy } from "../domain/PolicyScheduleService";
import { HallzeeError } from "../app/errors";
export class WorkspaceRepository {
  constructor(private db: LocalDatabase) {}
  async ensure() {
    const existing = await this.db.all("workspaces");
    if (existing.length) return existing[0];
    const now = new Date().toISOString();
    const workspace: Workspace = {
      workspaceId: crypto.randomUUID(),
      name: "My classroom",
      teacher: "",
      school: "",
      room: "",
      timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone,
      createdAtUtc: now,
      updatedAtUtc: now,
    };
    await this.db.transaction(
      ["workspaces", "policy_rules", "app_meta"],
      "readwrite",
      async (tx) => {
        await request(tx.objectStore("workspaces").add(workspace));
        await request(tx.objectStore("policy_rules").add(defaultPolicy(workspace.workspaceId)));
        await request(
          tx
            .objectStore("app_meta")
            .put({ key: "activeWorkspaceId", value: workspace.workspaceId }),
        );
      },
    );
    return workspace;
  }
  async save(workspace: Workspace) {
    if (
      !workspace.name.trim() ||
      [workspace.name, workspace.teacher, workspace.school, workspace.room].some(
        (s) => s.length > 200,
      )
    )
      throw new HallzeeError(
        "INVALID_WORKSPACE",
        "Classroom fields must be between 1 and 200 characters.",
      );
    try {
      new Intl.DateTimeFormat("en", { timeZone: workspace.timeZone });
    } catch {
      throw new HallzeeError("INVALID_TIMEZONE");
    }
    await this.db.put("workspaces", { ...workspace, updatedAtUtc: new Date().toISOString() });
  }
  async savePolicy(policy: Policy, periods: BellPeriod[], exceptions: ScheduleException[]) {
    validatePolicy(policy, periods, exceptions);
    await this.db.transaction(
      ["policy_rules", "bell_periods", "schedule_exceptions"],
      "readwrite",
      async (tx) => {
        const old = await request<Policy>(tx.objectStore("policy_rules").get(policy.workspaceId));
        await request(
          tx
            .objectStore("policy_rules")
            .put({
              ...policy,
              revision: (old?.revision ?? 0) + 1,
              appliedRevision: old?.appliedRevision ?? null,
              appliedDate: old?.appliedDate ?? null,
            }),
        );
        for (const name of ["bell_periods", "schedule_exceptions"]) {
          const store = tx.objectStore(name);
          const rows = await request(store.getAll());
          for (const row of rows)
            if (row.workspaceId === policy.workspaceId)
              await request(
                store.delete([
                  row.workspaceId,
                  name === "bell_periods" ? row.scheduleId : row.date,
                ]),
              );
        }
        for (const p of periods) await request(tx.objectStore("bell_periods").put(p));
        for (const e of exceptions) await request(tx.objectStore("schedule_exceptions").put(e));
      },
    );
  }
}
