export const quotationStatuses = { Saisie: "warning", Envoyée: "info", Gagnée: "success", Perdue: "error", Annulée: "warning" };
export const products = [{ id: "GROUPAGE_RI", label: "Groupage import routier", groupage: true }, { id: "COMPLET_RE", label: "Complet export routier", groupage: false }, { id: "MARITIME", label: "Transport maritime", groupage: false }];
export const currencies = ["MAD", "EUR", "USD", "GBP"];
export class QuotationError extends Error {}
export const guard = (condition, message) => { if (!condition) throw new QuotationError(message); };
export const isOpen = (row) => ["Saisie", "Envoyée"].includes(row.statut);
export const normalize = (value) => String(value ?? "").normalize("NFD").replace(/[\u0300-\u036f]/g, "").trim().toLowerCase();
export const money = (value) => new Intl.NumberFormat("fr-FR", { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value);
export const round = (value) => Math.sign(value) * Math.round((Math.abs(value) + Number.EPSILON) * 100) / 100;
export const numeric = (value) => value !== "" && value != null && Number.isFinite(Number(value)) && Number(value) >= 0;
export function validateHeader(row, clients) {
  guard(row.numCotation?.trim(), "Le numéro de cotation est obligatoire.");
  guard(products.some((item) => item.id === row.produit), "Sélectionnez un produit valide.");
  guard(clients.some((item) => item.id === row.keyClient), "Sélectionnez un client valide.");
  guard(!row.dateValidite || row.dateValidite >= row.dateCotation.slice(0, 10), "La validité ne peut pas précéder la création.");
}
export function quotationTotals(row) {
  if (row.remote) { const t = row.serverTotals; const missing=t?.devisesSansTaux || []; return { prices: row.prix.map(x=>({...x,native:null,mad:null})), purchases: row.achats.map(x=>({...x,mad:x.mtMad})), missing, complete:Boolean(t && !missing.length), vente:t?.totalVente, achat:t?.totalAchat, marge:t?.marge }; }
  const fx = Object.fromEntries(row.taux.map((item) => [item.devise, Number(item.taux)])); fx.MAD = 1;
  const used = [...new Set([...row.prix, ...row.achats].map((line) => line.devise))];
  const missing = used.filter((currency) => !Number.isFinite(fx[currency]) || fx[currency] <= 0);
  const base = {};
  for (const line of row.prix.filter((line) => line.typeUtilisation === "Montant")) base[line.devise] = (base[line.devise] || 0) + round(Number(line.quantite) * Number(line.montantValeur));
  const prices = row.prix.map((line) => { const native = line.typeUtilisation === "Pourcentage" ? round((base[line.devise] || 0) * Number(line.montantValeur) / 100) : round(Number(line.quantite) * Number(line.montantValeur)); return { ...line, native, mad: fx[line.devise] > 0 ? round(native * fx[line.devise]) : null }; });
  const purchases = row.achats.map((line) => ({ ...line, mad: fx[line.devise] > 0 ? round(Number(line.mtDevise) * fx[line.devise]) : null }));
  const vente = round(prices.reduce((sum, line) => sum + (line.mad || 0), 0)), achat = round(purchases.reduce((sum, line) => sum + (line.mad || 0), 0));
  const complete = !missing.length && Number.isFinite(vente) && Number.isFinite(achat);
  return { prices, purchases, missing, complete, vente: complete ? vente : null, achat: complete ? achat : null, marge: complete ? round(vente - achat) : null };
}
export function validateLines(kind, lines, row) {
  for (const line of lines) {
    if (kind === "prix") { guard(line.libelle?.trim(), "Le libellé de chaque prix est obligatoire."); guard(["Montant", "Pourcentage"].includes(line.typeUtilisation), "Sélectionnez le mode de calcul."); guard(numeric(line.montantValeur) && numeric(line.quantite), "Les quantités et valeurs de prix doivent être positives ou nulles."); guard(currencies.includes(line.devise), "Sélectionnez une devise valide."); }
    if (kind === "achats") { guard(line.libelle?.trim() && line.fournisseur?.trim(), "Le libellé et le fournisseur de chaque achat sont obligatoires."); guard(numeric(line.mtDevise) && Number(line.mtDevise) > 0, "Le montant d’achat doit être supérieur à zéro."); guard(currencies.includes(line.devise), "Sélectionnez une devise valide."); guard(!line.dateValidite || line.dateValidite <= row.dateCotation.slice(0, 10), "La validité d’un achat ne peut pas être postérieure à la création de la cotation."); }
    if (kind === "taux") { guard(currencies.includes(line.devise) && line.devise !== "MAD", "Sélectionnez une devise étrangère valide."); guard(numeric(line.taux) && Number(line.taux) > 0, "Le taux de change doit être supérieur à zéro."); }
    if (kind === "colisages") for (const field of ["qte", "poids", "longueur", "largeur", "hauteur", "volume"]) guard(line[field] == null || line[field] === "" || numeric(line[field]), "Les mesures des colisages doivent être positives ou nulles.");
  }
  if (kind === "taux") guard(new Set(lines.map((line) => line.devise)).size === lines.length, "Chaque devise ne doit apparaître qu’une fois.");
}
