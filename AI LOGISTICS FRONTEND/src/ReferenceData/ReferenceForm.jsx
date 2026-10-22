import { useState } from "react";
import PropTypes from "prop-types";
import { Button, ErrorState, Input, Select } from "../design-system";
import { catalog, validateReference, referenceLimits } from "./referenceCatalog";
export default function ReferenceForm({ kind, remote=false, blocked=false, busy, error, onSubmit, onCancel }) {
  const [value, setValue] = useState({ code: "", label: "", alpha2: "", agreement: "", zone: "", invoiceLabel: "", minorUnit: "", decimals: "2" }), [errors, setErrors] = useState({});
  const edit = (key) => (event) => { setValue((current) => ({ ...current, [key]: event.target.value })); setErrors({}); };
  const limits=referenceLimits(kind,remote);
  const submit = (event) => { event.preventDefault(); if (busy || blocked) return; const validation = validateReference(kind, value, remote); setErrors(validation); if (!Object.keys(validation).length) void onSubmit(value); };
  return <form className="bookings-form" noValidate onSubmit={submit}><div className="bookings-fields"><Input label="Code" value={value.code} maxLength={limits.code} required disabled={busy || blocked} onChange={edit("code")} error={errors.code} /><Input label="Libellé" value={value.label} maxLength={limits.label} required disabled={busy || blocked} onChange={edit("label")} error={errors.label} />{catalog[kind].fields.map(([key, label]) => key === "decimals" ? <Select key={key} label={label} value={value[key]} disabled={busy || blocked} onChange={edit(key)} error={errors[key]}>{[0, 1, 2, 3, 4, 5, 6].map((number) => <option key={number} value={number}>{number}</option>)}</Select> : <Input key={key} label={label} maxLength={limits[key] || 100} value={value[key]} disabled={busy || blocked} onChange={edit(key)} error={errors[key]} />)}</div>{error && <ErrorState message={error} />}<p>{remote ? "La référence sera créée dans le backend." : "Création simulée, conservée uniquement dans cette page."}</p><div className="bookings-actions"><Button type="submit" variant="primary" loading={busy} disabled={busy || blocked}>Enregistrer</Button><Button disabled={busy || blocked} onClick={onCancel}>Annuler</Button></div></form>;
}
ReferenceForm.propTypes = { kind: PropTypes.string.isRequired, remote: PropTypes.bool, blocked: PropTypes.bool, busy: PropTypes.bool, error: PropTypes.string, onSubmit: PropTypes.func.isRequired, onCancel: PropTypes.func.isRequired };
