import { invoiceSeed } from "../SalesInvoices/mockInvoiceRepository";
import { eligibleInvoices, guard, isAmount, outstanding, remaining, round, validatePayment } from "./paymentRules";
export function paymentSeed() {
  const sales = invoiceSeed();
  const parties = [...sales.clients.map((client) => ({ ...client, type: "Client" })), { id: "FRS-DEMO-1", label: "Transporteur Démo", type: "Fournisseur" }, { id: "AG-DEMO-1", label: "Agent Démo", type: "Etrangers" }, { id: "PER-DEMO-1", label: "Personnel Démo", type: "Personnel" }];
  const modes = [{ id: "CHEQUE", label: "Chèque", status: "Portefeuille" }, { id: "VIREMENT", label: "Virement", status: "Saisie" }, { id: "ESPECE", label: "Espèces", status: "Saisie" }, { id: "TRAITE", label: "Traite", status: "Portefeuille" }];
  const treasuries = [{ id: "TR-DEMO-1", label: "Banque principale Démo" }, { id: "TR-DEMO-2", label: "Caisse Démo" }];
  const invoices = sales.invoices.map((invoice) => ({ id: invoice.id, number: invoice.number, partyId: invoice.clientId, currency: invoice.currency, total: invoice.totals.net, paid: invoice.paid, side: "Vente" }));
  invoices.push({ id: "FA-DEMO-1", number: "FA-2026-001", partyId: "FRS-DEMO-1", currency: "MAD", total: -2000, paid: 0, side: "Achat" }, { id: "FV-EUR-1", number: "FV-EUR-DEMO", partyId: "CLI-DEMO-1", currency: "EUR", total: 500, paid: 0, side: "Vente" });
  const payments = Array.from({ length: 7 }, (_, index) => ({ id: `REG-DEMO-${index + 1}`, reference: `PAY-DEMO-${index + 1}`, type: index === 2 ? "Fournisseur" : "Client", partyId: index === 2 ? "FRS-DEMO-1" : sales.clients[index % 2].id, amount: index === 2 ? -1000 : 1000, currency: "MAD", rate: 1, mad: index === 2 ? -1000 : 1000, mode: index === 1 ? "VIREMENT" : "CHEQUE", nature: index === 0 ? "Avance" : "Règlement", status: index === 3 ? "Impaye" : index === 4 ? "RemisEnBanque" : "Portefeuille", date: `2026-10-0${index + 1}`, dueDate: "2026-11-15", observation: "Règlement de démonstration", allocations: [], treasury: index === 4 ? "TR-DEMO-1" : "", remittanceDate: index === 4 ? "2026-10-06" : "", remittanceId: index === 4 ? "REM-DEMO-1" : "", accountingReference: "", unpaidReason: index === 3 ? "Provision insuffisante" : "", unpaidComment: "" }));
  payments[0].allocations = [{ id: "FV-DEMO-1", invoiceId: "FV-DEMO-1", amount: 200, currency: "MAD", side: "Vente" }]; invoices[0].paid += 200;
  return { parties, modes, treasuries, invoices, payments };
}
const wait = (delay, signal) => new Promise((resolve, reject) => { const abort = () => { clearTimeout(timer); reject(new DOMException("Annulé", "AbortError")); }; const timer = setTimeout(() => { signal?.removeEventListener("abort", abort); resolve(); }, delay); if (signal?.aborted) abort(); else signal?.addEventListener("abort", abort, { once: true }); });
export function createMockPaymentRepository({ seed = paymentSeed(), delay = 200 } = {}) {
  const state = structuredClone(seed); let sequence = 0;
  const snapshot = (updatedId) => ({ ...structuredClone(state), updatedId });
  const find = (id) => { const payment = state.payments.find((row) => row.id === id); guard(payment, "Le règlement n’est plus disponible."); return payment; };
  return {
    async list({ signal } = {}) { await wait(delay, signal); return snapshot(); },
    async create(value, { signal } = {}) {
      await wait(delay, signal); validatePayment(value, state.parties, state.modes); const rate = value.currency === "MAD" ? 1 : Number(value.rate), amount = round(Number(value.amount)) * (value.direction === "out" ? -1 : 1); const mad = round(amount * rate); guard(Number.isFinite(mad) && Math.abs(mad) < 1e12, "Le montant converti dépasse la plage autorisée.");
      guard(Math.abs(amount) >= 0.01, "Le montant doit être au moins égal à 0,01."); const id = `REG-NEW-${++sequence}`, banked = ["VIREMENT", "ESPECE"].includes(value.mode); state.payments.unshift({ ...structuredClone(value), id, amount, mad, rate, nature: value.advance ? "Avance" : "Règlement", status: state.modes.find((mode) => mode.id === value.mode).status, allocations: [], treasury: "", remittanceDate: banked ? value.date : "", validationDate: banked ? value.date : "", remittanceId: "", accountingReference: "", unpaidReason: "", unpaidComment: "" }); return snapshot(id);
    },
    async allocate(id, value, { signal } = {}) {
      await wait(delay, signal); const payment = find(id); guard(payment.status !== "Impaye", "Un règlement impayé ne peut pas recevoir une nouvelle affectation.");
      guard(!payment.allocations.some((allocation) => allocation.invoiceId === value.invoiceId), "Cette facture est déjà affectée à ce règlement.");
      const invoice = eligibleInvoices(payment, state.invoices).find((row) => row.id === value.invoiceId); guard(invoice, "Sélectionnez une facture non réglée du même tiers et de la même devise.");
      guard(isAmount(value.amount) && Number(value.amount) > 0, "Le montant affecté doit être strictement positif."); const amount = round(Number(value.amount)); guard(amount > 0, "Le montant affecté doit être au moins égal à 0,01.");
      guard(amount <= remaining(payment), "Le montant dépasse le disponible du règlement."); guard(amount <= outstanding(invoice), "Le montant dépasse le solde de la facture.");
      payment.allocations.push({ id: invoice.id, invoiceId: invoice.id, amount, currency: payment.currency, side: invoice.side }); invoice.paid = round(invoice.paid + (invoice.side === "Achat" ? -amount : amount)); return snapshot(id);
    },
    async deallocate(id, value, { signal } = {}) { await wait(delay, signal); const payment = find(id), allocation = payment.allocations.find((row) => row.invoiceId === value.invoiceId); guard(allocation, "Cette affectation n’existe plus."); const invoice = state.invoices.find((row) => row.id === allocation.invoiceId); guard(invoice, "La facture liée n’est plus disponible."); invoice.paid = round(invoice.paid - (allocation.side === "Achat" ? -allocation.amount : allocation.amount)); payment.allocations = payment.allocations.filter((row) => row.invoiceId !== value.invoiceId); return snapshot(id); },
    async remit(id, value, { signal } = {}) { await wait(delay, signal); const payment = find(id); guard(payment.status !== "Impaye", "Un règlement impayé ne peut pas être remis en banque."); guard(state.treasuries.some((row) => row.id === value.treasury), "Sélectionnez une trésorerie."); guard(value.date && value.date >= payment.date, "La date de remise ne peut pas précéder l’opération."); payment.treasury = value.treasury; payment.remittanceDate = value.date; payment.accountingReference = value.reference.trim(); payment.remittanceId = `REM-NEW-${++sequence}`; payment.status = "RemisEnBanque"; return snapshot(id); },
    async unpaid(id, value, { signal } = {}) { await wait(delay, signal); const payment = find(id); guard(value.reason.trim(), "Le motif d’impayé est obligatoire."); payment.unpaidReason = value.reason.trim(); payment.unpaidComment = value.comment.trim(); payment.status = "Impaye"; return snapshot(id); },
  };
}
