import { useEffect, useMemo, useRef, useState } from "react";
import { createSignalRService } from "../services/signalRService";
import { getChatAccessToken, subscribeChatAccessToken } from "../services/chatAccessToken";
import { ChatTransportError } from "../services/chatErrors";
import { authStore } from "../Auth/authState";

export function useChatSignalR({ enabled = true, url = import.meta.env.VITE_CHAT_HUB_URL || "/hubs/chat",
  accessTokenFactory = getChatAccessToken, serviceFactory = createSignalRService } = {}) {
  const service = useRef(null);
  const [sessionRevision, setSessionRevision] = useState(0);
  const [snapshot, setSnapshot] = useState({ status: enabled ? "connecting" : "demo", error: null });
  useEffect(() => subscribeChatAccessToken(() => setSessionRevision((value) => value + 1)), []);
  useEffect(() => {
    if (!enabled) { setSnapshot({ status: "demo", error: null }); return; }
    let current;
    try { current = serviceFactory({ url, accessTokenFactory }); }
    catch { setSnapshot({ status: "failed", error: new ChatTransportError("chat_connection_failed") }); return; }
    service.current = current;
    const unsubscribe = current.subscribe((snapshot) => { setSnapshot(snapshot); if (snapshot.status === "unauthorized") authStore.invalidate(); });
    void current.connect().catch(() => {}); // The service publishes a safe error in its snapshot.
    return () => {
      unsubscribe();
      if (service.current === current) service.current = null;
      void current.disconnect().catch(() => {});
    };
  }, [enabled, url, accessTokenFactory, serviceFactory, sessionRevision]);
  const transport = useMemo(() => ({
    stream(request) {
      if (!service.current) throw new ChatTransportError("chat_connection_lost");
      return service.current.stream(request);
    },
  }), []);
  return { ...snapshot, transport, ready: !enabled || snapshot.status === "connected",
    reconnect: () => { void service.current?.connect().catch(() => {}); } };
}
