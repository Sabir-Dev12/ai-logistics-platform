import { confirmedBusinessWrite, businessKey, businessPage, createBusinessApi, dateOnly, invalidBusinessResponse } from "./businessApi";
const status = value => ({ NonValide: "Non validé", Valide: "Validé", Supprime: "Supprimé", 0: "Non validé", 1: "Validé", 2: "Supprimé" })[value];
const column = row => ({ id: `column-${row.idColonne}`, columnId: row.idColonne, label: row.libelleColonne || "", from: row.trancheDebut, to: row.trancheFin, formula: row.formuleCalcul, minimum: row.mtMin, comment: row.commentaire });
const columnInput = row => ({ idColonne: row.columnId, libelleColonne: row.label || null, trancheDebut: row.from, trancheFin: row.to, formuleCalcul: row.formula || null, mtMin: row.minimum, commentaire: row.comment || null });
export function createScalesService({ request }) {
  const call = createBusinessApi(request), path = id => `/api/v1/baremes-achat/${businessKey(id)}`;
  const map = (r, full = false) => {
    if (!r || typeof r.keyBareme !== "string" || !status(r.statut)) throw invalidBusinessResponse();
    if (full && [r.colonnes, r.details, r.prestations].some(value => !Array.isArray(value))) throw invalidBusinessResponse();
    return { id: r.keyBareme, label: r.libelleBareme || "", supplier: r.fournisseur || "", type: r.typeBareme || "", status: status(r.statut), from: dateOnly(r.debutValidite), to: dateOnly(r.finValidite), currency: r.devise || "", flow: r.flux || "", base: r.baseCalcul || "", incoterm: r.incoterm || "", rounding1: r.arrondi1, rounding2: r.arrondi2,
      columnCount: r.nbColonnes, serviceCount: r.nbPrestations, columns: (r.colonnes || []).map(column), cells: (r.details || []).map((x, i) => ({ id: `cell-${i}`, columnId: x.idColonne, department: x.departement, amount: x.montant })),
      services: (r.prestations || []).map(x => ({ id: x.keyLigne, code: x.codeRubrique, label: x.libelleRubrique, amount: x.mtDevise, currency: x.devise, base: x.baseCalcul })) };
  };
  const get = async (id, { signal } = {}) => { const row = map(await call(path(id), { signal }), true); if (row.id !== id) throw invalidBusinessResponse(); return row; };
  const header = value => ({ libelleBareme: value.label, typeBareme: ({ Complet: 0, Groupage: 1 })[value.type] ?? null, fournisseur: value.supplier || null, baseCalcul: value.base || null, debutValidite: value.from || null, finValidite: value.to || null, devise: value.currency || null, flux: ({ National: 0, International: 1 })[value.flow] ?? null, incoterm: value.incoterm || null, arrondi1: value.rounding1, arrondi2: value.rounding2, ...(value.columns ? { colonnes: value.columns.map(columnInput) } : {}) });
  const write = async (url, method, body, signal, expected = 200) => { const raw = await call(url, { method, body, signal, expected }); if (expected === 204) return { updatedId: null }; const row = confirmedBusinessWrite(() => map(raw, true)); return { row, updatedId: row.id }; };
  return { remote: true, get,
    async list({ search = "", status: state = "", type = "", page = 1, pageSize = 5, signal } = {}) {
      const raw = await businessPage(call, "/api/v1/baremes-achat", { libelle: search.trim(), statut: { "Non validé": "NonValide", "Validé": "Valide" }[state] || "", typeBareme: type, page, pageSize }, signal);
      return { scales: raw.items.map(row => map(row)), totalCount: raw.totalCount };
    },
    create: (value, { signal } = {}) => write("/api/v1/baremes-achat", "POST", header(value), signal, 201),
    update: (id, value, { signal } = {}) => write(path(id), "PUT", header(value), signal),
    validate: (id, { signal } = {}) => write(path(id) + "/valider", "POST", undefined, signal),
    duplicate: (id, value, { signal } = {}) => write(path(id) + "/dupliquer", "POST", { libelleBareme: value.label }, signal, 201),
    remove: (id, { signal } = {}) => write(path(id), "DELETE", undefined, signal, 204),
    setLines(id, value, { signal } = {}) {
      const suffix = { columns: "colonnes", cells: "details", services: "prestations" }[value.kind]; if (!suffix) throw invalidBusinessResponse();
      const input = value.lines.map(row => value.kind === "columns" ? columnInput(row) : value.kind === "cells" ? { idColonne: row.columnId, departement: row.department, montant: row.amount } : { codeRubrique: row.code, libelleRubrique: row.label, mtDevise: row.amount, devise: row.currency, baseCalcul: row.base });
      return write(path(id) + "/" + suffix, "PUT", input, signal);
    },
    async resolve(id, department, value, { signal } = {}) { const r = await call(path(id) + `/tarif?${new URLSearchParams({ departement: department, valeur: String(value) })}`, { signal }); if (!r || typeof r.trouve !== "boolean") throw invalidBusinessResponse(); return { found: r.trouve, amount: r.montant, currency: r.devise, column: { columnId: r.idColonne, label: r.libelleColonne } }; },
  };
}
