import { useEffect, useRef, useState } from "react";
import { AccountError } from "../services/accountService";
const safeFailure = (failure) => failure instanceof AccountError ? failure : new AccountError("unavailable", "Le service du compte est indisponible. Réessayez.");
export function useAccount(source) {
  const [revision, setRevision] = useState(0), [phase, setPhase] = useState("loading"), [profile, setProfile] = useState(null), [busy, setBusy] = useState(null), [error, setError] = useState(null);
  const mounted = useRef(false), action = useRef(null);
  useEffect(() => {
    mounted.current = true; const controller = new AbortController();
    setPhase("loading"); setProfile(null); setError(null); setBusy(null);
    Promise.resolve().then(() => source.getProfile({ signal: controller.signal })).then((value) => {
      if (!controller.signal.aborted) { setProfile(value); setPhase("ready"); }
    }).catch((failure) => { if (!controller.signal.aborted) { setError(safeFailure(failure)); setPhase("error"); } });
    return () => { mounted.current = false; controller.abort(); action.current?.abort(); action.current = null; };
  }, [source, revision]);
  const mutate = async (operation, payload) => {
    if (action.current || phase !== "ready") return false;
    const controller = new AbortController(); action.current = controller; setBusy(operation); setError(null);
    try {
      if (operation === "password") await source.changePassword(payload, { signal: controller.signal });
      else await source.logout({ signal: controller.signal });
      if (!mounted.current || controller.signal.aborted) return false;
      if (operation === "logout") { setProfile(null); setPhase("disconnected"); }
      return true;
    } catch (failure) {
      if (mounted.current && !controller.signal.aborted) setError(safeFailure(failure)); return false;
    } finally { if (action.current === controller) { action.current = null; if (mounted.current) setBusy(null); } }
  };
  return { phase, profile, busy, error, clearError: () => setError(null), reload: () => { if (!action.current) setRevision((value) => value + 1); }, changePassword: (payload) => mutate("password", payload), logout: () => mutate("logout") };
}
