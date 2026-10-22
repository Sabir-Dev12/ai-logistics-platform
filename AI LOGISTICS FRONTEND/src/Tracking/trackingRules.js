export const trackingEntities = [{ value: "Client", label: "Client" }, { value: "Booking", label: "Booking" }, { value: "Dossier", label: "Dossier" }, { value: "Voyage", label: "Voyage" }, { value: "FactureVente", label: "Facture" }, { value: "Reglement", label: "Règlement" }, { value: "Cotation", label: "Cotation" }];
export class TrackingError extends Error {}
export const trackingGuard = (condition, message) => { if (!condition) throw new TrackingError(message); };
export const isSystemStatus = (status) => Boolean(status.isSysteme || status.keyStatut?.startsWith("$"));
export const allowedStatuses = (statuses, entity) => statuses.filter((status) => !isSystemStatus(status) && !status.retired && status.entities.includes(entity));
export const timelineFor = (events, entity, key) => events.filter((event) => event.entite === entity && event.cleEntite === key).sort((left, right) => new Date(right.dateTraceSystem) - new Date(left.dateTraceSystem) || right.sequence - left.sequence);
export function displayDate(value) { return value ? new Intl.DateTimeFormat("fr-FR", { dateStyle: "medium", timeStyle: "short" }).format(new Date(value)) : "—"; }
export function localDateTime(value = new Date()) { const date = new Date(value); return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}T${String(date.getHours()).padStart(2, "0")}:${String(date.getMinutes()).padStart(2, "0")}`; }
