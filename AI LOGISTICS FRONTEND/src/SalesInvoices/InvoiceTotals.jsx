import PropTypes from "prop-types";
import { Alert, Card, Input } from "../design-system";
import { money } from "./invoiceRules";
export default function InvoiceTotals({ invoice, totals, preview = false }) {
  return <Card className="bookings-line"><h3>{preview ? "Aperçu des montants" : "Montants de la facture"}</h3>{invoice.needsRecalculation && <Alert variant="warning" title="Recalcul nécessaire">Ces totaux sont antérieurs à l’exonération. Recalculez les montants pour les mettre à jour.</Alert>}
    <div className="bookings-fields">{[["Produits HT (MAD)", totals.produit], ["Débours (MAD)", totals.debours], ["Assignés (MAD)", totals.assigne], ["Taxe régionale (MAD)", totals.regional], ["Total HT (MAD)", totals.ht], ["TVA (MAD)", totals.vat], ["TTC (MAD)", totals.ttcMad], [`TTC (${invoice.currency})`, totals.ttc], [`Net à payer (${invoice.currency})`, totals.net]].map(([label, value], index) => <Input key={`${index}:${label}`} label={index === 6 && invoice.currency === "MAD" ? "TTC de référence (MAD)" : label} readOnly value={invoice.remote&&value==null?"Calculé par le serveur":money(value)} />)}</div>
    <p>{invoice.remote?(preview?"Les montants définitifs seront fournis après création par le backend.":"Montants retournés par le backend. Le récapitulatif TVA est celui enregistré sur la facture."):"Montants fictifs. Les charges restent hors des quatre rubriques du total HT ; leur TVA participe au total TVA."}</p>
  </Card>;
}
InvoiceTotals.propTypes = { invoice: PropTypes.object.isRequired, totals: PropTypes.object.isRequired, preview: PropTypes.bool };

