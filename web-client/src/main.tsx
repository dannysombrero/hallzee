import { createRoot } from "react-dom/client";
import { HallzeeProvider } from "./app/HallzeeProvider";
import { Dashboard } from "./ui/Dashboard";
import { RoomStation } from "./ui/station/RoomStation";
import "./styles.css";

function getRoute(): { type: "station"; roomCode: string } | { type: "dashboard" } {
  if (typeof window === "undefined") return { type: "dashboard" };
  const pathname = window.location.pathname;
  const search = new URLSearchParams(window.location.search);

  const match = pathname.match(/^\/pass\/([A-Za-z0-9_-]+)/);
  if (match && match[1]) {
    return { type: "station", roomCode: match[1].toUpperCase() };
  }
  const queryRoom = search.get("room") || search.get("pass");
  if (queryRoom) {
    return { type: "station", roomCode: queryRoom.toUpperCase() };
  }

  return { type: "dashboard" };
}

const route = getRoute();

createRoot(document.getElementById("root")!).render(
  route.type === "station" ? (
    <RoomStation roomCode={route.roomCode} />
  ) : (
    <HallzeeProvider>
      <Dashboard />
    </HallzeeProvider>
  ),
);

