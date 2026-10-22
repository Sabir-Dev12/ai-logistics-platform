export { currencies, money } from "../Quotations/quotationRules";
export class InvoiceError extends Error {}
export const guard = (condition, message) => { if (!condition) throw new InvoiceError(message); };
export const natures = ["Produit", "Debours", "Assigne", "TaxeRegionale", "Charge"];
export const natureLabels = { Produit: "Produit", Debours: "Débours", Assigne: "Assigné", TaxeRegionale: "Taxe régionale", Charge: "Charge" };
export const round = (value, digits = 2) => { const factor = 10 ** digits; return Math.sign(value) * Math.round((Math.abs(value) + Number.EPSILON) * factor) / factor; };
export const newLine = (id) => ({ id, label: "", nature: "Produit", gross: "", discount: 0, rate: 1, vatRate: 20 });
export function validateInvoice(value, clients, availableCurrencies = ["MAD", "EUR", "USD", "GBP"]) {
  guard(clients.some((client) => client.id === value.clientId), "Sélectionnez un client.");
  guard(value.date && (!value.dueDate || value.dueDate >= value.date), "Vérifiez les dates de facture et d’échéance.");
  guard(availableCurrencies.includes(value.currency), "Sélectionnez une devise valide.");
  guard(Number.isFinite(Number(value.rate)) && Number(value.rate) > 0, "Le taux de la facture doit être strictement positif.");
  guard(value.currency !== "MAD" || Number(value.rate) === 1, "Le taux MAD doit être égal à 1.");
  guard(value.lines.length, "Une facture doit comporter au moins une ligne.");
  for (const line of value.lines) {
    guard(line.label.trim(), "Le libellé de chaque ligne est obligatoire.");
    guard(natures.includes(line.nature), "Sélectionnez une nature de ligne valide.");
    guard(line.gross !== "" && Number.isFinite(Number(line.gross)) && Number(line.gross) >= 0, "Le montant brut doit être positif ou nul.");
    guard(Number.isFinite(Number(line.rate)) && Number(line.rate) > 0, "Le taux de change de chaque ligne doit être strictement positif.");
    guard(line.discount !== "" && Number.isFinite(Number(line.discount)) && Number(line.discount) >= 0 && Number(line.discount) <= 100, "La remise doit être comprise entre 0 et 100 %.");
    guard(line.vatRate !== "" && Number.isFinite(Number(line.vatRate)) && Number(line.vatRate) >= 0 && Number(line.vatRate) <= 100, "La TVA doit être comprise entre 0 et 100 %.");
  }
}
export function calculateInvoice(invoice) {
  const lines = invoice.lines.map((line) => { const net = round(Number(line.gross) * (100 - Number(line.discount)) / 100, 3); const ht = round(net * Number(line.rate)); return { ...line, net, ht, vat: round(ht * Number(line.vatRate) / 100) }; });
  const bucket = (nature) => round(lines.filter((line) => line.nature === nature).reduce((sum, line) => sum + line.ht, 0));
  const produit = bucket("Produit"), debours = bucket("Debours"), assigne = bucket("Assigne"), regional = bucket("TaxeRegionale");
  const ht = round(produit + debours + assigne + regional), vat = round(lines.reduce((sum, line) => sum + line.vat, 0)), ttcMad = round(ht + vat), ttc = round(ttcMad / Number(invoice.rate));
  const rasIs = round(produit / Number(invoice.rate) * (invoice.rasIsRate || 0) / 100), rasTva = round(vat * (invoice.rasTvaRate || 0) / 100), net = round(ttc - rasIs - rasTva);
  guard([ht, vat, ttcMad, ttc, net, ...lines.flatMap((line) => [line.net, line.ht, line.vat])].every((value) => Number.isFinite(value) && Math.abs(value) < 1e12), "Les montants dépassent la plage de calcul de cette démonstration.");
  return { lines, produit, debours, assigne, regional, ht, vat, ttcMad, ttc, rasIs, rasTva, net };
}
