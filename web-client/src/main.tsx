import { createRoot } from "react-dom/client";
import { HallzeeProvider } from "./app/HallzeeProvider";
import { Dashboard } from "./ui/Dashboard";
import "./styles.css";
createRoot(document.getElementById("root")!).render(
  <HallzeeProvider>
    <Dashboard />
  </HallzeeProvider>,
);
