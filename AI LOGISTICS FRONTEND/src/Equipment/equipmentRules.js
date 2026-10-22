export { money } from "../SalesInvoices/invoiceRules";
export class EquipmentError extends Error {}
export const guard = (condition, message) => { if (!condition) throw new EquipmentError(message); };
export const statusLabels = { Disponible: "Disponible", EnReparation: "En réparation", Sorti: "Sorti du parc" };
export const retired = (row) => row.status === "Sorti" || Boolean(row.exitDate);
export const optionalNumber = (value) => value === "" || value == null ? null : Number(value);
export function validateEquipment(value, equipment, types, id) {
  guard(types.some((type) => type.id === value.type), "Le type de matériel est obligatoire.");
  guard(!value.registration.trim() || !equipment.some((row) => row.id !== id && row.registration.toLowerCase() === value.registration.trim().toLowerCase()), "Cette immatriculation est déjà utilisée.");
  guard(!value.acquisitionDate || !value.circulationDate || value.circulationDate >= value.acquisitionDate, "La mise en circulation ne peut pas précéder l’acquisition.");
  const row = equipment.find((item) => item.id === id);
  guard(!row?.exitDate || !value.acquisitionDate || value.acquisitionDate <= row.exitDate, "L’acquisition ne peut pas être postérieure à la sortie.");
}
