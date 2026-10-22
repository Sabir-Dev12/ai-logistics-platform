import { useCallback, useState } from "react";
import PropTypes from "prop-types";
import { Alert, Button, ErrorState, Input, LoadingState, Select } from "../design-system";
import { bookingTypes } from "./bookingRules";
import BookingReferencePicker from "./BookingReferencePicker";
import { useBookingResource } from "./useBookingResource";
import VoyageCapacity from "./VoyageCapacity";

const headerFields = [["numBooking", "Numéro de réservation"], ["dateValidite", "Date de validité", "date"], ["intituleClient", "Client / payeur"],
  ["intituleExpediteur", "Expéditeur"], ["paysExpediteur", "Pays expéditeur"], ["intituleDestinataire", "Destinataire"], ["paysDestinataire", "Pays destinataire"],
  ["incoterm", "Incoterm"], ["mode", "Mode"], ["descriptionMarchandise", "Description de la marchandise"], ["portDepart", "Port de départ"],
  ["dateDepartPrevue", "Départ prévu", "date"], ["dateDisponibiliteMarchandise", "Disponibilité marchandise", "date"], ["commentaire", "Commentaire"], ["natureOperation", "Nature de l’opération"]];
const specialFields = {
  duplicate: [["numBooking", "Nouveau numéro (facultatif)"], ["dateValidite", "Nouvelle date de validité", "date"]],
  cancel: [["motif", "Motif d’annulation (facultatif)"]],
  unload: [["dateDepotage", "Date de dépotage", "date"], ["colisDepote", "Colis dépotés", "number"], ["magasin", "Magasin"], ["codeBureauDouane", "Bureau de douane"], ["constat", "Constat"]],
  linkVoyage: [], linkDossier: [],
};
export default function BookingForm({ kind, booking, data, onSubmit, onCancel }) {
  const remote = data.serverPaging;
  const unsupported=["portDepart","dateDepartPrevue","dateDisponibiliteMarchandise"];
  const editFields=["numBooking","dateValidite","incoterm","mode","commentaire","natureOperation"];
  const fields = specialFields[kind] || (remote ? headerFields.filter(([key])=>!unsupported.includes(key) && (kind!=="edit" || editFields.includes(key))).map(([key,label,type])=>[key,key==="intituleClient"?"Nom du payeur":label,type]) : headerFields);
  const loadTypes=useCallback(signal=>remote && kind==="create" ? data.repository.types({signal}) : Promise.resolve(bookingTypes),[remote,kind,data.repository]);
  const types=useBookingResource(loadTypes);
  const typeOptions=remote && kind==="create" ? types.value || [] : bookingTypes;
  const [values, setValues] = useState(() => ({ ...Object.fromEntries(fields.map(([key,,type]) => [key, (kind === "edit" ? (type === "date" ? booking?.[key]?.slice(0,10) : booking?.[key]) : "") || ""])),
    typeBooking: booking?.typeBooking || "RI", typeChargement: kind === "edit" ? booking.typeChargement || "" : "",
    ...(kind === "unload" ? { dateDepotage: new Date().toISOString().slice(0, 10) } : {}),
    ...(kind === "duplicate" || kind === "create" ? { dateValidite: new Date().toISOString().slice(0, 10) } : {}), keyVoyage: "", keyDossier: "", keyExpediteur:"",keyDestinataire:"",keyPayeur:"" }));
  const [errors, setErrors] = useState({});
  const set = (key, value) => { setValues((current) => ({ ...current, [key]: value })); setErrors({}); };
  const submit = async (event) => {
    event.preventDefault(); if (data.busy || data.uncertain) return;
    const issues = {};
    if (kind === "create" && !values.numBooking.trim()) issues.numBooking = "Le numéro est obligatoire.";
    if (kind === "linkVoyage" && !values.keyVoyage) issues.selection = "Sélectionnez un voyage.";
    if (kind === "linkDossier" && !values.keyDossier) issues.selection = "Sélectionnez un dossier.";
    if (kind === "unload" && !values.dateDepotage) issues.dateDepotage = "La date est obligatoire.";
    if (kind === "unload" && values.colisDepote !== "" && (!Number.isFinite(Number(values.colisDepote)) || Number(values.colisDepote) < 0)) issues.colisDepote = "Utilisez un nombre positif ou nul.";
    if(remote && kind==="create" && (!values.typeBooking || !typeOptions.some(item=>item.value===values.typeBooking))) issues.typeBooking="Sélectionnez un type disponible.";
    setErrors(issues); if (Object.keys(issues).length) return;
    const payload = kind === "linkVoyage" ? values.keyVoyage : kind === "linkDossier" ? values.keyDossier
      : Object.fromEntries(Object.entries(values).filter(([key]) => fields.some(([name]) => name === key) || (!specialFields[kind] && ["typeBooking", "typeChargement"].includes(key))));
    if(remote && kind==="create") Object.assign(payload,{keyExpediteur:values.keyExpediteur,keyDestinataire:values.keyDestinataire,keyPayeur:values.keyPayeur,intitulePayeur:values.intituleClient});
    if (kind === "unload") payload.colisDepote = values.colisDepote === "" ? null : Number(values.colisDepote);
    await onSubmit(payload);
  };
  return <form className="bookings-form" onSubmit={submit} noValidate><fieldset disabled={data.busy}><legend>{kind === "create" ? "Nouvelle réservation" : "Informations"}</legend>
    {!specialFields[kind] && <div className="bookings-fields"><Select label="Type de réservation" value={values.typeBooking} error={errors.typeBooking} disabled={kind === "edit" || (remote && types.phase!=="ready")} onChange={(event) => set("typeBooking", event.target.value)}>
      {remote && kind==="create" && <option value="">Sélectionner un type</option>}{typeOptions.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}</Select>
      <Select label="Chargement" disabled={remote && kind==="create"} value={values.typeChargement} onChange={(event) => set("typeChargement", event.target.value)}><option value="">À définir</option><option value="Complet">Complet</option><option value="Groupage">Groupage</option></Select></div>}
    {remote && kind==="create" && <>{types.phase==="loading" && <LoadingState label="Chargement des types…" />}{types.phase==="error" && <ErrorState message={types.error} onRetry={types.retry} />}<Alert title="Création de la réservation">Choisissez le chargement après avoir ajouté les équipements ou colisages. Le client est déterminé par le type et les parties renseignées.</Alert><div className="bookings-fields">{[["keyExpediteur","Référence expéditeur"],["keyDestinataire","Référence destinataire"],["keyPayeur","Référence payeur"]].map(([key,label])=><Input key={key} label={label} value={values[key]} onChange={event=>set(key,event.target.value)} />)}</div></>}
    <div className="bookings-fields">{fields.map(([key, label, type = "text"]) => <Input key={key} label={label} value={values[key]} type={type}
      readOnly={kind === "edit" && key === "numBooking"} min={type === "number" ? 0 : undefined} step={type === "number" ? "any" : undefined}
      error={errors[key]} onChange={(event) => set(key, event.target.value)} />)}</div>
    {kind === "duplicate" && <p>Sans numéro saisi, le backend générera un numéro si la numérotation automatique est activée. Les liens, volumes prévisionnels et le dépotage seront réinitialisés.</p>}
    {kind === "linkVoyage" && <VoyageCapacity voyages={data.voyages} bookings={data.bookings} repository={remote ? data.repository : undefined} selectedId={values.keyVoyage} onSelect={(id) => set("keyVoyage", id)} />}
    {kind === "linkDossier" && remote && <BookingReferencePicker repository={data.repository} kind="dossiers" value={values.keyDossier} onChange={id=>set("keyDossier",id)} />}
    {kind === "linkDossier" && !remote && <Select label="Dossier" value={values.keyDossier} onChange={(event) => set("keyDossier", event.target.value)}><option value="">Sélectionner un dossier</option>
      {data.dossiers.map((file) => <option key={file.keyDossier} value={file.keyDossier}>{file.label}{file.keyBooking && file.keyBooking !== booking?.keyBooking ? " (déjà rattaché)" : ""}</option>)}</Select>}
    {errors.selection && <ErrorState message={errors.selection} />}
  </fieldset>{data.error && <ErrorState message={data.error} />}<div className="bookings-actions"><Button disabled={data.busy} onClick={onCancel}>Annuler</Button><Button type="submit" variant="primary" disabled={data.uncertain || (remote && kind==="create" && types.phase!=="ready")} loading={data.busy}>Enregistrer</Button></div></form>;
}
BookingForm.propTypes = { kind: PropTypes.string.isRequired, booking: PropTypes.object, data: PropTypes.object.isRequired, onSubmit: PropTypes.func.isRequired, onCancel: PropTypes.func.isRequired };
