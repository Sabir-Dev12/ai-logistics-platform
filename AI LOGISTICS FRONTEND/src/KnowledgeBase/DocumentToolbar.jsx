import PropTypes from "prop-types";
import { Button, Card, Input, Select } from "../design-system";
import { documentStatuses, documentTypes } from "./documentTypes";

export default function DocumentToolbar({ query, type, status, onQuery, onType, onStatus, onReset, disabled = false }) {
  return <Card className="kb-toolbar">
    <Input disabled={disabled} label="Rechercher un document" placeholder="Nom du document…" value={query} onChange={(event) => onQuery(event.target.value)} fieldClassName="kb-search" />
    <Select disabled={disabled} label="Type" value={type} onChange={(event) => onType(event.target.value)} fieldClassName="kb-filter">
      <option value="">Tous les types</option>{documentTypes.map((value) => <option key={value}>{value}</option>)}
    </Select>
    <Select disabled={disabled} label="Statut" value={status} onChange={(event) => onStatus(event.target.value)} fieldClassName="kb-filter">
      <option value="">Tous les statuts</option>{Object.entries(documentStatuses).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
    </Select>
    <Button disabled={disabled} onClick={onReset}>Réinitialiser</Button>
  </Card>;
}
DocumentToolbar.propTypes = { query: PropTypes.string.isRequired, type: PropTypes.string.isRequired, status: PropTypes.string.isRequired,
  disabled:PropTypes.bool, onQuery: PropTypes.func.isRequired, onType: PropTypes.func.isRequired, onStatus: PropTypes.func.isRequired, onReset: PropTypes.func.isRequired };
