import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { createMockEdiRepository } from "./mockEdiRepository";
import {EdiApiError} from "../services/ediService";
import { EdiError } from "./mockEdiRepository";
export function useEdi(repository) {
  const source = useMemo(() => repository || createMockEdiRepository(), [repository]);
  const [state, setState] = useState({ analysis: null, stage: null, report: null, references: {}, aliases: {}, createdVoyages: [], createdDossiers: [], phase: "loading" });
  const [busy, setBusy] = useState(false), [error, setError] = useState(null), [uncertain,setUncertain]=useState(false);
  const mounted = useRef(false), load = useRef(null), mutation = useRef(null);
  const reload = useCallback(async () => {
    if (mutation.current) return;
    load.current?.abort(); const controller = new AbortController(); load.current = controller;
    setState((current) => ({ ...current, phase: "loading" })); setError(null);
    try { const result = await source.list({ signal: controller.signal }); if (mounted.current && !controller.signal.aborted) setState({ ...result, phase: "ready" }); }
    catch (failure) { if (mounted.current && !controller.signal.aborted) setState((current) => ({ ...current, phase: "error", listError:failure instanceof EdiApiError ? failure.message : undefined })); }
  }, [source]);
  useEffect(() => { mounted.current = true; void reload(); return () => { mounted.current = false; load.current?.abort(); mutation.current?.abort(); }; }, [reload]);
  const execute = async (method, ...args) => {
    if (mutation.current || uncertain && method!=="analyze" || state.phase !== "ready") return false;
    if(source.remote)setState(value=>({...value,...(method==="analyze"?{analysis:null}:{}),report:null}));
    const controller = new AbortController(); mutation.current = controller; setBusy(true); setError(null);
    try { const result = await source[method](...args, { signal: controller.signal }); if (!mounted.current || controller.signal.aborted) return false; setState({ ...result, phase: "ready" }); return result; }
    catch (failure) { if (mounted.current && !controller.signal.aborted) {setUncertain(value=>value||Boolean(failure.uncertain));setError(failure instanceof EdiError || failure instanceof EdiApiError ? failure.message : "L’opération n’a pas pu aboutir. Réessayez.");} return false; }
    finally { mutation.current = null; if (mounted.current) setBusy(false); }
  };
  return { ...state, remote:Boolean(source.remote), busy, error, uncertain, reload, execute, clearError: () => {if(!uncertain)setError(null);} };
}







