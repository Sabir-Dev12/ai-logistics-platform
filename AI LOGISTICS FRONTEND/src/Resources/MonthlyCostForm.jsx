import { useState } from "react";
import PropTypes from "prop-types";
import { Button, ErrorState, Input, Select } from "../design-system";
import { currencies as defaultCurrencies } from "../SalesInvoices/invoiceRules";
export default function MonthlyCostForm({ resource, subject = "cet employé", busy, blocked = false, currencies = defaultCurrencies, error, onSubmit, onCancel }) {
  const [cost, setCost] = useState(resource?.cost ?? ""), [currency, setCurrency] = useState(resource?.costCurrency || "MAD");
  return <form className="bookings-form" noValidate onSubmit={(event) => { event.preventDefault(); if (!busy && !blocked) void onSubmit({ cost, currency }); }}><p>Renseignez le coût mensuel de {subject} et sa devise. Laissez le montant vide pour retirer le coût renseigné ; zéro reste un coût valide.</p><div className="bookings-fields"><Input label="Coût mensuel" type="number" min="0" step="0.01" value={cost} disabled={busy || blocked} onChange={(event) => setCost(event.target.value)} /><Select label="Devise du coût" value={currency} disabled={busy || blocked} onChange={(event) => setCurrency(event.target.value)}>{currencies.map((value) => <option key={value}>{value}</option>)}</Select></div>{error && <ErrorState message={error} />}<div className="bookings-actions"><Button type="submit" variant="primary" loading={busy} disabled={busy || blocked}>Enregistrer le coût</Button><Button disabled={busy || blocked} onClick={onCancel}>Annuler</Button></div></form>;
}
MonthlyCostForm.propTypes = { resource: PropTypes.object, subject: PropTypes.string, busy: PropTypes.bool, blocked: PropTypes.bool, currencies: PropTypes.arrayOf(PropTypes.string), error: PropTypes.string, onSubmit: PropTypes.func.isRequired, onCancel: PropTypes.func.isRequired };
