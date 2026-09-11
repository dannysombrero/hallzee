export function migrate(db: IDBDatabase, oldVersion: number) {
  if (oldVersion < 1) {
    db.createObjectStore("app_meta", { keyPath: "key" });
    db.createObjectStore("workspaces", { keyPath: "workspaceId" });
    const terminals = db.createObjectStore("terminals", { keyPath: "terminalId" });
    terminals.createIndex("assignment", "assignedWorkspaceId", { unique: true });
    db.createObjectStore("credentials", { keyPath: "terminalId" });
    const trips = db.createObjectStore("trips", { keyPath: ["terminalId", "tripId"] });
    trips.createIndex("terminalDate", ["terminalId", "tripDate", "tripId"]);
    trips.createIndex("workspaceDate", ["receivedWorkspaceId", "tripDate", "tripId"]);
    trips.createIndex("studentDate", ["receivedWorkspaceId", "studentId", "tripDate", "tripId"]);
    db.createObjectStore("sync_state", { keyPath: "terminalId" });
    const roster = db.createObjectStore("roster_students", {
      keyPath: ["workspaceId", "studentId"],
    });
    roster.createIndex("workspace", "workspaceId");
    db.createObjectStore("roster_enrollments", {
      keyPath: ["workspaceId", "studentId", "classSection"],
    });
    db.createObjectStore("policy_rules", { keyPath: "workspaceId" });
    db.createObjectStore("bell_periods", { keyPath: ["workspaceId", "scheduleId"] });
    db.createObjectStore("schedule_exceptions", { keyPath: ["workspaceId", "date"] });
  }
}
