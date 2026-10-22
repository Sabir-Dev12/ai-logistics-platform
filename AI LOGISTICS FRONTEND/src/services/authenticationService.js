export class AuthenticationError extends Error {
  constructor(code, message, { fieldErrors = {}, retryable = false } = {}) {
    super(message); this.name = "AuthenticationError"; this.code = code; this.fieldErrors = fieldErrors; this.retryable = retryable;
  }
}
const failure = (status, login = false) => status === 401
  ? new AuthenticationError("unauthorized", login ? "Identifiant ou mot de passe incorrect." : "Votre session n’est plus autorisée. Reconnectez-vous.")
  : status === 403 ? new AuthenticationError("forbidden", "Vous n’avez pas accès à cette ressource.")
    : status === 400 || status === 422 ? new AuthenticationError("validation", "Les informations de connexion sont invalides. Vérifiez votre saisie.")
      : status === 429 ? new AuthenticationError("rate_limit", "Trop de tentatives de connexion. Patientez avant de réessayer.")
        : new AuthenticationError("unavailable", "Le service d’authentification est indisponible. Réessayez.", { retryable: true });
const networkFailure = () => new AuthenticationError("network", "Impossible de joindre le serveur. Vérifiez votre connexion puis réessayez.", { retryable: true });
const invalidResponse = () => new AuthenticationError("invalid_response", "Réponse d’authentification invalide. Réessayez.", { retryable: true });

export function createAuthenticationService({ fetcher = (...args) => fetch(...args), companyKey = import.meta.env.VITE_AUTH_SOCIETE_KEY || "", timeoutMs = 15000 } = {}) {
  const request = async (path, options, login = false) => {
    const controller = new AbortController();
    const abort = () => controller.abort();
    if (options.signal?.aborted) controller.abort();
    else options.signal?.addEventListener("abort", abort, { once: true });
    let timedOut = false;
    const timer = setTimeout(() => { timedOut = true; controller.abort(); }, timeoutMs);
    try {
      const response = await fetcher(path, { ...options, signal: controller.signal, cache: "no-store", credentials: "omit", redirect: "error" });
      if (!response.ok) throw failure(response.status, login);
      const text = await response.text();
      if (!text.trim()) throw new AuthenticationError("empty_response", "Le serveur a renvoyé une réponse vide. Réessayez.", { retryable: true });
      try { return JSON.parse(text); } catch { throw invalidResponse(); }
    } catch (error) {
      if (options.signal?.aborted) throw new DOMException("Annulé", "AbortError");
      if (timedOut) throw new AuthenticationError("timeout", "Le serveur met trop de temps à répondre. Réessayez.", { retryable: true });
      if (error instanceof AuthenticationError) throw error;
      throw networkFailure();
    } finally { clearTimeout(timer); options.signal?.removeEventListener("abort", abort); }
  };
  return {
    async signIn({ identifiant, motDePasse, societeKey = companyKey } = {}, signal) {
      const fieldErrors = {};
      if (typeof identifiant !== "string" || !identifiant.trim()) fieldErrors.identifiant = "L’identifiant est obligatoire.";
      if (typeof motDePasse !== "string" || !motDePasse.length) fieldErrors.motDePasse = "Le mot de passe est obligatoire.";
      if (Object.keys(fieldErrors).length) throw new AuthenticationError("validation", "Renseignez les informations de connexion.", { fieldErrors });
      const company = typeof societeKey === "string" ? societeKey.trim() : "";
      const session = await request("/api/v1/auth/token", { method: "POST", signal, headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({ identifiant: identifiant.trim(), motDePasse, ...(company ? { societeKey: company } : {}) }) }, true);
      // The backend issues an opaque bearer token, not a JWT.
      if (!session || typeof session !== "object" || Array.isArray(session)) throw invalidResponse();
      const expiry = typeof session.expiresAt === "string" ? Date.parse(/(?:Z|[+-]\d\d:\d\d)$/.test(session.expiresAt) ? session.expiresAt : `${session.expiresAt}Z`) : NaN;
      if (typeof session.accessToken !== "string" || !session.accessToken.trim() || session.tokenType !== "Bearer" || !Number.isFinite(expiry) || expiry <= Date.now()) throw invalidResponse();
      const identity = await request("/api/v1/auth/moi", { method: "GET", signal, headers: { Accept: "application/json", Authorization: `Bearer ${session.accessToken}` } });
      if (!identity || typeof identity.societeKey !== "string" || !identity.societeKey.trim() || (typeof session.societeKey === "string" && identity.societeKey !== session.societeKey))
        throw new AuthenticationError("invalid_response", "Identité de session invalide. Réessayez.", { retryable: true });
      return { token: session.accessToken, expiresAt: expiry, user: { displayName: typeof identity.userDisplayName === "string" && identity.userDisplayName.trim() ? identity.userDisplayName : "Utilisateur", societeKey: identity.societeKey } };
    },
    async authorizedRequest(url, token, options = {}) {
      const target = new URL(url, window.location.origin);
      if (target.origin !== window.location.origin || (!target.pathname.startsWith("/api/") && target.pathname !== "/mcp")) throw new AuthenticationError("invalid_target", "Destination API invalide.");
      const { acceptPasswordRejection = false, ...fetchOptions } = options;
      const headers = new Headers(fetchOptions.headers); headers.set("Authorization", `Bearer ${token}`);
      let response;
      try { response = await fetcher(target.href, { ...fetchOptions, headers, credentials: "omit", cache: "no-store", redirect: "error" }); }
      catch (error) { if (error.name === "AbortError") throw error; throw networkFailure(); }
      const passwordRejection = acceptPasswordRejection && target.pathname === "/api/v1/auth/mot-de-passe" && fetchOptions.method?.toUpperCase() === "POST";
      if ((response.status === 401 && !passwordRejection) || response.status === 403) throw failure(response.status);
      return response;
    },
  };
}
