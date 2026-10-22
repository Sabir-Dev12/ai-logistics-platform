import { useCallback } from "react";
import PropTypes from "prop-types";
import { Alert, Card, EmptyState, ErrorState, LoadingState, Select } from "../design-system";
import BookingReferencePicker from "./BookingReferencePicker";
import { useBookingResource } from "./useBookingResource";
import { voyageCapacity } from "./bookingRules";

export default function VoyageCapacity(props) {
 return props.repository ? <RemoteCapacity {...props} /> : <LocalCapacity {...props} />;
}
function RemoteCapacity({repository,selectedId,onSelect,readOnly=false}) {
 const loader=useCallback(signal=>selectedId?repository.capacity(selectedId,{signal}):Promise.resolve(null),[repository,selectedId]);
 const result=useBookingResource(loader);
 return <section className="bookings-capacity" aria-label="Capacité du voyage">
 {!readOnly&&<BookingReferencePicker repository={repository} kind="voyages" value={selectedId} onChange={onSelect} />}
 {!selectedId?<EmptyState title="Sélectionnez un voyage" />:result.phase==="loading"?<LoadingState label="Chargement de la capacité…" />:result.phase==="error"?<ErrorState message={result.error} onRetry={result.retry} />:result.value&&<CapacityMetrics capacity={result.value} />}
 </section>;
}
RemoteCapacity.propTypes={repository:PropTypes.object.isRequired,selectedId:PropTypes.string,onSelect:PropTypes.func,readOnly:PropTypes.bool};
function CapacityMetrics({capacity}) {
 return <><div className="bookings-capacity-grid">{[["Capacité (m³)",capacity.capaciteRemorque],["Volume réservé (m³)",capacity.volumeReserve],["Volume libre (m³)",capacity.libre],["Réservations affectées",capacity.nbBookings],["Occupation (%)",capacity.tauxOccupation]].map(([label,value])=><Card className="bookings-metric" key={label}><h3>{label}</h3><p>{new Intl.NumberFormat("fr-FR",{maximumFractionDigits:2}).format(value)}</p></Card>)}</div><p>Calcul fondé sur les volumes prévisionnels des réservations affectées.</p>{capacity.capaciteRemorque<=0&&<Alert title="Capacité non renseignée">Le voyage ne possède pas de limite de capacité renseignée.</Alert>}{capacity.capaciteRemorque>0&&capacity.libre<0&&<Alert variant="warning" title="Capacité dépassée">Le volume réservé dépasse la capacité du voyage.</Alert>}</>;
}
CapacityMetrics.propTypes={capacity:PropTypes.object.isRequired};
function LocalCapacity({ voyages, bookings, selectedId, onSelect, readOnly = false }) {
  const voyage = voyages.find((item) => item.keyVoyage === selectedId);
  const capacity = voyage ? voyageCapacity(voyage, bookings) : null;
  return <section className="bookings-capacity" aria-label="Capacité du voyage">
    {!readOnly && <Select label="Voyage" value={selectedId || ""} onChange={(event) => onSelect(event.target.value)}><option value="">Sélectionner un voyage</option>
      {voyages.map((item) => <option key={item.keyVoyage} value={item.keyVoyage}>{item.label}</option>)}
    </Select>}
    {!capacity ? <EmptyState title={voyages.length ? "Sélectionnez un voyage" : "Aucun voyage disponible"} />
      : <><p>{voyage.label}</p><div className="bookings-capacity-grid">{[
        ["Capacité (m³)", capacity.capaciteRemorque], ["Volume réservé (m³)", capacity.volumeReserve], ["Volume libre (m³)", capacity.libre],
        ["Réservations affectées", capacity.nbBookings], ["Occupation (%)", capacity.tauxOccupation],
      ].map(([label, value]) => <Card className="bookings-metric" key={label}><h3>{label}</h3><p>{new Intl.NumberFormat("fr-FR", { maximumFractionDigits: 2 }).format(value)}</p></Card>)}</div>
        <p>Calcul fondé sur les volumes prévisionnels des réservations affectées.</p>
        {capacity.capaciteRemorque <= 0 && <Alert title="Capacité non renseignée">Le voyage ne possède pas de limite de capacité renseignée.</Alert>}
        {capacity.capaciteRemorque > 0 && capacity.libre < 0 && <Alert variant="warning" title="Capacité dépassée">Le volume réservé dépasse la capacité du voyage.</Alert>}
      </>}
  </section>;
}
VoyageCapacity.propTypes = { voyages: PropTypes.array.isRequired, bookings: PropTypes.array.isRequired, selectedId: PropTypes.string, onSelect: PropTypes.func, readOnly: PropTypes.bool, repository:PropTypes.object };

LocalCapacity.propTypes = VoyageCapacity.propTypes;
