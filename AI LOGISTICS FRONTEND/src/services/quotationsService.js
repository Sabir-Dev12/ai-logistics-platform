import { confirmedBusinessWrite, businessKey, businessPage, createBusinessApi, dateOnly, invalidBusinessResponse } from "./businessApi";
const number = value => value == null || value === "" ? null : Number(value);
export function createQuotationsService({ request }) {
  const call = createBusinessApi(request), originals = new Map(), path = id => `/api/v1/cotations/${businessKey(id)}`;
  const map = (r, full = false) => {
    if (!r || typeof r.keyCotation !== "string" || typeof r.numCotation !== "string" || typeof r.keyClient !== "string") throw invalidBusinessResponse();
    if (full && [r.prix, r.achats, r.colisages, r.tauxChange, r.documents].some(x => !Array.isArray(x))) throw invalidBusinessResponse();
    return { ...r, id: r.keyCotation, remote: true, produit: r.produitCotation || "", dateCotation: dateOnly(r.dateCotation), dateValidite: dateOnly(r.dateValidite), commentaire: r.commentaireCotation || "", motifPerdue: r.motifCotationPerdue, groupId: r.keyRegroupement || "", serverTotals: r.totaux,
      colisages: (r.colisages || []).map(x => ({ ...x, id: x.keyColisageCotation })),
      prix: (r.prix || []).map(x => ({ ...x, id: x.keyLignePrix, libelle: x.libelleRubrique || "", typeUtilisation: x.typeUtilisation === 1 ? "Pourcentage" : x.typeUtilisation === 0 ? "Montant" : x.typeUtilisation })),
      achats: (r.achats || []).map(x => ({ ...x, id: x.keyLigne, libelle: x.libelleRubrique || "", fournisseur: x.libelleFournisseur || "", dateValidite: dateOnly(x.dateValidite) })),
      taux: (r.tauxChange || []).map(x => ({ id: x.devise, devise: x.devise, taux: x.tauxChange })),
      documents: (r.documents || []).map(x => ({ id: x.keyDocument, name: x.libelleDocument || x.keyDocument, type: x.typePieceDocument || "—", path: x.cheminDocument })) };
  };
  const get = async (id, { signal } = {}) => { const raw = await call(path(id), { signal }); const row = map(raw, true); if (row.id !== id) throw invalidBusinessResponse(); originals.set(id, raw); return row; };
  const write = async (url, method, body, signal, expected = 200) => { const raw = await call(url, { method, body, signal, expected }); if (expected === 204) return { updatedId: null }; const row = confirmedBusinessWrite(() => map(raw, true)); originals.set(row.id, raw); return { row, updatedId: row.id }; };
  return { remote: true, get,
    async list({ query = "", status = "", client = "", page = 1, pageSize = 5, signal } = {}) { const raw = await businessPage(call, "/api/v1/cotations", { numCotation: query.trim(), statut: status, keyClient: client, page, pageSize }, signal); return { quotations: raw.items.map(row => map(row)), totalCount: raw.totalCount, clients: [], groups: [] }; },
    async clients({ signal, search = "", page = 1 } = {}) { const r = await businessPage(call, "/api/v1/clients", { search, page, pageSize: 20 }, signal); return { ...r, items: r.items.map(x => ({ id: x.keyClient, label: x.raisonSociale })) }; },
    async products({ signal } = {}) { const rows = await call("/api/v1/reference/types-dossier", { signal }); if (!Array.isArray(rows)) throw invalidBusinessResponse(); return rows.map(x => ({ id: x.typeDossierCode, label: x.intituleType || x.typeDossierCode })); },
    create: (v, { signal } = {}) => write("/api/v1/cotations", "POST", { produitCotation: v.produit, keyClient: v.keyClient, numCotation: v.numCotation || null, typeTransport: v.typeTransport || null, natureMarchandise: v.natureMarchandise || null, dateValidite: v.dateValidite || null, contact: v.contact || null, commercial: v.commercial || null }, signal, 201),
    async update(id, v, { signal } = {}) { if (!originals.has(id)) await get(id, { signal }); const original = originals.get(id); if (!original.editableFields) throw new Error("Rechargez la fiche avant de modifier l’en-tête."); return write(path(id), "PUT", { ...original.editableFields, numCotation: v.numCotation, typeTransport: v.typeTransport || null, natureMarchandise: v.natureMarchandise || null, commentaire: v.commentaire || null, dateValidite: v.dateValidite || null }, signal); },
    duplicate: (id, v, { signal } = {}) => write(path(id) + "/dupliquer", "POST", { numCotation: v.numCotation, contreOffre: false }, signal, 201),
    send: (id, { signal } = {}) => write(path(id) + "/envoyer", "POST", undefined, signal),
    close: (id, v, { signal } = {}) => write(path(id) + "/cloturer", "POST", v, signal),
    cancel: (id, v, { signal } = {}) => write(path(id) + "/annuler", "POST", { motif: v.motif }, signal),
    remove: (id, { signal } = {}) => write(path(id), "DELETE", undefined, signal, 204),
    setLines(id, v, { signal } = {}) {
      const suffix = { prix: "prix", achats: "achats", taux: "taux-change", colisages: "colisages" }[v.kind]; if (!suffix) throw invalidBusinessResponse();
      const body = v.lines.map(x => v.kind === "prix" ? { position: x.position, codeRubrique: x.codeRubrique, libelleRubrique: x.libelle, quantite: number(x.quantite), typeUtilisation: ({ Montant: 0, Pourcentage: 1 })[x.typeUtilisation] ?? null, montantValeur: number(x.montantValeur), devise: x.devise, baseCalcul: x.baseCalcul, arrondi: x.arrondi || false, montantAchat: x.montantAchat, deviseAchat: x.deviseAchat, commentaire: x.commentaire, codeTva: x.codeTva }
        : v.kind === "achats" ? { codeRubrique: x.codeRubrique, libelleRubrique: x.libelle, mtDevise: number(x.mtDevise), devise: x.devise, keyFournisseur: x.keyFournisseur, libelleFournisseur: x.fournisseur, dateValidite: x.dateValidite || null, cheminJustif: x.cheminJustif, libelleJustif: x.libelleJustif }
        : v.kind === "taux" ? { devise: x.devise, tauxChange: number(x.taux) }
        : Object.fromEntries(["typeColis", "qte", "poids", "gerbable", "longueur", "largeur", "hauteur", "volume", "volumeNgerbable", "typeEquipement", "natureEquipement", "largeurEquipement", "mplUnitaire"].map(key => [key, ["typeColis", "gerbable", "typeEquipement", "natureEquipement"].includes(key) ? x[key] : number(x[key])])));
      return write(path(id) + "/" + suffix, "PUT", body, signal);
    },
    addDocument: (id, value, { signal } = {}) => write(path(id) + "/documents", "POST", { libelleDocument: value.name, cheminDocument: value.path, typePieceDocument: value.type || null }, signal),
    removeDocument: (id, value, { signal } = {}) => write(path(id) + `/documents/${businessKey(value.documentId)}`, "DELETE", undefined, signal),
    setGroup(id, value, { signal } = {}) { if (value.newGroupName?.trim()) throw new Error("La création d’un regroupement n’est pas exposée par cette API. Indiquez une référence existante."); return write(path(id) + `/regroupement?${new URLSearchParams({ keyRegroupement: value.groupId || "" })}`, "PUT", undefined, signal); },
  };
}
