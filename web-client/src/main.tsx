import { createRoot } from "react-dom/client";
import { HallzeeProvider } from "./app/HallzeeProvider";
import { Dashboard } from "./ui/Dashboard";
import { RoomStation } from "./ui/station/RoomStation";
import { JoinPage } from "./ui/station/JoinPage";
import { appRoute } from "./domain/RoomLinks";
import "./styles.css";

const route = appRoute(new URL(window.location.href));

createRoot(document.getElementById("root")!).render(
  route.type === "station" ? (
    <RoomStation roomCode={route.roomCode} />
  ) : route.type === "join" ? <JoinPage invalidCode={route.invalidCode} /> : (
    <HallzeeProvider>
      <Dashboard />
    </HallzeeProvider>
  ),
);
