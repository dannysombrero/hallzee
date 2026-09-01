"use client";

import { HallzeeProvider } from "./hallzee/HallzeeProvider";
import AppShell from "./hallzee/components/AppShell";

export default function HallzeeApp() {
  return (
    <HallzeeProvider>
      <AppShell />
    </HallzeeProvider>
  );
}
