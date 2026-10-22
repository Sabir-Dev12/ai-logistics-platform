import { AuthenticationError } from "./authenticationService";

export class BusinessApiError extends Error {
  constructor(message, code = "unavailable", uncertain = false) { super(message); this.code = code; this.uncertain = uncertain; }
}
export const invalidBusinessResponse = () => new BusinessApiError("Réponse du serveur invalide. Actualisez la page.", "invalid_response");
export const uncertainBusinessResponse = () => new BusinessApiError("Le résultat de l’opération est incertain. Actualisez pour le vérifier avant de réessayer.", "uncertain", true);
export function confirmedBusinessWrite(mapper) { try { return mapper(); } catch { throw uncertainBusinessResponse(); } }
export const businessKey = (value) => { if (typeof value !== "string" || !value.trim()) throw invalidBusinessResponse(); return encodeURIComponent(value); };
export const dateOnly = (value) => typeof value === "string" ? value.slice(0, 10) : "";
export function createBusinessApi(request, timeoutMs = 15000) {
  return async (path, { method = "GET", body, signal, expected = 200 } = {}) => {
    const controller = new AbortController(), abort = () => controller.abort();
    if (signal?.aborted) controller.abort(); else signal?.addEventListener("abort", abort, { once: true });
    const timer = setTimeout(abort, timeoutMs), writing = method !== "GET";
    const uncertain = uncertainBusinessResponse;
    try {
      const response = await request(path, { method, signal: controller.signal, ...(body !== undefined ? { headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) } : {}) });
      if (response.status !== expected) {
        if (writing && response.status >= 500) throw uncertain();
        const message = response.status === 404 ? "Cette ressource n’est plus disponible. Actualisez la liste."
          : response.status === 403 ? "Votre profil ne permet pas cette opération."
          : response.status === 400 || response.status === 422 ? "Le serveur refuse cette opération. Vérifiez les champs et les règles métier."
          : response.status === 409 ? "Ces informations sont déjà utilisées ou ont été modifiées. Actualisez la liste."
          : "Le service est indisponible. Réessayez.";
        throw new BusinessApiError(message, String(response.status));
      }
      if (expected === 204) return null;
      try { return await response.json(); } catch { throw writing ? uncertain() : invalidBusinessResponse(); }
    } catch (error) {
      if (signal?.aborted) throw new DOMException("Annulé", "AbortError");
      if (error instanceof AuthenticationError || error instanceof BusinessApiError) throw error;
      if (writing) throw uncertain();
      throw new BusinessApiError("Impossible de joindre le serveur. Vérifiez votre connexion puis réessayez.", "network");
    } finally { clearTimeout(timer); signal?.removeEventListener("abort", abort); }
  };
}
export async function businessPage(call, path, params, signal) {
  const result = await call(`${path}?${new URLSearchParams(Object.fromEntries(Object.entries(params).filter(([, value]) => value !== "" && value != null)))}`, { signal });
  if (!result || !Array.isArray(result.items) || result.page !== Number(params.page) || result.pageSize !== Number(params.pageSize)
    || !Number.isSafeInteger(result.totalCount) || result.totalCount < 0 || result.items.length > Number(params.pageSize) || result.items.length > result.totalCount) throw invalidBusinessResponse();
  return result;
}
