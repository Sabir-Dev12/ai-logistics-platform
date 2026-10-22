export const categories = ["International", "Previsionnel", "Enlevement", "Autre", "Mead"];
export const natures = ["Complet", "Groupage", "GroupageConfrere"];
export const categoryLabel = (value) => ({ Previsionnel: "Prévisionnel", Enlevement: "Enlèvement", Mead: "MEAD" }[value] || value);
export const natureLabel = (value) => value === "GroupageConfrere" ? "Groupage confrère" : value;
export class VoyageError extends Error {}
export const guard = (test, message) => { if (!test) throw new VoyageError(message); };
export const status = (row) => row.dateCloture ? "Clôturé" : row.dateVoyage ? "Validé" : "Brouillon";
export const normalize = (value) => String(value ?? "").normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLowerCase().trim();
export const money = (value) => new Intl.NumberFormat("fr-FR", { minimumFractionDigits: 3, maximumFractionDigits: 3 }).format(value);
export function validNumber(value) { return value !== "" && value != null && Number.isFinite(Number(value)) && Number(value) >= 0; }
export function validateVoyage(value) {
  guard(Boolean(value.numVoyage?.trim()), "Le numéro de voyage est obligatoire.");
  guard(categories.includes(value.categorie) && natures.includes(value.nature), "Sélectionnez une catégorie et une nature valides.");
  guard(!value.dateDepartPrevu || !value.dateArriveePrevu || value.dateArriveePrevu >= value.dateDepartPrevu, "L’arrivée prévue ne peut pas précéder le départ.");
}
export const rates = [
  { id: "societe", label: "Société", value: 300, unit: "MAD / voyage" }, { id: "assurance", label: "Assurance marchandise", value: 80, unit: "MAD / voyage" },
  { id: "banque", label: "Frais bancaires", value: 25, unit: "MAD / voyage" }, { id: "mead", label: "MEAD", value: 120, unit: "MAD / voyage MEAD" },
  { id: "agence", label: "Agence", value: 150, unit: "MAD / voyage" }, { id: "tracteur", label: "Tracteur", value: 180, unit: "MAD / unité / trajet (quote-part mensuelle)" },
  { id: "remorque", label: "Remorque", value: 90, unit: "MAD / unité / trajet (quote-part mensuelle)" }, { id: "chauffeur", label: "Chauffeur", value: 100, unit: "MAD / trajet avec chauffeur (quote-part mensuelle)" },
  { id: "tracteurKm", label: "Tracteur kilométrique", value: 1.2, unit: "MAD / km" }, { id: "gasoil", label: "Gasoil", value: 2.4, unit: "MAD / km" }, { id: "adblue", label: "AdBlue", value: 0.15, unit: "MAD / km" },
];
export function previewCosts(row, dossiers, month) {
  const attached = dossiers.filter((item) => item.keyVoyageInter === row.id);
  let reason = !row.dateVoyage ? "Le voyage doit être validé." : row.dateVoyage.slice(0, 7) !== month ? "Le mois de calcul doit correspondre au mois de validation." : !attached.length ? "Aucun dossier n’est associé à ce voyage." : "";
  if (!/^\d{4}-(0[1-9]|1[0-2])$/.test(month)) reason = "Sélectionnez un mois de calcul valide.";
  if (reason) return { eligible: false, reason, allocations: [], skipped: [], nbDossiers: attached.length, total: 0 };
  const r = Object.fromEntries(rates.map((rate) => [rate.id, rate.value]));
  const legs = row.trajets.filter((leg) => leg.tracteur?.trim() && Number(leg.kilometrage) > 0);
  const km = legs.reduce((sum, leg) => sum + Number(leg.kilometrage), 0);
  const qty = (type) => row.equipements.filter((item) => item.typeEquipement.toLowerCase() === type).reduce((sum, item) => sum + Number(item.qte), 0);
  const operations = [{ operation: "Coût Société", amount: r.societe }, { operation: "Coût Assurance Marchandise", amount: r.assurance }, { operation: "Coût Frais Bancaire", amount: r.banque },
    ...(attached.some((item) => item.magasinDouane) ? [{ operation: "Coût MEAD", amount: r.mead }] : []), ...(row.agence ? [{ operation: "Coût Agence", amount: r.agence }] : []),
    { operation: "Coût Tracteur", amount: qty("tracteur") * r.tracteur + km * r.tracteurKm }, { operation: "Coût Remorque", amount: qty("remorque") * r.remorque },
    { operation: "Coût Chauffeur", amount: row.trajets.filter((leg) => leg.chauffeur?.trim()).length * r.chauffeur },
    ...(km ? [{ operation: "Coût Gasoil", amount: km * r.gasoil }, { operation: "Coût AdBlue", amount: km * r.adblue }] : [])];
  const skipped = operations.filter((item) => row.costMarkers.includes(item.operation)).map((item) => item.operation);
  const allocations = operations.filter((item) => !row.costMarkers.includes(item.operation)).map((item) => ({ ...item, perDossier: Math.round(item.amount / attached.length * 1000) / 1000 }));
  return { eligible: true, reason: "", nbDossiers: attached.length, allocations, skipped, total: allocations.reduce((sum, item) => sum + item.amount, 0) };
}

