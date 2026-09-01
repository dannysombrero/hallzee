"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type PropsWithChildren,
} from "react";
import { discoverableTerminals, initialTrips } from "./mockData";
import type {
  ActiveTrip,
  ModalView,
  TerminalDevice,
  TerminalSettings,
  TerminalState,
  Trip,
  View,
} from "./types";

const nowLabel = () =>
  new Date().toLocaleTimeString([], { hour: "numeric", minute: "2-digit" });

export interface HallzeeContextValue {
  view: View;
  setView: (view: View) => void;
  activeModal: ModalView | null;
  openModal: (modal: ModalView) => void;
  closeModal: () => void;
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

export function HallzeeProvider({ children }: PropsWithChildren) {
  const [activeModal, setActiveModal] = useState<ModalView | null>(null);
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
  const [terminalSettings, setTerminalSettings] = useState<TerminalSettings>({
    name: "ESP32-HALLZEE-204",
    maxStudentIdLength: 8,
  });
  const [toast, setToast] = useState<string | null>(null);
  const [discoveredTerminals, setDiscoveredTerminals] = useState<TerminalDevice[]>([]);
  const [connectingTerminalId, setConnectingTerminalId] = useState<string | null>(null);

  const discoveryTimerRef = useRef<number | null>(null);
  const prevTerminalStateRef = useRef<TerminalState | null>(null);

  const view: View = activeModal ?? "dashboard";

  const openModal = useCallback((modal: ModalView) => {
    setActiveModal(modal);
  }, []);

  const closeModal = useCallback(() => {
    setActiveModal(null);
  }, []);

  const setView = useCallback((targetView: View) => {
    if (targetView === "dashboard") {
      setActiveModal(null);
    } else {
      setActiveModal(targetView);
    }
  }, []);

  const searchOpen = activeModal === "search";
  const setSearchOpen = useCallback((open: boolean) => {
    if (open) {
      setActiveModal("search");
    } else {
      setActiveModal((current) => (current === "search" ? null : current));
    }
  }, []);

  useEffect(() => {
    if (!isOccupied || terminalState !== "connected") return;
    const timer = window.setInterval(
      () => setActiveTrip((trip) => ({ ...trip, elapsedSeconds: trip.elapsedSeconds + 1 })),
      1000,
    );
    return () => window.clearInterval(timer);
  }, [isOccupied, terminalState]);

  const showToast = useCallback((message: string) => {
    setToast(message);
    window.setTimeout(() => setToast(null), 3500);
  }, []);

  const findTerminals = useCallback(() => {
    // Capture only stable prior connection state (not transient states like syncing)
    prevTerminalStateRef.current =
      terminalState === "connected" || terminalState === "syncing" ? "connected" : "disconnected";
    setActiveModal("search");
    setTerminalState("discovering");
    setDiscoveredTerminals([]);

    if (discoveryTimerRef.current) {
      window.clearTimeout(discoveryTimerRef.current);
      discoveryTimerRef.current = null;
    }

    discoveryTimerRef.current = window.setTimeout(() => {
      setDiscoveredTerminals(discoverableTerminals);
      setTerminalState((current) => {
        // Only restore prior connection state if still in discovering state
        if (current === "discovering") {
          return prevTerminalStateRef.current === "connected" ? "connected" : "disconnected";
        }
        return current;
      });
      discoveryTimerRef.current = null;
    }, 900);
  }, [terminalState]);

  useEffect(() => {
    if (activeModal !== "search") {
      if (discoveryTimerRef.current) {
        window.clearTimeout(discoveryTimerRef.current);
        discoveryTimerRef.current = null;
      }
      setTerminalState((current) => {
        if (current === "discovering") {
          return prevTerminalStateRef.current === "connected" ? "connected" : "disconnected";
        }
        return current;
      });
    }
  }, [activeModal]);

  const connectTerminal = useCallback(
    (terminal: TerminalDevice) => {
      setConnectingTerminalId(terminal.id);
      setTerminalState("connecting");
      window.setTimeout(() => {
        setConnectedTerminalName(terminal.name);
        setTerminalId(terminal.id);
        setTerminalSettings((settings) => ({ ...settings, name: terminal.id }));
        setConnectingTerminalId(null);
        setActiveModal(null);
        setTerminalState("connected");
        setLastSyncTime(`Today, ${nowLabel()} (Just now)`);
        showToast(`Connected to ${terminal.name}!`);
      }, 900);
    },
    [showToast],
  );

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

    const depart = nowLabel();
    const newActive: ActiveTrip = {
      studentId: "8821",
      studentName: "Elena Rostova",
      destination: "Hallway Restroom (East)",
      departTime: depart,
      elapsedSeconds: 0,
    };
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

  const exportCsv = useCallback(
    () => showToast("Exported hallzee_trips_room204.csv with student logs."),
    [showToast],
  );

  const importRoster = useCallback(
    (fileName = "Chemistry_Period3_Students.csv") => {
      setRosterFileName(fileName);
      showToast("Roster updated: Attached student names to IDs.");
    },
    [showToast],
  );

  const applyTerminalSettings = useCallback(
    (settings: TerminalSettings) => {
      setTerminalSettings(settings);
      showToast(`Terminal settings applied. Student IDs allow ${settings.maxStudentIdLength} digits.`);
    },
    [showToast],
  );

  const value = useMemo<HallzeeContextValue>(
    () => ({
      view,
      setView,
      activeModal,
      openModal,
      closeModal,
      terminalState,
      connectedTerminalName,
      terminalId,
      isOccupied,
      activeTrip,
      lastSyncTime,
      trips,
      rosterFileName,
      terminalSettings,
      toast,
      searchOpen,
      setSearchOpen,
      discoveredTerminals,
      connectingTerminalId,
      findTerminals,
      connectTerminal,
      syncNow,
      toggleOccupancy,
      toggleStudentName,
      disconnect,
      exportCsv,
      importRoster,
      applyTerminalSettings,
    }),
    [
      view,
      setView,
      activeModal,
      openModal,
      closeModal,
      terminalState,
      connectedTerminalName,
      terminalId,
      isOccupied,
      activeTrip,
      lastSyncTime,
      trips,
      rosterFileName,
      terminalSettings,
      toast,
      searchOpen,
      setSearchOpen,
      discoveredTerminals,
      connectingTerminalId,
      findTerminals,
      connectTerminal,
      syncNow,
      toggleOccupancy,
      toggleStudentName,
      disconnect,
      exportCsv,
      importRoster,
      applyTerminalSettings,
    ],
  );

  return <HallzeeContext.Provider value={value}>{children}</HallzeeContext.Provider>;
}

export function useHallzee() {
  const value = useContext(HallzeeContext);
  if (!value) throw new Error("useHallzee must be used inside HallzeeProvider");
  return value;
}
