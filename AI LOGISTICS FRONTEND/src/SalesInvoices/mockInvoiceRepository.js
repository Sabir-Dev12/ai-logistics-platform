import { calculateInvoice, guard, newLine, validateInvoice } from "./invoiceRules";
export function invoiceSeed() {
  const clients = [{ id: "CLI-DEMO-1", label: "Atlas Transit Démo" }, { id: "CLI-DEMO-2", label: "Azur Distribution Démo" }];
  const invoices = Array.from({ length: 7 }, (_, index) => {
    const row = { id: `FV-DEMO-${index + 1}`, number: `FV-2026-${String(index + 1).padStart(3, "0")}`, clientId: clients[index % 2].id, clientName: clients[index % 2].label, type: "Facture", status: index === 3 ? "Contestée" : "Émise", date: `2026-10-0${index + 1}`, dueDate: "2026-11-15", currency: "MAD", rate: 1, dossier: `DOS-DEMO-${index + 1}`, voyage: "VOY-DEMO-1", service: "Transport routier", comment: "Facture fictive", exemption: "", needsRecalculation: false, rasIsRate: 0, rasTvaRate: 0, settled: index === 1, paid: 0, lines: [{ ...newLine(`L-${index}-1`), label: "Transport", gross: 1000 + index * 100, vatRate: 20, originalVatRate: 20 }, { ...newLine(`L-${index}-2`), label: "Frais avancés", nature: "Debours", gross: 100, vatRate: 0, originalVatRate: 0 }] };
    row.totals = calculateInvoice(row); row.lines = row.totals.lines; row.paid = row.settled ? row.totals.net : index === 2 ? 300 : 0; return row;
  });
  return { clients, invoices };
}
const wait = (delay, signal) => new Promise((resolve, reject) => { const abort = () => { clearTimeout(timer); reject(new DOMException("Annulé", "AbortError")); }; const timer = setTimeout(() => { signal?.removeEventListener("abort", abort); resolve(); }, delay); if (signal?.aborted) abort(); else signal?.addEventListener("abort", abort, { once: true }); });
export function createMockInvoiceRepository({ seed = invoiceSeed(), delay = 200 } = {}) {
  const state = structuredClone(seed); let sequence = 0;
  const snapshot = (updatedId) => ({ ...structuredClone(state), updatedId });
  const find = (id) => { const row = state.invoices.find((item) => item.id === id); guard(row, "La facture n’est plus disponible."); return row; };
  return {
    async list({ signal } = {}) { await wait(delay, signal); return snapshot(); },
    async create(value, { signal } = {}) {
      await wait(delay, signal); validateInvoice(value, state.clients); const id = `FV-NEW-${++sequence}`; const number = value.number.trim() || `FV-DEMO-N${sequence}`;
      guard(!state.invoices.some((row) => row.number.toLowerCase() === number.toLowerCase()), "Ce numéro de facture est déjà utilisé.");
      const row = { ...structuredClone(value), id, number, type: "Facture", status: "Émise", clientName: state.clients.find((client) => client.id === value.clientId).label, exemption: "", needsRecalculation: false, settled: false, paid: 0, rasIsRate: 0, rasTvaRate: 0, rate: Number(value.rate), lines: value.lines.map((line) => ({ ...structuredClone(line), id: `L-NEW-${++sequence}`, originalVatRate: Number(line.vatRate) })) };
      row.totals = calculateInvoice(row); row.lines = row.totals.lines; state.invoices.unshift(row); return snapshot(id);
    },
    async recalculate(id, { signal } = {}) { await wait(delay, signal); const row = find(id); const totals = calculateInvoice(row); row.totals = totals; row.lines = totals.lines; row.needsRecalculation = false; return snapshot(id); },
    async credit(id, value, { signal } = {}) {
      await wait(delay, signal); const source = find(id); guard(source.type === "Facture", "Un avoir total ne peut être créé que depuis une facture."); guard(!source.needsRecalculation, "Recalculez la facture avant de créer l’avoir.");
      const copyId = `AV-NEW-${++sequence}`, copy = { ...structuredClone(source), id: copyId, number: `AV-DEMO-${sequence}`, type: "Avoir", status: "Émis", settled: Boolean(value.settled), paid: value.settled ? -source.totals.net : 0, sourceId: source.id, sourceNumber: source.number, creditId: null, creditNumber: null, lines: source.lines.map((line) => ({ ...structuredClone(line), id: `AL-NEW-${++sequence}`, gross: -Number(line.gross), net: -line.net, ht: -line.ht, vat: -line.vat })) };
      copy.totals = Object.fromEntries(Object.entries(source.totals).filter(([key]) => key !== "lines").map(([key, amount]) => [key, -amount])); copy.totals.lines = copy.lines;
      source.creditId = copy.id; source.creditNumber = copy.number; state.invoices.unshift(copy); return snapshot(copy.id);
    },
    async exempt(id, value, { signal } = {}) {
      await wait(delay, signal); const row = find(id); guard(row.lines.some((line) => Number(line.originalVatRate) !== 0), "Aucune ligne n’est concernée par cette exonération.");
      row.lines = row.lines.map((line) => ({ ...line, vatRate: 0, vat: 0 })); row.exemption = value.comment.trim(); row.needsRecalculation = true; return snapshot(id);
    },
  };
}
