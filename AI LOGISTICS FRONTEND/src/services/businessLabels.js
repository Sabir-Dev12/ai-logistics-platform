export const businessLabels = { DebitNote: "Note de débit", CreditNote: "Note de crédit", Od: "Opération diverse", Impaye: "Impayé", Etrangers: "Tiers étrangers", RemisAuFournisseur: "Remis au fournisseur", Signe: "Signé" };
export const businessLabel = value => businessLabels[value] || value;
