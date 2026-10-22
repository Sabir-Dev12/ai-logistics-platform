import { AuthenticationError } from "./authenticationService";
export class DashboardError extends Error {
  constructor(code, message) { super(message); this.name = "DashboardError"; this.code = code; }
}
export const dashboardEntities = { client: "Client", voyage: "Voyage", shipment: "Dossier", invoice: "FactureVente", payment: "Reglement", booking: "Booking", quotation: "Cotation" };
const enumValues = ["Client", "Voyage", "Dossier", "FactureVente", "Reglement", "Booking", "Cotation"];
const invalid = () => new DashboardError("invalid_response", "Le serveur a renvoyé des données de dashboard invalides. Réessayez.");
const text = (value) => typeof value === "string" ? value.trim() : "";
export function mapDashboardActivity(rows) {
  if (!Array.isArray(rows) || rows.length > 500) throw invalid();
  return rows.map((row, index) => {
    if (!row || typeof row !== "object") throw invalid();
    const entity = Number.isInteger(row.entite) ? enumValues[row.entite] : text(row.entite);
    const entityType = Object.keys(dashboardEntities).find((key) => dashboardEntities[key].toLowerCase() === entity?.toLowerCase());
    const dateValue = text(row.dateTraceSystem);
    const date = new Date(/(?:Z|[+-]\d\d:\d\d)$/i.test(dateValue) ? dateValue : `${dateValue}Z`);
    const calendar = new Date(`${dateValue.slice(0, 10)}T00:00:00Z`);
    if (!entityType || !text(row.cleEntite) || !/^\d{4}-\d\d-\d\dT/.test(dateValue) || Number.isNaN(date.valueOf()) || Number.isNaN(calendar.valueOf()) || calendar.toISOString().slice(0, 10) !== dateValue.slice(0, 10)) throw invalid();
    const statusLabel = text(row.libelleStatut) || text(row.keyStatut) || "Non renseigné";
    return { id: `${entityType}:${row.cleEntite}:${date.toISOString()}:${row.keyStatut || ""}:${index}`, entityType, entityId: row.cleEntite, date: date.toISOString(), businessDate: row.dateTraceUser, status: "unknown", statusLabel, description: text(row.commentaire) || statusLabel };
  });
}
export function createDashboardService({ request, timeoutMs = 15000, now = () => new Date() }) {
  return {
    async list({ signal, period = "30", entityType = "" } = {}) {
      if (!["7", "30", "90", "all"].includes(period) || (entityType && !dashboardEntities[entityType])) throw new DashboardError("validation", "Les filtres du dashboard sont invalides.");
      const asOf = now();
      if (!(asOf instanceof Date) || Number.isNaN(asOf.valueOf())) throw invalid();
      const params = new URLSearchParams({ take: "500", to: asOf.toISOString() });
      if (period !== "all") { const from = new Date(asOf.getFullYear(), asOf.getMonth(), asOf.getDate()); from.setDate(from.getDate() - (Number(period) - 1)); params.set("from", from.toISOString()); }
      if (entityType) params.set("entites", dashboardEntities[entityType]);
      const controller = new AbortController(), abort = () => controller.abort();
      if (signal?.aborted) controller.abort(); else signal?.addEventListener("abort", abort, { once: true });
      let timedOut = false; const timer = setTimeout(() => { timedOut = true; controller.abort(); }, timeoutMs);
      try {
        const response = await request(`/api/v1/tracking/activite?${params}`, { method: "GET", signal: controller.signal, headers: { Accept: "application/json" } });
        if (!response.ok) throw new DashboardError(response.status === 400 || response.status === 422 ? "validation" : response.status === 403 ? "forbidden" : "unavailable", response.status === 403 ? "Vous n’avez pas accès aux activités du dashboard." : response.status === 400 || response.status === 422 ? "Les filtres du dashboard ont été refusés." : "Le service du dashboard est indisponible. Réessayez.");
        const body = await response.text();
        if (!body.trim()) throw new DashboardError("empty_response", "Le serveur a renvoyé une réponse vide. Réessayez.");
        let rows; try { rows = JSON.parse(body); } catch { throw invalid(); }
        const activities = mapDashboardActivity(rows);
        return { activities, asOf, limit: 500, limited: rows.length === 500, realStatuses: true };
      } catch (error) {
        if (signal?.aborted) throw new DOMException("Annulé", "AbortError");
        if (timedOut) throw new DashboardError("timeout", "Le serveur met trop de temps à répondre. Réessayez.");
        if (error instanceof DashboardError) throw error;
        if (error instanceof AuthenticationError) throw new DashboardError(error.code, error.message);
        throw new DashboardError("network", "Impossible de joindre le serveur. Vérifiez votre connexion puis réessayez.");
      } finally { clearTimeout(timer); signal?.removeEventListener("abort", abort); }
    },
  };
}
