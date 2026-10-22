import { guard, validateHeader, validateLines } from "./scaleRules";
export function scaleSeed() {
  return { scales: Array.from({ length: 7 }, (_, index) => ({ id: `BA-DEMO-${index + 1}`, label: `Transport ${index % 2 ? "international" : "national"} Démo ${index + 1}`, supplier: index % 2 ? "Azur Transport Démo" : "Atlas Transit Démo", type: index % 2 ? "Complet" : "Groupage", base: "Poids (kg)", from: "2026-01-01", to: "2026-12-31", currency: index % 2 ? "EUR" : "MAD", flow: index % 2 ? "International" : "National", incoterm: "DAP", rounding1: null, rounding2: null, status: index === 1 ? "Non validé" : "Validé",
    columns: [{ id: "C1", columnId: 1, label: "Moins de 100 kg", from: 0, to: 100, minimum: null, formula: "", comment: "" }, { id: "C2", columnId: 2, label: "À partir de 100 kg", from: 100, to: null, minimum: null, formula: "", comment: "" }],
    cells: [{ id: "D1", columnId: 1, department: "75", amount: 150 + index * 10 }, { id: "D2", columnId: 2, department: "75", amount: 250 + index * 10 }, { id: "D3", columnId: 1, department: "13", amount: 180 + index * 10 }],
    services: [{ id: "P1", code: "MANUT", label: "Manutention", amount: 40, currency: index % 2 ? "EUR" : "MAD", base: "Forfait" }] })) };
}
const wait = (delay, signal) => new Promise((resolve, reject) => { const abort = () => { clearTimeout(timer); reject(new DOMException("Annulé", "AbortError")); }; const timer = setTimeout(() => { signal?.removeEventListener("abort", abort); resolve(); }, delay); if (signal?.aborted) abort(); else signal?.addEventListener("abort", abort, { once: true }); });
export function createMockScaleRepository({ seed = scaleSeed(), delay = 200 } = {}) {
  const state = structuredClone(seed); let sequence = 0;
  const snapshot = (updatedId) => ({ ...structuredClone(state), updatedId });
  const find = (id) => { const row = state.scales.find((item) => item.id === id); guard(row && row.status !== "Supprimé", "Ce barème n’est plus disponible."); return row; };
  const editable = (row) => guard(row.status === "Validé", "Seul un barème validé peut être modifié ou dupliqué.");
  const unique = (label, id) => guard(!state.scales.some((row) => row.id !== id && row.status !== "Supprimé" && row.label.toLowerCase() === label.trim().toLowerCase()), "Ce libellé de barème est déjà utilisé.");
  const lines = (items) => structuredClone(items).map((item) => ({ ...item, id: `L-NEW-${++sequence}` }));
  return {
    async list({ signal } = {}) { await wait(delay, signal); return snapshot(); },
    async create(value, { signal } = {}) {
      await wait(delay, signal); validateHeader(value); unique(value.label); validateLines("columns", value.columns);
      guard(value.columns.length, "Ajoutez au moins une colonne initiale.");
      const id = `BA-NEW-${++sequence}`; state.scales.unshift({ ...structuredClone(value), id, label: value.label.trim(), status: "Non validé", columns: lines(value.columns), cells: [], services: [] }); return snapshot(id);
    },
    async update(id, value, { signal } = {}) { await wait(delay, signal); const row = find(id); editable(row); validateHeader(value); unique(value.label, id); const { columns: _columns, cells: _cells, services: _services, status: _status, id: _id, ...header } = value; void _columns; void _cells; void _services; void _status; void _id; Object.assign(row, structuredClone(header), { label: value.label.trim() }); return snapshot(id); },
    async validate(id, { signal } = {}) { await wait(delay, signal); const row = find(id); guard(row.columns.length, "Un barème doit comporter au moins une colonne."); row.status = "Validé"; return snapshot(id); },
    async duplicate(id, value, { signal } = {}) { await wait(delay, signal); const row = find(id); editable(row); guard(value.label?.trim(), "Le libellé de la copie est obligatoire."); unique(value.label); const copyId = `BA-NEW-${++sequence}`; state.scales.unshift({ ...structuredClone(row), id: copyId, label: value.label.trim(), status: "Non validé", columns: lines(row.columns), cells: lines(row.cells), services: lines(row.services) }); return snapshot(copyId); },
    async remove(id, { signal } = {}) { await wait(delay, signal); find(id).status = "Supprimé"; return snapshot(); },
    async setLines(id, value, { signal } = {}) { await wait(delay, signal); const row = find(id); editable(row); guard(["columns", "cells", "services"].includes(value.kind), "Type de ligne inconnu."); validateLines(value.kind, value.lines, row.columns); row[value.kind] = lines(value.lines); if (value.kind === "columns") row.cells = row.cells.filter((cell) => row.columns.some((column) => column.columnId === cell.columnId)); return snapshot(id); },
  };
}
