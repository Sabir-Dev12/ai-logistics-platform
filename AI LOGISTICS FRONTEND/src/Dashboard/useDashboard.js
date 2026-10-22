import { useCallback, useEffect, useRef, useState } from "react";
import { DashboardError } from "../services/dashboardService";
export function useDashboard(source, { period = "30", entityType = "" } = {}) {
  const key = `${period}:${entityType}`;
  const [state, setState] = useState({ key, phase: "loading", activities: [], asOf: new Date() });
  const request = useRef(null);
  const reload = useCallback(async () => {
    request.current?.abort(); const controller = new AbortController(); request.current = controller;
    setState({ key: `${period}:${entityType}`, phase: "loading", activities: [], asOf: new Date() });
    try {
      const result = await source.list({ signal: controller.signal, period, entityType });
      const snapshot = Array.isArray(result) ? { activities: result, asOf: new Date() } : result;
      if (!controller.signal.aborted) setState({ ...snapshot, key: `${period}:${entityType}`, phase: "ready" });
    } catch (error) {
      if (!controller.signal.aborted) setState({ key: `${period}:${entityType}`, phase: "error", activities: [], asOf: new Date(), error: error instanceof DashboardError ? error : null });
    }
  }, [source, period, entityType]);
  useEffect(() => { void reload(); return () => request.current?.abort(); }, [reload]);
  return state.key === key ? { ...state, reload } : { key, phase: "loading", activities: [], asOf: new Date(), reload };
}
