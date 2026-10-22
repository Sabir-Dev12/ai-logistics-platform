export const catalog = {
  company: { label: "Société", singular: "société", readOnly: true, fields: [] },
  countries: { label: "Pays", singular: "pays", create: "Créer un pays", fields: [["alpha2", "Code alpha-2"], ["agreement", "Accord"], ["zone", "Zone pays"]] },
  currencies: { label: "Devises", singular: "devise", create: "Créer une devise", fields: [["invoiceLabel", "Libellé facture"], ["minorUnit", "Unité divisionnaire"], ["decimals", "Nombre de décimales"]] },
  agencies: { label: "Agences", singular: "agence", fields: [["city", "Ville"], ["address", "Adresse"]] },
  vat: { label: "Comptes TVA", singular: "compte TVA", fields: [["rate", "Taux (%)"], ["account", "Compte comptable"]] },
  payments: { label: "Modes de paiement", singular: "mode de paiement", fields: [["dueDays", "Délai (jours)"]] },
  treasuries: { label: "Trésoreries", singular: "trésorerie", fields: [["currency", "Devise"], ["nature", "Nature"]] },
  dossierTypes: { label: "Types de dossier", singular: "type de dossier", readOnly: true, fields: [["nature", "Nature"]] },
  incoterms: { label: "Incoterms", singular: "incoterm", fields: [["edi", "Code EDI"]] },
  clientClasses: { label: "Classes clients", singular: "classe client", fields: [["description", "Description"]] },
  clientStatuses: { label: "Statuts clients", singular: "statut client", fields: [["description", "Description"]] },
  clientRisks: { label: "Risques clients", singular: "risque client", fields: [["description", "Description"]] },
  clientSectors: { label: "Secteurs d’activité", singular: "secteur d’activité", fields: [["description", "Description"]] },
  clientGroups: { label: "Groupes de sociétés", singular: "groupe de sociétés", fields: [["description", "Description"]] },
  settings: { label: "Paramètres", singular: "paramètre", readOnly: true, fields: [["value", "Valeur"], ["description", "Description"]] },
};
export const normalize = (value) => String(value ?? "").normalize("NFD").replace(/[\u0300-\u036f]/g, "").trim().toLowerCase();
export const referenceLimits=(kind,remote=false)=>remote ? {code:kind==="countries"?3:5,label:kind==="countries"?60:50,alpha2:2,agreement:30,zone:30,invoiceLabel:50,minorUnit:50} : {code:20,label:100,alpha2:2,agreement:100,zone:100,invoiceLabel:100,minorUnit:100};
export function validateReference(kind, value, remote=false) {
  const errors = {}, limits=referenceLimits(kind,remote);
  if (!value.code?.trim() || value.code.trim().length > limits.code) errors.code = `Utilisez un code de 1 à ${limits.code} caractères.`;
  if (!value.label?.trim() || value.label.trim().length > limits.label) errors.label = `Utilisez un libellé de 1 à ${limits.label} caractères.`;
  if (kind === "countries" && value.alpha2 && !/^[a-z]{2}$/i.test(value.alpha2.trim())) errors.alpha2 = "Le code alpha-2 contient deux lettres.";
  if (kind === "currencies" && (!/^\d+$/.test(String(value.decimals)) || Number(value.decimals) > 6)) errors.decimals = remote ? "Choisissez un entier entre 0 et 6." : "Choisissez un entier entre 0 et 6 pour la démonstration.";
  if(remote)for(const key of ["agreement","zone","invoiceLabel","minorUnit"])if((value[key]||"").trim().length>limits[key])errors[key]=`Utilisez au maximum ${limits[key]} caractères.`;
  return errors;
}

