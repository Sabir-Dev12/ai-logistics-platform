import { useCallback, useEffect, useRef, useState } from "react";

// Lists and details have separate lifecycles: no N+1 detail loading in a table.
export function useBusinessResource(source, collection, filters = {}) {
  const filterKey = JSON.stringify(source.remote ? filters : {});
  const [state, setState] = useState({ [collection]: [], clients: [], groups: [], phase: "loading", totalCount: 0, key: filterKey });
  const [detail, setDetail] = useState({ phase: "idle", row: null, id: null });
  const [busy, setBusy] = useState(false), [error, setError] = useState(null), [uncertain, setUncertain] = useState(false);
  const mounted = useRef(false), load = useRef(null), detailLoad = useRef(null), mutation = useRef(null);
  const reload = useCallback(async () => {
    if (mutation.current) return;
    load.current?.abort(); const controller = new AbortController(); load.current = controller;
    setState(value => ({ ...value, phase: "loading", key: filterKey }));
    try {
      const result = await source.list({ ...JSON.parse(filterKey), signal: controller.signal });
      if (mounted.current && !controller.signal.aborted) {
        setState(value => ({ ...value, ...(Array.isArray(result) ? { [collection]: result } : result), phase: "ready", key: filterKey }));
        setUncertain(false); setError(null);
      }
    } catch (failure) { if (mounted.current && !controller.signal.aborted) setState(value => ({ ...value, phase: "error", listError: source.remote ? failure.message : undefined, key: filterKey })); }
  }, [source, collection, filterKey]);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; load.current?.abort(); detailLoad.current?.abort(); mutation.current?.abort(); }; }, [source]);
  useEffect(() => { void reload(); return () => load.current?.abort(); }, [reload]);
  const openDetail = useCallback(async (id) => {
    if (!source.remote) return;
    detailLoad.current?.abort(); const controller = new AbortController(); detailLoad.current = controller;
    setDetail({ phase: "loading", row: null, id });
    try { const row = await source.get(id, { signal: controller.signal }); if (mounted.current && !controller.signal.aborted) setDetail({ phase: "ready", row, id }); }
    catch (failure) { if (mounted.current && !controller.signal.aborted) setDetail({ phase: "error", row: null, id, error: failure.message }); }
  }, [source]);
  const execute = async (method, ...args) => {
    if (mutation.current || uncertain || state.phase !== "ready") return false;
    const controller = new AbortController(); mutation.current = controller; setBusy(true); setError(null);
    let saved = false;
    try {
      const result = await source[method](...args, { signal: controller.signal });
      if (!mounted.current || controller.signal.aborted) return false;
      if (source.remote) { if (result.row) setDetail({ phase: "ready", id: result.updatedId, row: result.row }); saved = true; }
      else if (collection === "clients") setState(value => ({ ...value, clients: method === "remove" ? value.clients.filter(row => row.keyClient !== result) : method === "create" ? [result, ...value.clients] : value.clients.map(row => row.keyClient === result.keyClient ? result : row) }));
      else setState(value => ({ ...value, ...result, phase: "ready" }));
      return result;
    } catch (failure) { if (mounted.current && !controller.signal.aborted) { const known = source.remote || ["ClientRuleError", "QuotationError", "ScaleError"].includes(failure.constructor?.name); setError(known && failure.message || "L’opération n’a pas pu aboutir. Réessayez."); setUncertain(Boolean(failure.uncertain)); } return false; }
    finally { if (mutation.current === controller) { mutation.current = null; if (mounted.current) { setBusy(false); if (saved) void reload(); } } }
  };
  return { ...state, ...(state.key !== filterKey ? { phase: "loading" } : {}), remote: Boolean(source.remote), detail, openDetail, totalCount: source.remote ? state.totalCount : state[collection].length,
    busy, error, actionError: error, uncertain, execute, reload, clearError: () => setError(null), clearActionError: () => setError(null) };
}
