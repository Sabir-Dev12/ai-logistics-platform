export class ScaleError extends Error {}
export const guard = (condition, message) => { if (!condition) throw new ScaleError(message); };
export const numberOrNull = (value) => value === "" || value == null ? null : Number(value);
export const money = (value, currency = "MAD") => value == null ? "—" : `${new Intl.NumberFormat("fr-FR", { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value)} ${currency}`;
export function validateHeader(value) {
  guard(value.label?.trim(), "Le libellé du barème est obligatoire.");
  guard(!value.from || !value.to || value.to >= value.from, "La fin de validité ne peut pas précéder le début.");
  for (const key of ["rounding1", "rounding2"]) guard(value[key] == null || Number.isFinite(value[key]), "Les arrondis doivent être numériques.");
}
export function validateLines(kind, lines, columns = []) {
  const keys = new Set();
  for (const line of lines) {
    if (kind === "columns") {
      guard(Number.isInteger(line.columnId) && line.columnId > 0, "Le numéro de colonne doit être un entier positif.");
      guard(!keys.has(line.columnId), "Les numéros de colonnes doivent être uniques."); keys.add(line.columnId);
      for (const key of ["from", "to", "minimum"]) guard(line[key] == null || Number.isFinite(line[key]), "Les bornes et montants doivent être numériques.");
      guard(line.from == null || line.to == null || line.to >= line.from, "La fin de tranche ne peut pas être inférieure au début.");
    } else if (kind === "cells") {
      guard(columns.some((column) => column.columnId === line.columnId), "Sélectionnez une colonne existante.");
      guard(line.department?.trim(), "Le département est obligatoire.");
      const key = `${line.columnId}:${line.department.trim().toLowerCase()}`;
      guard(!keys.has(key), "Une seule cellule est autorisée par département et colonne."); keys.add(key);
      guard(line.amount == null || Number.isFinite(line.amount), "Le montant doit être numérique.");
    } else {
      guard(line.amount == null || Number.isFinite(line.amount), "Le montant doit être numérique.");
    }
  }
}
export function resolveTariff(scale, department, value) {
  guard(department.trim() && Number.isFinite(value), "Renseignez un département et une valeur numérique.");
  const column = [...scale.columns].sort((a, b) => a.columnId - b.columnId).find((item) => (item.from == null || value >= item.from) && (item.to == null || value < item.to));
  const cell = column && scale.cells.find((item) => item.columnId === column.columnId && item.department.toLowerCase() === department.trim().toLowerCase());
  return { column, amount: cell?.amount ?? null, found: Boolean(column && cell?.amount != null), currency: scale.currency };
}
