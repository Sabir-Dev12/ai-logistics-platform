import { useEffect, useMemo, useSyncExternalStore } from "react";
import PropTypes from "prop-types";
import { AuthContext } from "./AuthContext";
import { authStore } from "./authState";
export default function AuthProvider({ children, store = authStore }) {
  const snapshot = useSyncExternalStore(store.subscribe, store.getSnapshot);
  useEffect(() => {
    const check = () => store.getAccessToken();
    window.addEventListener("focus", check); document.addEventListener("visibilitychange", check);
    check();
    return () => { window.removeEventListener("focus", check); document.removeEventListener("visibilitychange", check); };
  }, [store]);
  const value = useMemo(() => ({ ...snapshot, login: store.login, logout: store.logout, request: store.request }), [snapshot, store]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
AuthProvider.propTypes = { children: PropTypes.node, store: PropTypes.object };
