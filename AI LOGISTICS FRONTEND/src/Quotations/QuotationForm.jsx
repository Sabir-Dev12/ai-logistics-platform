import { useCallback, useEffect, useState } from "react";
import PropTypes from "prop-types";
import { Alert, Button, ErrorState, Input, Select } from "../design-system";
import { products as demoProducts } from "./quotationRules";
import InvoiceClientPicker from "../SalesInvoices/InvoiceClientPicker";
import { useBookingResource } from "../Bookings/useBookingResource";
export default function QuotationForm({ row, clients, source, kind, busy, error, onSubmit, onCancel }) {
  const loader = useCallback(signal => source && kind === "create" ? source.products({signal}) : Promise.resolve(row ? [{id:row.produit,label:row.produit}] : demoProducts), [source,kind,row]);
  const catalog = useBookingResource(loader);
  const products = source ? catalog.value || [] : demoProducts;
  const header = kind === "create" || kind === "edit";
  const [value, setValue] = useState(() => header ? { numCotation: row?.numCotation || "", produit: row?.produit || products[0]?.id || "", keyClient: row?.keyClient || "", typeTransport: row?.typeTransport || "Routier", natureMarchandise: row?.natureMarchandise || "", dateValidite: row?.dateValidite || "", contact: row?.contact || "", commercial: row?.commercial || "", commentaire: row?.commentaire || "" } : kind === "duplicate" ? { numCotation: `${row.numCotation}-COPIE` } : kind === "close" ? { gagnee: true, motifPerdue: "", concurrent: "", commentaire: "" } : { motif: "" });
  useEffect(() => { if(source && kind === "create" && products.length) setValue(current => current.produit ? current : {...current,produit:products[0].id}); }, [source,kind,products]);
  const [validation, setValidation] = useState("");
  const change = (key) => (event) => { setValue((current) => ({ ...current, [key]: event.target.value })); setValidation(""); };
  const fields = header ? [["numCotation", "Numéro de cotation"], ["typeTransport", "Type de transport"], ["natureMarchandise", "Nature de la marchandise"], ["dateValidite", "Validité du devis", "date"], ["contact", "Contact"], ["commercial", "Commercial"], ["commentaire", "Commentaire"]] : kind === "duplicate" ? [["numCotation", "Numéro de la copie"]] : kind === "close" ? [["commentaire", "Commentaire de clôture"], ...(!value.gagnee ? [["motifPerdue", "Motif de perte"], ["concurrent", "Concurrent"]] : [])] : [["motif", "Motif d’annulation"]];
  const submit = (event) => { event.preventDefault(); if (busy) return; if (header && (!value.numCotation.trim() || !value.keyClient)) { setValidation("Le numéro de cotation et le client sont obligatoires."); return; } void onSubmit(value); };
  return <form className="bookings-form" onSubmit={submit} noValidate>{kind === "duplicate" && <Alert title="Duplication">L’en-tête, les colisages, les prix, les taux et les documents sont copiés. Les achats et le regroupement ne sont pas repris.</Alert>}
    <fieldset disabled={busy}><legend>{header ? "Informations du devis" : "Informations de l’opération"}</legend><div className="bookings-fields">
      {header && <>{source ? <InvoiceClientPicker source={source} value={value.keyClient} onChange={keyClient => setValue(current => ({...current,keyClient}))} label="Client du devis" allowAll={false} disabled={busy || kind === "edit"} /> : <Select label="Client du devis" value={value.keyClient} onChange={change("keyClient")} disabled={kind === "edit"}><option value="">Sélectionnez un client</option>{clients.map((client) => <option key={client.id} value={client.id}>{client.label}</option>)}</Select>}<Select label="Produit du devis" value={value.produit} onChange={change("produit")} disabled={kind === "edit"}>{products.map((item) => <option key={item.id} value={item.id}>{item.label}</option>)}</Select></>}
      {kind === "close" && <Select label="Résultat de la cotation" value={String(value.gagnee)} onChange={(event) => setValue((current) => ({ ...current, gagnee: event.target.value === "true" }))}><option value="true">Gagnée</option><option value="false">Perdue</option></Select>}
      {fields.map(([key, label, type = "text"]) => <Input key={key} label={label} disabled={Boolean(source && kind === "edit" && ["contact","commercial"].includes(key))} type={type} value={value[key]} onChange={change(key)} />)}
    </div></fieldset>{source && catalog.phase === "error" && <ErrorState message={catalog.error} onRetry={catalog.retry} />}{(validation || error) && <ErrorState message={validation || error} />}<div className="bookings-actions"><Button disabled={busy} onClick={onCancel}>Annuler</Button><Button type="submit" variant="primary" loading={busy} disabled={busy || (source && kind === "create" && (catalog.phase !== "ready" || !products.length))}>{header ? "Enregistrer le devis" : "Confirmer"}</Button></div>
  </form>;
}
QuotationForm.propTypes = { row: PropTypes.object, source: PropTypes.object, clients: PropTypes.array.isRequired, kind: PropTypes.string.isRequired, busy: PropTypes.bool, error: PropTypes.string, onSubmit: PropTypes.func.isRequired, onCancel: PropTypes.func.isRequired };
