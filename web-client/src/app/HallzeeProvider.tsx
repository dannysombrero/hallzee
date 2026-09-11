import {
  createContext,
  useContext,
  useEffect,
  useState,
  useSyncExternalStore,
  type ReactNode,
} from "react";
import { ApplicationController, initialSnapshot } from "./ApplicationController";
const Context = createContext<ApplicationController | null>(null);
export function HallzeeProvider({ children }: { children: ReactNode }) {
  const [controller, setController] = useState<ApplicationController | null>(null);
  useEffect(() => {
    const instance = new ApplicationController();
    setController(instance);
    void instance.start();
    return () => instance.stop();
  }, []);
  return <Context.Provider value={controller}>{children}</Context.Provider>;
}
const noop = () => () => {};
const initial = () => initialSnapshot;
export function useHallzee() {
  const controller = useContext(Context);
  const state = useSyncExternalStore(
    controller?.subscribe ?? noop,
    controller?.getSnapshot ?? initial,
  );
  return { controller, state };
}
