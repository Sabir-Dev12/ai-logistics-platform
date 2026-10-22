import { useRef, useState } from "react";
import PropTypes from "prop-types";
import { Button, Card, EmptyState, ErrorState, Input, Select } from "../design-system";

const equipmentFields = [["typeEquipement", "Type d’équipement"], ["natureEquipement", "Nature d’équipement"], ["qte", "Quantité", "number"]];
const packingFields = [["typeColis", "Type de colis"], ["qte", "Quantité", "number"], ["poids", "Poids", "number"], ["longueur", "Longueur", "number"], ["largeur", "Largeur", "number"],
  ["hauteur", "Hauteur", "number"], ["volume", "Volume", "number"], ["typeEquipement", "Type d’équipement"], ["natureEquipement", "Nature d’équipement"]];
export default function BookingLinesEditor({ kind, booking, busy, blocked, error, onSubmit, onCancel }) {
  const fields = kind === "equipment" ? equipmentFields : packingFields;
  const [lines, setLines] = useState(() => structuredClone(kind === "equipment" ? booking.equipements : booking.colisages));
  const [validation, setValidation] = useState({}), sequence = useRef(0);
  const change = (id, key, value) => { setLines((current) => current.map((line) => line.id === id ? { ...line, [key]: value } : line)); setValidation({}); };
  const submit = async (event) => {
    event.preventDefault(); if (busy || blocked) return;
    const issues = {};
    for (const line of lines) for (const [key, , type] of fields) if (type === "number"
      && ((kind === "equipment" && key === "qte" && (line[key] == null || line[key] === "")) || (line[key] != null && line[key] !== "" && (!Number.isFinite(Number(line[key])) || Number(line[key]) < 0)))) issues[`${line.id}:${key}`] = "Utilisez un nombre positif ou nul.";
    setValidation(issues); if (Object.keys(issues).length) return;
    const payload = lines.map((line) => ({ ...line, ...Object.fromEntries(fields.filter(([, , type]) => type === "number").map(([key]) => [key, line[key] == null || line[key] === "" ? null : Number(line[key])])) }));
    await onSubmit(payload);
  };
  return <form className="bookings-form" onSubmit={submit} noValidate>
    {lines.length === 0 && <EmptyState title="Aucune ligne" description="Ajoutez une ligne ou enregistrez une liste vide." />}
    {lines.map((line, index) => <Card key={line.id} className="bookings-line"><fieldset disabled={busy}><legend>Ligne {index + 1}</legend><div className="bookings-fields">
      {fields.map(([key, label, type = "text"]) => <Input key={key} label={`${label} — ligne ${index + 1}`} type={type} value={line[key] ?? ""} min={type === "number" ? 0 : undefined} step={type === "number" ? "any" : undefined}
        error={validation[`${line.id}:${key}`]} onChange={(event) => change(line.id, key, event.target.value)} />)}
      {kind === "packing" && <Select label={`Gerbable — ligne ${index + 1}`} value={String(line.gerbable)} onChange={(event) => change(line.id, "gerbable", event.target.value === "true")}><option value="true">Oui</option><option value="false">Non</option></Select>}
    </div><div className="bookings-actions"><Button onClick={() => { setLines((current) => current.filter((item) => item.id !== line.id)); setValidation({}); }}>Retirer la ligne {index + 1}</Button></div></fieldset></Card>)}
    <Button disabled={busy} onClick={() => setLines((current) => [...current, { id: `draft-${++sequence.current}`, qte: 1, gerbable: true }])}>Ajouter une ligne</Button>
    {error && <ErrorState message={error} />}<div className="bookings-actions"><Button disabled={busy} onClick={onCancel}>Annuler</Button><Button type="submit" variant="primary" disabled={blocked} loading={busy}>Enregistrer les lignes</Button></div>
  </form>;
}
BookingLinesEditor.propTypes = { kind: PropTypes.oneOf(["equipment", "packing"]).isRequired, booking: PropTypes.object.isRequired, busy: PropTypes.bool, blocked: PropTypes.bool, error: PropTypes.string, onSubmit: PropTypes.func.isRequired, onCancel: PropTypes.func.isRequired };
