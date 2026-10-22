import { useState } from "react";
import PropTypes from "prop-types";
import { Alert, Button, ErrorState, Input, Select } from "../design-system";
import { localDateTime } from "./trackingRules";
export default function TrackingEventForm({ statuses, busy, blocked, error, reference, onSubmit, onCancel }) {
  const [keyStatut, setStatus] = useState(""), [dateEvenement, setDate] = useState(localDateTime), [commentaire, setComment] = useState(""), [validation, setValidation] = useState("");
  const submit = (event) => {
    event.preventDefault(); if (busy||blocked) return;
    if (!statuses.some((status) => status.keyStatut === keyStatut)) { setValidation("Sélectionnez un statut autorisé."); return; }
    if (!dateEvenement || Number.isNaN(new Date(dateEvenement).getTime())) { setValidation("Renseignez une date d’événement valide."); return; }
    if (!commentaire.trim()) { setValidation("Le commentaire de suivi est obligatoire."); return; }
    if (commentaire.length > 2000) { setValidation("Le commentaire est limité à 2 000 caractères."); return; }
    setValidation(""); void onSubmit({ keyStatut, dateEvenement, commentaire });
  };
  return <form className="bookings-form" onSubmit={submit} noValidate><p>{reference}</p><Alert title="Événement de suivi">Cette note ajoute une trace de suivi ; elle ne modifie pas l’état métier de l’entité. L’auteur et la date d’enregistrement sont renseignés automatiquement.</Alert>
    <fieldset disabled={busy}><legend>Nouvel événement</legend><div className="bookings-fields"><Select label="Statut autorisé" value={keyStatut} onChange={(event) => { setStatus(event.target.value); setValidation(""); }} required><option value="">Sélectionnez un statut</option>{statuses.map((status) => <option key={status.keyStatut} value={status.keyStatut}>{status.label}</option>)}</Select>
      <Input label="Date de l’événement" type="datetime-local" required value={dateEvenement} onChange={(event) => { setDate(event.target.value); setValidation(""); }} />
      <Input label="Commentaire de suivi" required maxLength={2000} value={commentaire} onChange={(event) => { setComment(event.target.value); setValidation(""); }} hint="2 000 caractères maximum." />
    </div></fieldset>{(validation || error) && <ErrorState message={validation || error} />}<div className="bookings-actions"><Button disabled={busy} onClick={onCancel}>Annuler</Button><Button type="submit" variant="primary" disabled={blocked} loading={busy}>Enregistrer l’événement</Button></div>
  </form>;
}
TrackingEventForm.propTypes = { statuses: PropTypes.array.isRequired, busy: PropTypes.bool,blocked:PropTypes.bool,error: PropTypes.string, reference: PropTypes.string.isRequired, onSubmit: PropTypes.func.isRequired, onCancel: PropTypes.func.isRequired };
