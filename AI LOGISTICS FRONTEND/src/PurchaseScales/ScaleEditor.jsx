import { useRef, useState } from "react";
import PropTypes from "prop-types";
import { Button, Card, EmptyState, ErrorState, Input, Select } from "../design-system";
import { numberOrNull, validateHeader, validateLines } from "./scaleRules";
const headerFields = [["label", "Libellé"], ["supplier", "Fournisseur"], ["type", "Type", ["Groupage", "Complet"]], ["base", "Base de calcul"], ["from", "Début de validité", "date"], ["to", "Fin de validité", "date"], ["currency", "Devise", ["MAD", "EUR", "USD"]], ["flow", "Flux", ["National", "International"]], ["incoterm", "Incoterm"], ["rounding1", "Arrondi 1", "number"], ["rounding2", "Arrondi 2", "number"]];
const definitions = {
  columns: [["columnId", "Numéro de colonne", "number"], ["label", "Libellé de colonne"], ["from", "Début de tranche", "number"], ["to", "Fin de tranche (exclue)", "number"], ["minimum", "Montant minimum", "number"], ["formula", "Formule (métadonnée)"], ["comment", "Commentaire"]],
  cells: [["columnId", "Colonne", "column"], ["department", "Département"], ["amount", "Montant", "number"]],
  services: [["code", "Code rubrique"], ["label", "Libellé de prestation"], ["amount", "Montant", "number"], ["currency", "Devise", ["MAD", "EUR", "USD"]], ["base", "Base de calcul"]],
};
export default function ScaleEditor({ kind = "header", scale, busy, error, onSubmit, onCancel }) {
  const creating = kind === "header" && !scale?.id;
  const [header, setHeader] = useState(() => structuredClone(scale || { label: "", supplier: "", type: "Groupage", base: "Poids (kg)", from: "", to: "", currency: "MAD", flow: "National", incoterm: "", rounding1: null, rounding2: null }));
  const [rows, setRows] = useState(() => kind === "header" ? [{ id: "initial", columnId: 1, label: "Tranche initiale", from: 0, to: null, minimum: null, formula: "", comment: "" }] : structuredClone(scale[kind]));
  const [validation, setValidation] = useState(""); const sequence = useRef(0);
  const rowKind = kind === "header" ? "columns" : kind;
  const updateRow = (index, key, value) => setRows((current) => current.map((row, rowIndex) => rowIndex === index ? { ...row, [key]: value } : row));
  const fields = (schema, value, update, suffix = "") => <div className="bookings-fields">{schema.map(([key, label, type]) => {
    const props = { label: `${label}${suffix}`, value: value[key] ?? "", disabled: busy, onChange: (event) => { setValidation(""); update(key, type === "number" || type === "column" ? numberOrNull(event.target.value) : event.target.value); } };
    return Array.isArray(type) || type === "column" ? <Select key={key} {...props}>{type === "column" ? <><option value="">Sélectionner une colonne</option>{scale.columns.map((column) => <option key={column.columnId} value={column.columnId}>{column.columnId} · {column.label || "Sans libellé"}</option>)}</> : type.map((option) => <option key={option}>{option}</option>)}</Select> : <Input key={key} {...props} type={type || "text"} step={key === "columnId" ? "1" : "any"} />;
  })}</div>;
  const submit = (event) => {
    event.preventDefault(); if (busy) return;
    try { if (kind === "header") { validateHeader(header); if (creating) validateLines("columns", rows); } else validateLines(kind, rows, scale.columns); setValidation(""); onSubmit(kind === "header" ? { ...header, ...(creating ? { columns: rows } : {}) } : { kind, lines: rows }); }
    catch (failure) { setValidation(failure.message); }
  };
  return <form className="bookings-form" onSubmit={submit} noValidate>
    {kind === "header" && <fieldset><legend>Informations du barème</legend>{fields(headerFields, header, (key, value) => setHeader((current) => ({ ...current, [key]: value })))}</fieldset>}
    {(creating || kind !== "header") && <><p>{rowKind === "columns" ? "Les tranches incluent le début et excluent la fin. Une borne vide reste ouverte. Retirer une colonne retire ses cellules." : rowKind === "cells" ? "Une cellule par département et colonne. Un montant vide indique un tarif indisponible." : "Prestations associées au barème, conservées séparément du tarif résolu."}</p>
      {!rows.length && <EmptyState title="Aucune ligne" description="Ajoutez une ligne pour compléter le barème." />}
      {rows.map((row, index) => <Card className="bookings-line" key={row.id}><fieldset><legend>Ligne {index + 1}</legend>{fields(definitions[rowKind], row, (key, value) => updateRow(index, key, value), ` — ligne ${index + 1}`)}</fieldset><div className="bookings-actions"><Button disabled={busy} onClick={() => setRows((current) => current.filter((_, rowIndex) => rowIndex !== index))}>Retirer la ligne {index + 1}</Button></div></Card>)}
      <Button disabled={busy} onClick={() => { const columnId = Math.max(0, ...rows.map((row) => row.columnId || 0)) + 1; setRows((current) => [...current, { id: `editor-${++sequence.current}`, ...(rowKind === "columns" ? { columnId, label: "", from: null, to: null, minimum: null, formula: "", comment: "" } : rowKind === "cells" ? { columnId: scale.columns[0]?.columnId ?? null, department: "", amount: null } : { code: "", label: "", amount: null, currency: scale.currency, base: "" }) }]); }}>Ajouter une ligne</Button></>}
    {(validation || error) && <ErrorState message={validation || error} />}
    <div className="bookings-actions"><Button type="submit" variant="primary" loading={busy} disabled={busy}>Enregistrer {kind === "header" ? "le barème" : "les lignes"}</Button><Button disabled={busy} onClick={onCancel}>Annuler</Button></div>
  </form>;
}
ScaleEditor.propTypes = { kind: PropTypes.string, scale: PropTypes.object, busy: PropTypes.bool, error: PropTypes.string, onSubmit: PropTypes.func.isRequired, onCancel: PropTypes.func.isRequired };
