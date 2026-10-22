import { useState } from "react";
import PropTypes from "prop-types";
import { Alert, Badge, Button, Card, EmptyState, ErrorState, LoadingState, Pagination, Select, Table } from "../design-system";
import DashboardCharts from "../components/Layout";
import { activityStatuses, entityTypes, periods, selectDashboard } from "./dashboardData";
import { useDashboard } from "./useDashboard";
import "./dashboard.css";

const seriesNames = { uv: "Expéditions", pv: "Réservations", amt: "Autres entités" };
const typeLabel = (value) => entityTypes.find((type) => type.value === value)?.label || value;
const statusBadge = (value, row) => <Badge variant={(activityStatuses[value] || activityStatuses.unknown).variant}>{row.statusLabel || (activityStatuses[value] || activityStatuses.unknown).label}</Badge>;
const dateLabel = (value) => <time dateTime={value}>{new Intl.DateTimeFormat("fr-FR", { dateStyle: "medium", timeStyle: "short" }).format(new Date(value))}</time>;
const activityColumns = [
  { key: "date", header: "Date", render: dateLabel }, { key: "entityType", header: "Type d’entité", render: typeLabel },
  { key: "entityId", header: "Référence" }, { key: "description", header: "Activité" }, { key: "status", header: "Statut", render: statusBadge },
];
const summaryColumns = [{ key: "label", header: "Type d’entité" }, { key: "total", header: "Entités" },
  ...Object.entries(activityStatuses).map(([key, status]) => ({ key, header: status.label, render: (value, row) => key !== "unknown" && row.unknown > 0 ? "—" : value }))];

export default function DashboardPage({ repository }) {
  const [period, setPeriod] = useState("30"), [entityType, setEntityType] = useState(""), [page, setPage] = useState(1);
  const data = useDashboard(repository, { period, entityType });
  const filtered = selectDashboard(data.activities, { period, entityType, now: data.asOf });
  const totalPages = Math.max(1, Math.ceil(filtered.activities.length / 5)), currentPage = Math.min(page, totalPages);
  const start = (currentPage - 1) * 5;
  const reset = () => { setPeriod("30"); setEntityType(""); setPage(1); };
  const change = (setter) => (event) => { setter(event.target.value); setPage(1); };
  const metrics = [{ label: "Activités", value: filtered.activities.length }, { label: "Entités concernées", value: filtered.entities },
    { label: "En cours", value: filtered.active ?? "—" }, { label: "À vérifier", value: filtered.attention ?? "—" }];
  const empty = data.activities.length ? <EmptyState title="Aucun résultat" description="Aucune activité ne correspond aux filtres."
    action={<Button onClick={reset}>Réinitialiser les filtres</Button>} />
    : <EmptyState title="Aucune activité" description="Les prochaines activités apparaîtront ici." />;
  return <section className="dashboard-page" aria-label="Dashboard">
    <Card className="dashboard-header"><div><h1>Dashboard</h1><p>Activité récente et synthèses des événements métier chargés.</p></div><Badge>Activité métier</Badge>
      <Button className="dashboard-refresh" loading={data.phase === "loading"} onClick={data.reload}>Actualiser</Button>
    </Card>
    <Card className="dashboard-toolbar" aria-label="Filtres de l’activité">
      <Select label="Période" value={period} onChange={change(setPeriod)} fieldClassName="dashboard-filter">
        {periods.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
      </Select>
      <Select label="Type d’entité" value={entityType} onChange={change(setEntityType)} fieldClassName="dashboard-filter">
        <option value="">Toutes les entités</option>{entityTypes.map((type) => <option key={type.value} value={type.value}>{type.label}</option>)}
      </Select>
      <Button onClick={reset}>Réinitialiser les filtres</Button>
    </Card>
    {data.phase === "loading" ? <Card><LoadingState label="Chargement du dashboard…" /></Card>
      : data.phase === "error" ? <ErrorState message={data.error?.message || "Impossible de charger le dashboard. Réessayez."} onRetry={data.reload} />
        : !filtered.activities.length ? <Card>{empty}</Card>
          : <>
            <div className="dashboard-metrics" aria-label="Synthèses de l’activité filtrée">
              {metrics.map((metric) => <Card key={metric.label} className="dashboard-metric"><h2>{metric.label}</h2><p>{metric.value}</p></Card>)}
            </div>
            <Card className="dashboard-panel"><h2>Statuts par type d’entité</h2>
              <p>Dernier événement chargé de chaque entité dans l’activité filtrée.</p>
              <Table columns={summaryColumns} rows={filtered.summaries} rowKey="value" caption="Synthèse des entités concernées" className="dashboard-table" />
            </Card>
            <section aria-label="Graphiques de l’activité" className="dashboard-charts"><h2>Activité par jour</h2>
              <p>Barres : expéditions et réservations. Aires : expéditions, réservations et autres entités. Les valeurs représentent des activités.</p>
              <DashboardCharts data={filtered.chartData} seriesNames={seriesNames} autoSize />
            </section>
            <Card className="dashboard-panel"><h2>Activité récente</h2>
              <Table columns={activityColumns} rows={filtered.activities.slice(start, start + 5)} caption="Activités de la période sélectionnée" className="dashboard-table" />
            </Card>
            <div className="dashboard-footer"><p aria-live="polite">{start + 1}–{Math.min(start + 5, filtered.activities.length)} sur {filtered.activities.length} activités</p>
              <Pagination page={currentPage} totalPages={totalPages} onPageChange={setPage} />
            </div>
          </>}
    {data.phase === "ready" && data.realStatuses && <Alert title="Périmètre des données">Les synthèses portent sur les événements chargés, au maximum {data.limit}. Les libellés de statut sont fournis par le backend ; leur classement métier n’est pas disponible. « — » indique une valeur non calculable.{data.limited && <p>La limite est atteinte : réduisez la période ou sélectionnez un type d’entité. Ce flux ne fournit pas de pagination serveur ni de total global.</p>}</Alert>}
    <Alert title="Indicateurs financiers indisponibles">Les montants, le chiffre d’affaires et les soldes nécessitent des calculs ou des endpoints supplémentaires.</Alert>
  </section>;
}
DashboardPage.propTypes = { repository: PropTypes.shape({ list: PropTypes.func.isRequired }).isRequired };
