export const normalizeSearch = (value) => String(value ?? "").normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLocaleLowerCase("fr").trim();
const nameKey = (value) => value.trim().replace(/ /g, "").toLocaleLowerCase("fr");
export const legalFields = ["rcClient", "centreRcClient", "patenteClient", "ifClient", "cnssClient", "iceClient"];
export const clientErrorMessages = {
  required: "La raison sociale est obligatoire.", duplicate_name: "Cette raison sociale est déjà utilisée.",
  duplicate_account: "Ce compte comptable est déjà utilisé par un autre client.",
  account_required: "Un compte comptable est obligatoire pour valider ce client.",
  foreign_identifiers: "Un client hors du Maroc ne doit porter aucun identifiant marocain (RC, Centre RC, Patente, IF, CNSS, ICE).",
  validated: "Un client validé ne peut pas être supprimé.", referenced: "Ce client possède des références métier ou des coordonnées bancaires. Sa suppression est bloquée.",
  principal: "L’adresse principale ne peut pas être supprimée.", address_required: "Une ligne d’adresse et une ville sont obligatoires.",
  duplicate_rib: "Ce RIB est déjà utilisé par un client.", missing: "Ce client ou cet élément n’est plus disponible.",
  unavailable: "L’opération n’a pas pu aboutir. Réessayez.",
};
export class ClientRuleError extends Error { constructor(code) { super(clientErrorMessages[code]); this.code = code; } }
export const requireRule = (condition, code) => { if (!condition) throw new ClientRuleError(code); };
export function checkClient(values, rows, id) {
  requireRule(Boolean(values.raisonSociale?.trim()), "required");
  requireRule(!rows.some((row) => row.keyClient !== id && nameKey(row.raisonSociale) === nameKey(values.raisonSociale)), "duplicate_name");
  if (values.compteComptable?.trim()) requireRule(!rows.some((row) => row.keyClient !== id && row.compteComptable === values.compteComptable.trim()), "duplicate_account");
}
export function checkValidation(client) {
  requireRule(client.paysClient?.trim().toUpperCase() === "MAROC" || !legalFields.some((field) => client[field]?.trim()), "foreign_identifiers");
  requireRule(Boolean(client.compteComptable?.trim()), "account_required");
}
export function deletionReason(client) {
  return client.valide ? clientErrorMessages.validated : client.references?.length || client.banques?.length ? clientErrorMessages.referenced : null;
}
export function addressErrors(values) {
  return { ...(!values.adresse1?.trim() && !values.adresse2?.trim() ? { adresse1: "Renseignez au moins une ligne d’adresse." } : {}),
    ...(!values.ville?.trim() ? { ville: "La ville est obligatoire." } : {}) };
}
