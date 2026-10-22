import { useState } from "react";
import PropTypes from "prop-types";
import { Button, ErrorState, Input } from "../design-system";
export default function DocumentReferenceForm({ busy, error, onSubmit, onCancel }) {
  const [name, setName] = useState(""), [path, setPath] = useState(""), [type, setType] = useState("");
  return <form className="bookings-form" onSubmit={event => { event.preventDefault(); if (!busy && name.trim() && path.trim()) void onSubmit({ name, path, type }); }}>
    <p>Associez un document déjà accessible au serveur. Cette API enregistre sa référence, sans téléverser son contenu.</p>
    <Input label="Libellé du document" required value={name} disabled={busy} onChange={event => setName(event.target.value)} />
    <Input label="Chemin du document" required value={path} disabled={busy} onChange={event => setPath(event.target.value)} />
    <Input label="Type de pièce" value={type} disabled={busy} onChange={event => setType(event.target.value)} />
    {error && <ErrorState message={error} />}<div className="bookings-actions"><Button disabled={busy} onClick={onCancel}>Annuler</Button><Button type="submit" variant="primary" loading={busy} disabled={busy || !name.trim() || !path.trim()}>Associer le document</Button></div>
  </form>;
}
DocumentReferenceForm.propTypes = { busy: PropTypes.bool, error: PropTypes.string, onSubmit: PropTypes.func.isRequired, onCancel: PropTypes.func.isRequired };
