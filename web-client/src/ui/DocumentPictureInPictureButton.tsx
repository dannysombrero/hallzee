import { useEffect, useState } from "react";
import { createPortal } from "react-dom";
import { PictureInPicture2 } from "lucide-react";
import { ProjectionView, type ProjectionProps } from "./ProjectionView";
interface PiPWindow extends Window {
  documentPictureInPicture?: {
    requestWindow(options: { width: number; height: number }): Promise<Window>;
  };
}
export function DocumentPictureInPictureButton({
  projection,
  fallback,
}: {
  projection: ProjectionProps;
  fallback: () => void;
}) {
  const [pip, setPip] = useState<Window | null>(null);
  useEffect(
    () => () => {
      pip?.close();
    },
    [pip],
  );
  async function open() {
    const api = (window as PiPWindow).documentPictureInPicture;
    if (!api) {
      fallback();
      return;
    }
    if (pip && !pip.closed) {
      pip.focus();
      return;
    }
    try {
      const next = await api.requestWindow({ width: 340, height: 180 });
      for (const node of document.querySelectorAll('link[rel="stylesheet"]')) {
        const href = (node as HTMLLinkElement).href;
        if (new URL(href).origin === location.origin) {
          const link = next.document.createElement("link");
          link.rel = "stylesheet";
          link.href = href;
          next.document.head.append(link);
        }
      }
      // Vite uses inline styles in development; all originate from this application.
      if (import.meta.env.DEV)
        for (const node of document.querySelectorAll("style[data-vite-dev-id]"))
          next.document.head.append(node.cloneNode(true));
      next.document.title = "Hallzee pass status";
      next.document.body.className = "pip-body";
      next.addEventListener("pagehide", () => setPip(null), { once: true });
      setPip(next);
    } catch {
      fallback();
    }
  }
  return (
    <>
      <button className="secondary" onClick={() => void open()}>
        <PictureInPicture2 size={17} />
        Mini window
      </button>
      {pip && !pip.closed && createPortal(<ProjectionView {...projection} />, pip.document.body)}
    </>
  );
}
