import { tokens } from "../design-system/tokens";

export const toolStatuses = { available: "Disponible", unavailable: "Indisponible", error: "Erreur" };
export const mockTools = [
  { name: "GetClient", description: "Consulter la fiche d’un client à partir de son identifiant.", category: "Clients", status: "available", parameters: [{ name: "clientId", type: "string", required: true, description: "Identifiant du client." }], lastUsed: "2026-10-14T10:30:00Z", lastInput: { clientId: "CLI-DEMO-001" }, result: { clientId: "CLI-DEMO-001", name: "Client de démonstration", city: "Casablanca" }, error: null },
  { name: "GetInvoice", description: "Consulter une facture et son statut de paiement.", category: "Facturation", status: "error", parameters: [{ name: "invoiceId", type: "string", required: true, description: "Identifiant de la facture." }], lastUsed: "2026-10-14T09:15:00Z", lastInput: { invoiceId: "INV-DEMO-404" }, result: null, error: { code: "invoice_not_found", message: "La facture de démonstration est introuvable." } },
  { name: "GetShipment", description: "Consulter les informations d’une expédition.", category: "Expéditions", status: "available", parameters: [{ name: "shipmentId", type: "string", required: true, description: "Identifiant de l’expédition." }], lastUsed: "2026-10-13T15:20:00Z", lastInput: { shipmentId: "SHP-DEMO-001" }, result: { shipmentId: "SHP-DEMO-001", origin: "Casablanca", destination: "Tanger", status: "En transit" }, error: null },
  { name: "GetTracking", description: "Consulter les événements de suivi d’une expédition.", category: "Suivi", status: "unavailable", parameters: [{ name: "shipmentId", type: "string", required: true, description: "Identifiant de l’expédition à suivre." }], lastUsed: "2026-10-13T14:05:00Z", lastInput: { shipmentId: "SHP-DEMO-002" }, result: null, error: { code: "tracking_unavailable", message: "Le service de suivi de démonstration est temporairement indisponible." } },
  { name: "GetBooking", description: "Consulter les informations d’une réservation.", category: "Réservations", status: "available", parameters: [{ name: "bookingId", type: "string", required: true, description: "Identifiant de la réservation." }], lastUsed: null, lastInput: null, result: null, error: null },
];

export const searchText = (text) => text.normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLocaleLowerCase("fr-FR").trim();
export const lastUsedLabel = (value) => value ? new Intl.DateTimeFormat("fr-FR", { dateStyle: "short", timeStyle: "short", timeZone: "Africa/Casablanca" }).format(new Date(value)) : "Jamais utilisé";

export function createMockToolRepository({ tools = mockTools, failListOnce = false } = {}) {
  return { async list({ signal } = {}) {
    await new Promise((resolve, reject) => {
      if (signal?.aborted) { reject(new DOMException("Aborted", "AbortError")); return; }
      const abort = () => { clearTimeout(timer); reject(new DOMException("Aborted", "AbortError")); };
      const timer = setTimeout(() => { signal?.removeEventListener("abort", abort); resolve(); }, Number.parseInt(tokens.motion.layout, 10));
      signal?.addEventListener("abort", abort, { once: true });
    });
    if (failListOnce) { failListOnce = false; throw new Error("Mock load failure"); }
    return tools.map((tool) => ({ ...tool }));
  } };
}
