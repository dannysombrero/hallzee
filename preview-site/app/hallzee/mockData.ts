import type { TerminalDevice, Trip } from "./types";

export const initialTrips: Trip[] = [
  { id: "TRIP-1049", studentId: "9042", studentName: "Marcus Sterling", destination: "Hallway Restroom (East)", departTime: "9:20 AM", returnTime: "--:--", durationSeconds: 380, status: "OCCUPIED", date: "Today" },
  { id: "TRIP-1048", studentId: "8821", studentName: "Elena Rostova", destination: "Hallway Restroom (East)", departTime: "9:08 AM", returnTime: "9:12 AM", durationSeconds: 242, status: "COMPLETED", date: "Today" },
  { id: "TRIP-1047", studentId: "6219", studentName: "Devon Patel", destination: "Hallway Restroom (East)", departTime: "8:44 AM", returnTime: "8:48 AM", durationSeconds: 258, status: "COMPLETED", date: "Today" },
  { id: "TRIP-1046", studentId: "4401", studentName: "", destination: "Hallway Restroom (East)", departTime: "8:21 AM", returnTime: "8:25 AM", durationSeconds: 230, status: "COMPLETED", date: "Today" },
  { id: "TRIP-1045", studentId: "1094", studentName: "Zoe Washington", destination: "Hallway Restroom (East)", departTime: "8:05 AM", returnTime: "8:09 AM", durationSeconds: 245, status: "COMPLETED", date: "Today" },
  { id: "TRIP-1044", studentId: "9912", studentName: "Aaliyah Chen", destination: "Hallway Restroom (East)", departTime: "Yesterday, 2:15 PM", returnTime: "2:19 PM", durationSeconds: 260, status: "COMPLETED", date: "Yesterday" },
];

export const discoverableTerminals: TerminalDevice[] = [
  { id: "ESP32-HALLZEE-204", name: "Room 204 Door Kiosk (East)", rssi: -48, signal: "Strong", status: "Available" },
  { id: "ESP32-HALLZEE-205", name: "Room 205 Biology Door", rssi: -72, signal: "Moderate", status: "In Use (Rm 205)" },
  { id: "ESP32-HALLZEE-HALL-E", name: "East Wing Main Hallway", rssi: -84, signal: "Weak", status: "Available" },
];
