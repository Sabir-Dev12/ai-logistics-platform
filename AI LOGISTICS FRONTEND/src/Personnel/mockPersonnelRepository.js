import { guard, validateEmployee } from "./personnelRules";
export function personnelSeed() {
  return {
    functions: [{ id: "$CHAUFFEUR", label: "Chauffeur" }, { id: "EXPLOIT", label: "Agent d’exploitation" }, { id: "COMPTA", label: "Comptable" }],
    services: [{ id: "TRANSPORT", label: "Transport" }, { id: "LOGISTIQUE", label: "Logistique" }, { id: "FINANCE", label: "Finance" }],
    agencies: [{ id: "AG-CASA", label: "Casablanca Démo" }, { id: "AG-TANGER", label: "Tanger Démo" }],
    employees: Array.from({ length: 7 }, (_, index) => ({ id: `PER-DEMO-${index + 1}`, employeeNumber: `MAT-${String(index + 1).padStart(3, "0")}`, lastName: ["Amrani", "Bennani", "El Idrissi", "Alaoui", "Tazi", "Mansouri", "Fassi"][index], firstName: ["Sami", "Lina", "Yassine", "Nora", "Adam", "Salma", "Rayan"][index], functionId: ["$CHAUFFEUR", "EXPLOIT", "COMPTA"][index % 3], serviceId: ["TRANSPORT", "LOGISTIQUE", "FINANCE"][index % 3], agencyId: index % 2 ? "AG-TANGER" : "AG-CASA", city: index % 2 ? "Tanger" : "Casablanca", mobile: "", email: `employe${index + 1}@example.test`, hireDate: `2024-0${index + 1}-01`, departureDate: index > 4 ? "2026-09-30" : "", departureReason: index > 4 ? "Fin de contrat — démonstration" : "", blocked: index > 4, identifier: "", cost: index === 2 ? null : index === 3 ? 0 : 7000 + index * 500, costCurrency: index === 2 ? "" : "MAD" }))
  };
}
const wait = (delay, signal) => new Promise((resolve, reject) => { const abort = () => { clearTimeout(timer); reject(new DOMException("Annulé", "AbortError")); }; const timer = setTimeout(() => { signal?.removeEventListener("abort", abort); resolve(); }, delay); if (signal?.aborted) abort(); else signal?.addEventListener("abort", abort, { once: true }); });
const identity = (value) => Object.fromEntries(["lastName", "firstName", "employeeNumber", "functionId", "serviceId", "agencyId", "city", "mobile", "email", "hireDate"].map((key) => [key, String(value[key] || "").trim()]));
export function createMockPersonnelRepository({ seed = personnelSeed(), delay = 200 } = {}) {
  const state = structuredClone(seed); let sequence = 0;
  const snapshot = (updatedId) => ({ ...structuredClone(state), updatedId });
  const find = (id) => { const row = state.employees.find((employee) => employee.id === id); guard(row, "L’employé n’est plus disponible."); return row; };
  return {
    async list({ signal } = {}) { await wait(delay, signal); return snapshot(); },
    async create(value, { signal } = {}) { await wait(delay, signal); validateEmployee(value, state.employees, state); const id = `PER-NEW-${++sequence}`; state.employees.unshift({ ...identity(value), id, departureDate: "", departureReason: "", blocked: false, identifier: "", cost: null, costCurrency: "" }); return snapshot(id); },
    async update(id, value, { signal } = {}) { await wait(delay, signal); const row = find(id); validateEmployee(value, state.employees, state, id); Object.assign(row, identity(value)); return snapshot(id); },
    async depart(id, value, { signal } = {}) { await wait(delay, signal); const row = find(id); guard(!row.departureDate, "Une sortie est déjà enregistrée pour cet employé."); guard(value.date, "La date de sortie est obligatoire."); guard(!row.hireDate || value.date >= row.hireDate, "La date de sortie ne peut pas précéder l’embauche."); row.departureDate = value.date; row.departureReason = value.reason.trim(); row.blocked = true; row.identifier = ""; return snapshot(id); },
    async setCost(id, value, { signal } = {}) { await wait(delay, signal); const row = find(id); const cost = value.cost === "" ? null : Number(value.cost); guard(cost == null || (Number.isFinite(cost) && cost >= 0 && cost < 1e12), "Le coût mensuel doit être positif ou nul et dans la plage autorisée."); guard(cost == null || ["MAD", "EUR", "USD", "GBP"].includes(value.currency), "Sélectionnez la devise du coût mensuel."); row.cost = cost; row.costCurrency = cost == null ? "" : value.currency; return snapshot(id); },
  };
}
