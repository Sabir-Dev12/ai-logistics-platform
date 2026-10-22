import { useState } from "react";
import PropTypes from "prop-types";
import { Button, ErrorState, Input } from "../design-system";
import { addressErrors } from "./clientRules";
import { clientGroups } from "./clientFields";
const addressFields = [["adresse1", "Adresse ligne 1"], ["adresse2", "Adresse ligne 2"], ["cp", "Code postal"], ["ville", "Ville de livraison"], ["pays", "Pays de livraison"], ["contact", "Contact"], ["telAdresse", "Téléphone de livraison"], ["faxAdresse", "Fax de livraison"]];
const bankFields = [["banque", "Banque"], ["compteRib", "RIB"], ["codeBanque", "Agence bancaire"], ["adresseBanque", "Ville de la banque"], ["swift", "SWIFT"], ["ibanCode", "IBAN"]];

export default function ClientForm({ kind = "client", client, remote = false, busy, error, onSubmit, onCancel }) {
  const groups = kind === "address" ? [{ title: "Adresse de livraison", fields: addressFields }] : kind === "bank" ? [{ title: "Coordonnées bancaires", fields: bankFields }] : clientGroups;
  const [values, setValues] = useState(() => Object.fromEntries(groups.flatMap((group) => group.fields.map(([field]) => [field, client?.[field] || ""]))));
  const [errors, setErrors] = useState({});
  const submit = async (event) => {
    event.preventDefault(); if (busy) return;
    const validation = kind === "address" ? addressErrors(values) : kind === "bank" ? {} : {
      ...(!values.raisonSociale.trim() ? { raisonSociale: "La raison sociale est obligatoire." } : {}),
      ...(client?.compteComptable && !values.compteComptable.trim() ? { compteComptable: "Le compte existant doit être renseigné pour le remplacer." } : {}),
    };
    setErrors(validation); if (Object.keys(validation).length) return;
    await onSubmit(values);
  };
  return <form className="clients-form" onSubmit={submit} noValidate>
    {groups.map((group) => <fieldset key={group.title} disabled={busy}><legend>{group.title}</legend>
      {group.title === "Identifiants marocains" && <p>À laisser vides pour un client dont le pays n’est pas MAROC.</p>}
      <div className="clients-fields">{group.fields.map(([field, label]) => <Input key={field} label={label} value={values[field]}
        required={field === "raisonSociale" || (kind === "address" && field === "ville")} error={errors[field]}
        type={field.toLowerCase().includes("tel") || field === "faxClient" ? "tel" : "text"}
        onChange={(event) => { setValues((current) => ({ ...current, [field]: event.target.value })); setErrors({}); }} />)}</div>
    </fieldset>)}
    {kind === "bank" && <p>{remote ? "Le RIB doit être unique." : "Coordonnées fictives pour cette démonstration. Le RIB doit être unique."}</p>}
    {error && <ErrorState message={error} />}
    <div className="clients-actions"><Button disabled={busy} onClick={onCancel}>Annuler</Button><Button variant="primary" type="submit" loading={busy}>Enregistrer</Button></div>
  </form>;
}
ClientForm.propTypes = { kind: PropTypes.oneOf(["client", "address", "bank"]), client: PropTypes.object, remote: PropTypes.bool, busy: PropTypes.bool, error: PropTypes.string,
  onSubmit: PropTypes.func.isRequired, onCancel: PropTypes.func.isRequired };
