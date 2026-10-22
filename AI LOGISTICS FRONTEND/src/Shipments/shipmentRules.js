export const shipmentTypes = [{ value: "RI", label: "Import routier" }, { value: "RE", label: "Export routier" }, { value: "MI", label: "Import maritime" }, { value: "ME", label: "Export maritime" }];
export const billingStatuses = { ANePasFacturer: { label: "À ne pas facturer", variant: "info" }, NonFacture: { label: "Non facturé", variant: "warning" }, Facture: { label: "Facturé", variant: "success" } };
export const normalizeSearch = (value) => String(value ?? "").normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLowerCase().trim();
export const numberValue = (value) => value === "" || value == null ? null : Number(value);
export class ShipmentError extends Error {}
export const ensure = (condition, message) => { if (!condition) throw new ShipmentError(message); };
export function validateShipment(value) {
  ensure(Boolean(value.numDossier?.trim()), "Le numéro de dossier est obligatoire.");
  ensure(shipmentTypes.some((type) => type.value === value.typeDossier), "Sélectionnez un type de dossier.");
  for (const key of ["nbColis", "poidsBrut", "poidsTaxable", "volume"]) ensure(value[key] == null || value[key] === "" || (Number.isFinite(Number(value[key])) && Number(value[key]) >= 0), "Les mesures doivent être des nombres positifs ou nuls.");
}
export function validatePacking(lines) {
  for (const line of lines) {
    ensure(Boolean(line.typeColis?.trim()), "Le type de colis est obligatoire pour chaque ligne.");
    ensure(line.nbColis !== "" && line.nbColis != null, "Le nombre de colis est obligatoire.");
    for (const key of ["nbColis", "poidsBrut", "volume"]) ensure(line[key] == null || line[key] === "" || (Number.isFinite(Number(line[key])) && Number(line[key]) >= 0), "Les mesures du colisage doivent être positives ou nulles.");
  }
}
export function splitByTaxableWeight(rows, mad, currency) {
  const total = rows.reduce((sum, row) => sum + Number(row.poidsTaxable || 0), 0);
  if (total <= 0) return [];
  return rows.map((row) => ({ ...row, part: Number(row.poidsTaxable || 0) / total * 100,
    montantMad: Math.round(Number(mad) * Number(row.poidsTaxable || 0) / total * 1000) / 1000,
    montantDevise: Math.round(Number(currency) * Number(row.poidsTaxable || 0) / total * 1000) / 1000 }));
}
