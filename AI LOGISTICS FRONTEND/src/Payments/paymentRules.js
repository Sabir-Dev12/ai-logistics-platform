export { currencies, money, round } from "../SalesInvoices/invoiceRules";
export class PaymentError extends Error {}
export const guard = (condition, message) => { if (!condition) throw new PaymentError(message); };
export const paymentTypes = ["Client", "Fournisseur", "Etrangers", "Personnel"];
export const statusLabels = { RemisAuFournisseur: "Remis au fournisseur", Signe: "Signé", Saisie: "Saisie", Portefeuille: "Portefeuille", RemisEnBanque: "Remis en banque", Regle: "Réglé", Impaye: "Impayé" };
export const isAmount = (value) => value !== "" && Number.isFinite(Number(value)) && Math.abs(Number(value)) < 1e12;
export const usedAmount = (payment) => payment.allocations.reduce((sum, allocation) => sum + allocation.amount, 0);
export const remaining = (payment) => Math.max(0, Math.round((Math.abs(payment.amount) - usedAmount(payment)) * 100) / 100);
export const outstanding = (invoice) => Math.max(0, Math.round((Math.abs(invoice.total) - Math.abs(invoice.paid)) * 100) / 100);
export function eligibleInvoices(payment, invoices) {
  return invoices.filter((invoice) => invoice.partyId === payment.partyId && invoice.currency === payment.currency && invoice.side === (payment.type === "Fournisseur" ? "Achat" : "Vente") && outstanding(invoice) > 0 && !payment.allocations.some((allocation) => allocation.invoiceId === invoice.id));
}
export function validatePayment(value, parties, modes) {
  guard(paymentTypes.includes(value.type), "Sélectionnez un type de règlement valide.");
  guard(parties.some((party) => party.id === value.partyId && party.type === value.type), "Sélectionnez un tiers correspondant au type de règlement.");
  guard(isAmount(value.amount) && Number(value.amount) > 0, "Le montant doit être strictement positif et dans la plage autorisée.");
  guard(["MAD", "EUR", "USD", "GBP"].includes(value.currency), "Sélectionnez une devise valide.");
  guard(isAmount(value.rate) && Number(value.rate) > 0, "Le taux de change doit être strictement positif.");
  guard(value.direction === "in" || value.direction === "out", "Sélectionnez le sens du règlement.");
  guard(!value.advance || value.direction === "in", "Une avance doit être un encaissement.");
  guard(value.date && (!value.dueDate || value.dueDate >= value.date), "Vérifiez les dates d’opération et d’échéance.");
  guard(modes.some((mode) => mode.id === value.mode), "Sélectionnez un mode de paiement.");
}
