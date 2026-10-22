import PropTypes from "prop-types";
import { Alert, Card, Input } from "../design-system";
import { money } from "./quotationRules";
export default function QuotationTotals({ row, totals }) {
  return <Card className="bookings-line"><h3>Totaux du devis</h3>{!totals.complete && <Alert title="Totaux incomplets" variant="warning">{totals.missing.length ? `Taux manquant pour : ${totals.missing.join(", ")}. Renseignez les taux de change pour consulter les totaux.` : "Les totaux ne sont pas disponibles."}</Alert>}
    <div className="bookings-fields"><Input label="Total vente (MAD)" readOnly value={totals.complete ? money(totals.vente) : "Indisponible"} /><Input label="Total achats (MAD)" readOnly value={totals.complete ? money(totals.achat) : "Indisponible"} /><Input label="Marge (MAD)" readOnly value={totals.complete ? money(totals.marge) : "Indisponible"} /></div>
    <p>{row.prix.length} ligne(s) de prix · {row.achats.length} achat(s) · {row.colisages.length} colisage(s)</p><p>{row.remote ? "Totaux calculés par le serveur. Les montants détaillés non exposés sont indiqués comme non fournis." : "Montants fictifs convertis en MAD. Les pourcentages s’appliquent à la base des lignes au montant dans la même devise."}</p>
  </Card>;
}
QuotationTotals.propTypes = { row: PropTypes.object.isRequired, totals: PropTypes.object.isRequired };
