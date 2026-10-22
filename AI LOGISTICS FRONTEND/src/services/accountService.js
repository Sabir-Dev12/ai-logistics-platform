import { AuthenticationError } from "./authenticationService";
export class AccountError extends Error {
  constructor(code, message) { super(message); this.name = "AccountError"; this.code = code; }
}
const invalid = () => new AccountError("invalid_response", "Le serveur a renvoyé une réponse de compte invalide. Réessayez.");
const httpError = (status) => status === 403 ? new AccountError("forbidden", "Vous n’avez pas l’autorisation d’effectuer cette action.")
  : status === 404 ? new AccountError("not_found", "Le compte utilisateur n’existe plus ou ne permet pas le changement de mot de passe.")
    : status === 400 || status === 422 ? new AccountError("validation", "Le mot de passe a été refusé. Utilisez entre 8 et 200 caractères et vérifiez votre saisie.")
      : status === 429 ? new AccountError("rate_limit", "Trop de tentatives. Patientez avant de réessayer.")
        : new AccountError("unavailable", "Le service du compte est indisponible. Réessayez.");
const safeError = (error) => error instanceof AccountError ? error : error instanceof AuthenticationError
  ? new AccountError(error.code, error.message) : new AccountError("network", "Impossible de joindre le serveur. Vérifiez votre connexion puis réessayez.");
export function createAccountService({ request, logout, timeoutMs = 15000 }) {
  const call = async (path, options = {}, parse = true) => {
    const controller = new AbortController(), abort = () => controller.abort();
    if (options.signal?.aborted) controller.abort(); else options.signal?.addEventListener("abort", abort, { once: true });
    let timedOut = false; const timer = setTimeout(() => { timedOut = true; controller.abort(); }, timeoutMs);
    try {
      const response = await request(path, { ...options, signal: controller.signal });
      if (!parse) return response;
      if (!response.ok) throw httpError(response.status);
      const text = await response.text(); if (!text.trim()) return null;
      try { return JSON.parse(text); } catch { throw invalid(); }
    } catch (error) {
      if (options.signal?.aborted) throw new DOMException("Annulé", "AbortError");
      if (timedOut) throw new AccountError("timeout", "Le serveur met trop de temps à répondre. Réessayez.");
      throw safeError(error);
    } finally { clearTimeout(timer); options.signal?.removeEventListener("abort", abort); }
  };
  const identity = async (signal) => {
    const result = await call("/api/v1/auth/moi", { method: "GET", signal });
    if (result === null) return null;
    if (!result || typeof result.societeKey !== "string" || !result.societeKey.trim()) throw invalid();
    return result;
  };
  return {
    async getProfile({ signal } = {}) {
      const user = await identity(signal); if (!user) return null;
      let company = null, companyWarning = null;
      try {
        company = await call("/api/v1/reference/societe", { method: "GET", signal });
        if (company && (typeof company.intituleSoc !== "string" || !company.intituleSoc.trim() || (company.keySoc && company.keySoc !== user.societeKey))) throw invalid();
        if (!company) companyWarning = "Aucune société disponible pour ce compte.";
      } catch (error) {
        if (error.name === "AbortError" || error.code === "unauthorized") throw error;
        companyWarning = error.code === "forbidden" ? "Vous n’avez pas accès aux informations de la société." : "Les informations de la société sont indisponibles. Réessayez.";
      }
      return { displayName: typeof user.userDisplayName === "string" && user.userDisplayName.trim() ? user.userDisplayName : "Non renseigné", identifier: typeof user.userKey === "string" && user.userKey.trim() ? user.userKey : "Non renseigné", companyName: companyWarning ? "Non disponible" : company.intituleSoc, companyWarning };
    },
    async changePassword({ oldPassword, newPassword }, { signal } = {}) {
      if (typeof oldPassword !== "string" || !oldPassword.length) throw new AccountError("validation", "L’ancien mot de passe est obligatoire.");
      if (typeof newPassword !== "string" || !newPassword.trim() || newPassword.length < 8 || newPassword.length > 200) throw httpError(422);
      const response = await call("/api/v1/auth/mot-de-passe", { method: "POST", signal, acceptPasswordRejection: true, headers: { "Content-Type": "application/json", Accept: "application/json" }, body: JSON.stringify({ ancienMotDePasse: oldPassword, nouveauMotDePasse: newPassword }) }, false);
      if (response.status === 401) {
        // Distinguish an expired session from a wrong current password without repeating the mutation.
        const current = await identity(signal); if (!current) throw new AccountError("unauthorized", "Votre session n’est plus autorisée. Reconnectez-vous.");
        throw new AccountError("old_password_invalid", "L’ancien mot de passe est incorrect.");
      }
      if (!response.ok) throw httpError(response.status);
      if (response.status !== 204) throw invalid();
    },
    async logout() { await logout(); },
  };
}
