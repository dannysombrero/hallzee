"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from "react";
import { discoverableTerminals, initialTrips } from "./mockData";
import type { ActiveTrip, TerminalDevice, TerminalSettings, TerminalState, Trip, View } from "./types";

interface HallzeeContextValue {
  view: View;
  setView: (view: View) => void;
  terminalState: TerminalState;
  connectedTerminalName: string;
  terminalId: string;
  isOccupied: boolean;
  activeTrip: ActiveTrip;
  lastSyncTime: string;
  trips: Trip[];
  rosterFileName: string;
  terminalSettings: TerminalSettings;
  toast: string | null;
  searchOpen: boolean;
  setSearchOpen: (open: boolean) => void;
  discoveredTerminals: TerminalDevice[];
  connectingTerminalId: string | null;
  findTerminals: () => void;
  connectTerminal: (terminal: TerminalDevice) => void;
  syncNow: () => void;
  toggleOccupancy: () => void;
  toggleStudentName: () => void;
  disconnect: () => void;
  exportCsv: () => void;
  importRoster: (fileName?: string) => void;
  applyTerminalSettings: (settings: TerminalSettings) => void;
}

const HallzeeContext = createContext<HallzeeContextValue | null>(null);

const nowLabel = () => new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });

export function HallzeeProvider({ children }: { children: React.ReactNode }) {
  const [view, setView] = useState<View>("dashboard");
  const [terminalState, setTerminalState] = useState<TerminalState>("connected");
  const [connectedTerminalName, setConnectedTerminalName] = useState("Room 204 Door Kiosk (East-204)");
  const [terminalId, setTerminalId] = useState("ESP32-HALLZEE-204");
  const [isOccupied, setIsOccupied] = useState(true);
  const [activeTrip, setActiveTrip] = useState<ActiveTrip>({
    studentId: "9042",
    studentName: "Marcus Sterling",
    destination: "Hallway Restroom (East)",
    departTime: "9:20 AM",
    elapsedSeconds: 380,
  });
  const [lastSyncTime, setLastSyncTime] = useState("Today, 9:22 AM (4 mins ago)");
  const [trips, setTrips] = useState<Trip[]>(initialTrips);
  const [rosterFileName, setRosterFileName] = useState("Chemistry_Period3_Students.csv");
  const [terminalSettings, setTerminalSettings] = useState<TerminalSettings>({ name: "ESP32-HALLZEE-204", maxStudentIdLength: 8 });
  const [toast, setToast] = useState<string | null>(null);
  const [searchOpen, setSearchOpen] = useState(false);
  const [discoveredTerminals, setDiscoveredTerminals] = useState<TerminalDevice[]>([]);
  const [connectingTerminalId, setConnectingTerminalId] = useState<string | null>(null);

  // Refs to manage discovery timers and previous state so that cancelling discovery
  // does not drop an existing connection when a pending timeout completes.
  const discoveryTimerRef = useRef<number | null>(null);
  const prevTerminalStateRef = useRef<TerminalState | null>(null);
  const searchOpenRef = useRef<boolean>(searchOpen);

  useEffect(() => {
    searchOpenRef.current = searchOpen;
  }, [searchOpen]);

  useEffect(() => {
    if (!isOccupied || terminalState !== "connected") return;
    const timer = window.setInterval(() => setActiveTrip((trip) => ({ ...trip, elapsedSeconds: trip.elapsedSeconds + 1 })), 1000);
    return () => window.clearInterval(timer);
  }, [isOccupied, terminalState]);

  const showToast = useCallback((message: string) => {
    setToast(message);
    window.setTimeout(() => setToast(null), 3500);
  }, []);

  const findTerminals = useCallback(() => {
    // Remember the previous connection state so discovery can be cancelled safely.
    prevTerminalStateRef.current = terminalState;
    setSearchOpen(true);
    setTerminalState("discovering");
    setDiscoveredTerminals([]);

    if (discoveryTimerRef.current) {
      window.clearTimeout(discoveryTimerRef.current);
      discoveryTimerRef.current = null;
    }

    discoveryTimerRef.current = window.setTimeout(() => {
      // Only apply discovery results if the search is still open. This prevents
      // a late timeout from unconditionally dropping an existing connection.
      if (!searchOpenRef.current) return;
      setDiscoveredTerminals(discoverableTerminals);
      // Restore to connected if we were connected before opening discovery,
      // otherwise indicate a disconnected post-discovery state.
      setTerminalState(prevTerminalStateRef.current === "connected" ? "connected" : "disconnected");
      discoveryTimerRef.current = null;
    }, 900);
  }, [terminalState]);

  // Keep timers and refs consistent when the user explicitly closes the search.
  useEffect(() => {
    if (!searchOpen) {
      if (discoveryTimerRef.current) {
        window.clearTimeout(discoveryTimerRef.current);
        discoveryTimerRef.current = null;
      }
      // If discovery was opened from a connected state, ensure we remain connected.
      if (prevTerminalStateRef.current === "connected") setTerminalState("connected");
    }
  }, [searchOpen]);

  const connectTerminal = useCallback((terminal: TerminalDevice) => {
    setConnectingTerminalId(terminal.id);
    setTerminalState("connecting");
    window.setTimeout(() => {
      setConnectedTerminalName(terminal.name);
      setTerminalId(terminal.id);
      setTerminalSettings((settings) => ({ ...settings, name: terminal.id }));
      setConnectingTerminalId(null);
      setSearchOpen(false);
      setTerminalState("connected");
      setLastSyncTime(`Today, ${nowLabel()} (Just now)`);
      showToast(`Connected to ${terminal.name}!`);
    }, 900);
  }, [showToast]);

  const syncNow = useCallback(() => {
    if (terminalState !== "connected") return;
    setTerminalState("syncing");
    window.setTimeout(() => {
      setTerminalState("connected");
      setLastSyncTime(`Today, ${nowLabel()} (Just now)`);
      showToast("Manual sync complete. All logs and clock aligned.");
    }, 1000);
  }, [showToast, terminalState]);

  const toggleOccupancy = useCallback(() => {
    if (isOccupied) {
      const completed: Trip = {
        id: `TRIP-${Date.now().toString().slice(-4)}`,
        studentId: activeTrip.studentId,
        studentName: activeTrip.studentName,
        destination: activeTrip.destination,
        departTime: activeTrip.departTime,
        returnTime: nowLabel(),
        durationSeconds: activeTrip.elapsedSeconds,
        status: "COMPLETED",
        date: "Today",
      };
      setTrips((items) => [completed, ...items.filter((trip) => trip.status !== "OCCUPIED")]);
      setIsOccupied(false);
      showToast(`${activeTrip.studentName || `Student #${activeTrip.studentId}`} returned to classroom.`);
      return;
    }

    // When marking occupied, create an activeTrip and insert an OCCUPIED record
    // into the trip history so the recent activity and filters reflect the state.
    const depart = nowLabel();
    const newActive: ActiveTrip = { studentId: "8821", studentName: "Elena Rostova", destination: "Hallway Restroom (East)", departTime: depart, elapsedSeconds: 0 };
    const occupiedTrip: Trip = {
      id: `TRIP-${Date.now().toString().slice(-4)}`,
      studentId: newActive.studentId,
      studentName: newActive.studentName,
      destination: newActive.destination,
      departTime: newActive.departTime,
      returnTime: "--:--",
      durationSeconds: 0,
      status: "OCCUPIED",
      date: "Today",
    };

    setActiveTrip(newActive);
    setTrips((items) => [occupiedTrip, ...items.filter((t) => t.status !== "OCCUPIED")]);
    setIsOccupied(true);
    showToast("Hall pass activated: Student marked out of class.");
  }, [activeTrip, isOccupied, showToast]);

  const toggleStudentName = useCallback(() => {
    setActiveTrip((trip) => ({ ...trip, studentName: trip.studentName ? "" : "Marcus Sterling" }));
    showToast("Toggled student-name availability for ID fallback testing.");
  }, [showToast]);

  const disconnect = useCallback(() => {
    setTerminalState("disconnected");
    showToast("Terminal disconnected.");
  }, [showToast]);

  const exportCsv = useCallback(() => showToast("Exported hallzee_trips_room204.csv with student logs."), [showToast]);
  const importRoster = useCallback((fileName = "Chemistry_Period3_Students.csv") => {
    setRosterFileName(fileName);
    showToast("Roster updated: Attached student names to IDs.");
  }, [showToast]);
  const applyTerminalSettings = useCallback((settings: TerminalSettings) => {
    setTerminalSettings(settings);
    showToast(`Terminal settings applied. Student IDs allow ${settings.maxStudentIdLength} digits.`);
  }, [showToast]);

  const value = useMemo<HallzeeContextValue>(() => ({
    view, setView, terminalState, connectedTerminalName, terminalId, isOccupied, activeTrip, lastSyncTime,
    trips, rosterFileName, terminalSettings, toast, searchOpen, setSearchOpen, discoveredTerminals,
    connectingTerminalId, findTerminals, connectTerminal, syncNow, toggleOccupancy, toggleStudentName,
    disconnect, exportCsv, importRoster, applyTerminalSettings,
  }), [view, terminalState, connectedTerminalName, terminalId, isOccupied, activeTrip, lastSyncTime, trips,
    rosterFileName, terminalSettings, toast, searchOpen, discoveredTerminals, connectingTerminalId,
    findTerminals, connectTerminal, syncNow, toggleOccupancy, toggleStudentName, disconnect, exportCsv,
    importRoster, applyTerminalSettings]);

  return <HallzeeContext.Provider value={value}>{children}</HallzeeContext.Provider>;
}

export function useHallzee() {
  const value = useContext(HallzeeContext);
  if (!value) throw new Error("useHallzee must be used inside HallzeeProvider");
  return value;
}
