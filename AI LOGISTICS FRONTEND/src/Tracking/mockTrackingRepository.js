import { allowedStatuses, trackingEntities, trackingGuard } from "./trackingRules";
export function trackingSeed() {
  const prefixes = ["CLI", "BKG", "DOS", "VOY", "FAC", "REG", "COT"];
  const records = trackingEntities.flatMap((entity, index) => [{ entite: entity.value, key: `${prefixes[index]}-DEMO-1`, label: `${prefixes[index]}-DEMO-001` }, { entite: entity.value, key: `${prefixes[index]}-DEMO-2`, label: `${prefixes[index]}-DEMO-002 · Sans historique` }]);
  const statuses = [{ keyStatut: "NOTE", label: "Note de suivi", entities: trackingEntities.map((entity) => entity.value) },
    ...trackingEntities.map((entity) => ({ keyStatut: `SUIVI_${entity.value.toUpperCase()}`, label: `Suivi ${entity.label.toLowerCase()} confirmé`, entities: [entity.value] })),
    { keyStatut: "$CREATION", label: "Création", isSysteme: true, entities: trackingEntities.map((entity) => entity.value) },
    { keyStatut: "ARCHIVE", label: "Ancien statut", retired: true, entities: trackingEntities.map((entity) => entity.value) }];
  const events = trackingEntities.flatMap((entity, index) => Array.from({ length: 7 }, (_, position) => ({ id: `EVT-${index}-${position}`, sequence: index * 7 + position, entite: entity.value, cleEntite: `${prefixes[index]}-DEMO-1`, keyStatut: position === 0 ? "$CREATION" : position === 6 ? `SUIVI_${entity.value.toUpperCase()}` : "NOTE", libelleStatut: position === 0 ? "Création" : position === 6 ? `Suivi ${entity.label.toLowerCase()} confirmé` : "Note de suivi", isSysteme: position === 0,
    dateTraceUser: new Date(Date.UTC(2026, 9, 2 + position, 8, 30)).toISOString(), dateTraceSystem: new Date(Date.UTC(2026, 9, 2 + position, 9, 0)).toISOString(), utilisateur: position % 2 ? "Nadia Démo" : "Équipe logistique Démo", commentaire: position === 0 ? "Création de l’entité de démonstration." : `Événement de suivi fictif ${position}.` })));
  return { records, statuses, events, canRecord: true };
}
const wait = (delay, signal) => new Promise((resolve, reject) => { const abort = () => { clearTimeout(timer); reject(new DOMException("Opération annulée", "AbortError")); }; const timer = setTimeout(() => { signal?.removeEventListener("abort", abort); resolve(); }, delay); if (signal?.aborted) abort(); else signal?.addEventListener("abort", abort, { once: true }); });
export function createMockTrackingRepository({ seed = trackingSeed(), author = "Utilisateur de démonstration", delay = 200, now = () => new Date() } = {}) {
  const state = structuredClone(seed); let sequence = Math.max(0, ...state.events.map((event) => event.sequence)) + 1;
  return {
    async list({ signal } = {}) { await wait(delay, signal); return structuredClone(state); },
    async record(entity, key, value, { signal } = {}) {
      await wait(delay, signal);
      trackingGuard(state.canRecord, "Vous n’êtes pas autorisé à ajouter un événement.");
      trackingGuard(state.records.some((row) => row.entite === entity && row.key === key), "Sélectionnez une référence valide pour ce type d’entité.");
      const status = allowedStatuses(state.statuses, entity).find((item) => item.keyStatut === value.keyStatut);
      trackingGuard(status, "Ce statut n’est pas autorisé pour cette entité.");
      trackingGuard(value.commentaire?.trim(), "Le commentaire de suivi est obligatoire.");
      trackingGuard(value.commentaire.length <= 2000, "Le commentaire est limité à 2 000 caractères.");
      const date = new Date(value.dateEvenement), recorded = now();
      trackingGuard(value.dateEvenement && !Number.isNaN(date.getTime()), "Renseignez une date d’événement valide.");
      state.events.push({ id: `EVT-NEW-${sequence}`, sequence: sequence++, entite: entity, cleEntite: key, keyStatut: status.keyStatut, libelleStatut: status.label, isSysteme: false, dateTraceUser: date.toISOString(), dateTraceSystem: recorded.toISOString(), utilisateur: author, commentaire: value.commentaire.trim() });
      return structuredClone(state);
    },
  };
}
