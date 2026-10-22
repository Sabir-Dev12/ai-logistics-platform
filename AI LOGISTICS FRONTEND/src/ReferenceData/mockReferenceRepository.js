import { catalog, validateReference } from "./referenceCatalog";
export class ReferenceError extends Error {}
const guard = (condition, message) => { if (!condition) throw new ReferenceError(message); };
const row = (code, label, extra = {}) => ({ id: code, code, label, references: [], ...extra });
export function referenceSeed() {
  return { canDelete: true, canCreate: true, company: { name: "AI Logistics Démonstration", legalForm: "SARL", address: "Zone logistique de démonstration", city: "Casablanca", country: "Maroc", currency: "MAD", email: "contact@example.test", phone: "+212 000 000 000" }, entries: {
    countries: [row("MAROC", "Maroc", { alpha2: "MA", references: [{ label: "Clients", count: 3 }, { label: "Agences", count: 1 }] }), row("FRANCE", "France", { alpha2: "FR", references: [{ label: "Clients", count: 2 }] }), row("ESPAGNE", "Espagne", { alpha2: "ES" }), row("PORTUGAL", "Portugal", { alpha2: "PT" }), row("ITALIE", "Italie", { alpha2: "IT" }), row("ALLEMAGNE", "Allemagne", { alpha2: "DE" })],
    currencies: [row("MAD", "Dirham marocain", { decimals: 2, invoiceLabel: "Dirhams", minorUnit: "Centime", references: [{ label: "Société et factures", count: 4 }] }), row("EUR", "Euro", { decimals: 2, invoiceLabel: "Euros", minorUnit: "Centime", references: [{ label: "Factures", count: 2 }] }), row("USD", "Dollar américain", { decimals: 2, invoiceLabel: "Dollars", minorUnit: "Cent" })],
    agencies: [row("CASA", "Agence Casablanca", { city: "Casablanca", address: "Zone logistique démo", references: [{ label: "Voyages", count: 2 }] }), row("TNG", "Agence Tanger", { city: "Tanger", address: "Agence de démonstration" })],
    vat: [row("TVA20", "TVA 20 %", { rate: 20, account: "445700", references: [{ label: "Lignes de facture", count: 3 }] }), row("TVA0", "Exonération", { rate: 0, account: "445701" })],
    payments: [row("VIR", "Virement", { dueDays: 30, references: [{ label: "Règlements", count: 2 }] }), row("CHQ", "Chèque", { dueDays: 0 })],
    treasuries: [row("BANQUE", "Banque de démonstration", { currency: "MAD", nature: "Banque", references: [{ label: "Remises", count: 1 }] }), row("CAISSE", "Caisse de démonstration", { currency: "MAD", nature: "Caisse" })],
    dossierTypes: [row("IMPORT", "Import maritime", { nature: "Import" }), row("EXPORT", "Export maritime", { nature: "Export" })],
    incoterms: [row("DAP", "Delivered At Place", { edi: "DA", references: [{ label: "Dossiers", count: 2 }] }), row("EXW", "Ex Works", { edi: "EX" })],
    clientClasses: [row("A", "Classe A", { description: "Clients stratégiques", references: [{ label: "Clients", count: 2 }] }), row("B", "Classe B", { description: "Clients réguliers" })],
    clientStatuses: [row("ACTIF", "Actif", { description: "Relation commerciale active", references: [{ label: "Clients", count: 4 }] })],
    clientRisks: [row("NORMAL", "Normal", { description: "Risque standard", references: [{ label: "Clients", count: 3 }] })],
    clientSectors: [row("IND", "Industrie", { description: "Secteur industriel", references: [{ label: "Clients", count: 2 }] })],
    clientGroups: [row("DEMO", "Groupe de démonstration", { description: "Classification commerciale fictive" })],
    settings: [row("param_oblig_port", "Port obligatoire", { value: "Oui", description: "Port demandé sur les dossiers de démonstration." }), row("param_multi_devise", "Gestion multidevise", { value: "Oui", description: "Exemple de paramètre en lecture seule." })],
  } };
}
const wait = (delay, signal) => new Promise((resolve, reject) => { const abort = () => { clearTimeout(timer); reject(new DOMException("Annulé", "AbortError")); }; const timer = setTimeout(() => { signal?.removeEventListener("abort", abort); resolve(); }, delay); if (signal?.aborted) abort(); else signal?.addEventListener("abort", abort, { once: true }); });
export function createMockReferenceRepository({ seed = referenceSeed(), delay = 200 } = {}) {
  const state = structuredClone(seed), checks = new Map(); let sequence = 0;
  const snapshot = () => structuredClone(state);
  const find = (kind, id) => { const item = state.entries[kind]?.find((entry) => entry.id === id && !entry.deleted); guard(item, "La référence n’existe plus. Actualisez la liste."); return item; };
  const allowed = (kind, item) => !catalog[kind]?.readOnly && state.canDelete && item.references.every((reference) => reference.count === 0);
  return {
    async list({ signal } = {}) { await wait(delay, signal); return snapshot(); },
    async create(kind, value, { signal } = {}) { await wait(delay, signal); guard(state.canCreate && ["countries", "currencies"].includes(kind), "Création non autorisée."); const errors = validateReference(kind, value); guard(!Object.keys(errors).length, Object.values(errors)[0]); const code = value.code.trim().toUpperCase(), alpha2 = value.alpha2?.trim().toUpperCase(); guard(!state.entries[kind].some((item) => item.code.toUpperCase() === code), "Ce code existe déjà."); if (kind === "countries" && alpha2) guard(!state.entries.countries.some((item) => item.alpha2 === alpha2), "Ce code alpha-2 existe déjà."); const item = row(code, value.label.trim(), kind === "countries" ? { alpha2: alpha2 || "", agreement: value.agreement?.trim() || "", zone: value.zone?.trim() || "" } : { invoiceLabel: value.invoiceLabel?.trim() || "", minorUnit: value.minorUnit?.trim() || "", decimals: Number(value.decimals) }); state.entries[kind].unshift(item); return { ...snapshot(), updatedId: code }; },
    async check(kind, id, { signal } = {}) { await wait(delay, signal); const item = find(kind, id); const checkId = `CHECK-${++sequence}`; checks.set(checkId, { kind, id }); return { kind, id, checkId, canDelete: allowed(kind, item), references: structuredClone(item.references.filter((reference) => reference.count > 0)), reason: catalog[kind]?.readOnly ? "Ce référentiel est en lecture seule." : !state.canDelete ? "Votre profil ne permet pas la suppression." : item.references.some((reference) => reference.count > 0) ? "Cette référence est encore utilisée." : "Aucune utilisation détectée dans la démonstration.", softDelete: ["agencies", "incoterms"].includes(kind) }; },
    async remove(kind, id, checkId, { signal } = {}) { await wait(delay, signal); const check = checks.get(checkId); guard(check?.kind === kind && check.id === id, "Vérifiez les références avant de confirmer la suppression."); const item = find(kind, id); guard(allowed(kind, item), "Suppression refusée : référence utilisée ou action non autorisée."); if (["agencies", "incoterms"].includes(kind)) item.deleted = true; else state.entries[kind] = state.entries[kind].filter((entry) => entry.id !== id); checks.delete(checkId); return snapshot(); },
  };
}
