import { AuthenticationError, createAuthenticationService } from "../services/authenticationService";
import { setChatAccessToken } from "../services/chatAccessToken";

export function createAuthStore({ service = createAuthenticationService(), tokenSink = setChatAccessToken } = {}) {
  let state = { status: "anonymous", user: null, error: null, fieldErrors: {}, retryable: false };
  let session = null;
  let pending = null;
  let timer;
  let revision = 0;
  const listeners = new Set();
  const publish = (value) => { state = value; listeners.forEach((listener) => listener()); };
  const clear = (status = "anonymous", error = null) => {
    revision++; pending?.abort(); pending = null; clearTimeout(timer); session = null; tokenSink(null);
    publish({ status, user: null, error, fieldErrors: {}, retryable: false });
  };
  const expire = () => clear("unauthorized", "Votre session a expiré. Reconnectez-vous.");
  const schedule = () => {
    clearTimeout(timer);
    if (!session) return;
    const remaining = session.expiresAt - Date.now();
    if (remaining <= 0) { expire(); return; }
    timer = setTimeout(schedule, Math.min(remaining, 2147483647));
  };
  const accessToken = () => {
    if (session && session.expiresAt <= Date.now()) expire();
    return session?.token || null;
  };
  return {
    subscribe(listener) { listeners.add(listener); return () => listeners.delete(listener); },
    getSnapshot: () => state,
    getAccessToken: accessToken,
    logout: () => clear(),
    invalidate() { if (session) clear("unauthorized", "Votre session n’est plus autorisée. Reconnectez-vous."); },
    async login(credentials) {
      clear(); const attempt = revision; const controller = new AbortController(); pending = controller;
      publish({ status: "loading", user: null, error: null, fieldErrors: {}, retryable: false });
      try {
        const result = await service.signIn(credentials, controller.signal);
        if (controller.signal.aborted || attempt !== revision) return false;
        if (result.expiresAt <= Date.now()) { expire(); return false; }
        pending = null; session = result; tokenSink(result.token);
        publish({ status: "authenticated", user: result.user, error: null, fieldErrors: {}, retryable: false }); schedule(); return true;
      } catch (error) {
        if (controller.signal.aborted || attempt !== revision) return false;
        pending = null;
        publish({ status: error.code === "unauthorized" || error.code === "forbidden" ? "unauthorized" : "anonymous", user: null,
          error: error instanceof AuthenticationError ? error.message : "Connexion impossible. Réessayez.",
          fieldErrors: error instanceof AuthenticationError ? error.fieldErrors : {}, retryable: error instanceof AuthenticationError && error.retryable }); return false;
      }
    },
    async request(url, options) {
      const requestRevision = revision;
      const token = accessToken();
      if (!token) throw new AuthenticationError("unauthorized", "Connectez-vous pour continuer.");
      try { return await service.authorizedRequest(url, token, options); }
      catch (error) { if (error.code === "unauthorized" && requestRevision === revision && session?.token === token) clear("unauthorized", error.message); throw error; }
    },
  };
}
export const authStore = createAuthStore();

