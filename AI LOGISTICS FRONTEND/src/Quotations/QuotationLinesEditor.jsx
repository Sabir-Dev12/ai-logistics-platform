import { useId, useRef, useState } from "react";
import PropTypes from "prop-types";
import { Alert, Button, Card, EmptyState, ErrorState, Input, Select } from "../design-system";
import { currencies, validateLines } from "./quotationRules";
const fieldsByKind = {
  prix: [["libelle", "Libellé"], ["quantite", "Quantité", "number"], ["typeUtilisation", "Calcul", ["Montant", "Pourcentage"]], ["montantValeur", "Prix unitaire ou taux (%)", "number"], ["devise", "Devise", currencies]],
  achats: [["libelle", "Libellé"], ["fournisseur", "Fournisseur"], ["mtDevise", "Montant d’achat", "number"], ["devise", "Devise", currencies], ["dateValidite", "Validité de l’achat", "date"]],
  taux: [["devise", "Devise", currencies.filter((value) => value !== "MAD")], ["taux", "Taux vers MAD", "number"]],
};
export default function QuotationLinesEditor({ row, kind, busy, error, onSubmit, onCancel }) {
  const [lines, setLines] = useState(() => structuredClone(row[kind].filter((line) => kind !== "taux" || line.devise !== "MAD")));
  const [validation, setValidation] = useState(""); const sequence = useRef(0), prefix = useId();
  const submit = (event) => { event.preventDefault(); if (busy) return; try { validateLines(kind, lines, row); } catch (failure) { setValidation(failure.message); return; } void onSubmit(lines); };
  return <form className="bookings-form" onSubmit={submit} noValidate>{kind === "taux" && <Alert title="Devise de base">1 MAD = 1 MAD. Ce taux est conservé automatiquement.</Alert>}{kind === "prix" && <p>Montant : quantité × prix unitaire. Pourcentage : taux appliqué aux lignes au montant de la même devise.</p>}
    {!lines.length && <EmptyState title="Aucune ligne" description="Ajoutez une ligne ou enregistrez une liste vide." />}
    {lines.map((line, index) => <Card className="bookings-line" key={line.id}><fieldset disabled={busy}><legend>Ligne {index + 1}</legend><div className="bookings-fields">{fieldsByKind[kind].map(([key, label, type = "text"]) => { const change = (event) => { setLines((current) => current.map((item) => item.id === line.id ? { ...item, [key]: event.target.value } : item)); setValidation(""); }; return Array.isArray(type) ? <Select key={key} label={`${label} — ligne ${index + 1}`} value={line[key]} onChange={change}>{type.map((value) => <option key={value}>{value}</option>)}</Select> : <Input key={key} label={`${label} — ligne ${index + 1}`} type={type} min={type === "number" ? 0 : undefined} step={type === "number" ? "any" : undefined} value={line[key] ?? ""} onChange={change} />; })}</div><Button onClick={() => { setLines((current) => current.filter((item) => item.id !== line.id)); setValidation(""); }}>Retirer la ligne {index + 1}</Button></fieldset></Card>)}
    <Button disabled={busy} onClick={() => { setLines((current) => [...current, { id: `${prefix}-${++sequence.current}`, libelle: "", quantite: 1, typeUtilisation: "Montant", montantValeur: 0, mtDevise: "", fournisseur: "", devise: kind === "taux" ? "EUR" : "MAD", taux: "" }]); setValidation(""); }}>Ajouter une ligne</Button>
    {(validation || error) && <ErrorState message={validation || error} />}<div className="bookings-actions"><Button disabled={busy} onClick={onCancel}>Annuler</Button><Button type="submit" variant="primary" loading={busy}>Enregistrer les lignes</Button></div>
  </form>;
}
QuotationLinesEditor.propTypes = { row: PropTypes.object.isRequired, kind: PropTypes.oneOf(["prix", "achats", "taux"]).isRequired, busy: PropTypes.bool, error: PropTypes.string, onSubmit: PropTypes.func.isRequired, onCancel: PropTypes.func.isRequired };
