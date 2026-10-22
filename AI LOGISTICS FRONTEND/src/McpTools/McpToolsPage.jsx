import { useEffect, useMemo, useState } from "react";
import PropTypes from "prop-types";
import { Alert, Badge, Button, Card, EmptyState, ErrorState, Input, Modal, Select, Table } from "../design-system";
import { createMockToolRepository, lastUsedLabel, searchText, toolStatuses } from "./mockTools";
import {McpToolsApiError} from "../services/mcpToolsService";
import "./mcp-tools.css";
const statuses={...toolStatuses,published:"Publié"};

function Status({ status }) {
  return <Badge variant={status === "available" ? "success" : status === "error" ? "error" : status === "published" ? "info" : "warning"}>{statuses[status]}</Badge>;
}
Status.propTypes = { status: PropTypes.oneOf(Object.keys(statuses)).isRequired };

export default function McpToolsPage({ repository }) {
  const source = useMemo(() => repository || createMockToolRepository(), [repository]);
  const remote=Boolean(source.remote);
  const [loadError,setLoadError]=useState(null);
  const [tools, setTools] = useState([]);
  const [phase, setPhase] = useState("loading");
  const [revision, setRevision] = useState(0);
  const [query, setQuery] = useState("");
  const [category, setCategory] = useState("");
  const [status, setStatus] = useState("");
  const [selected, setSelected] = useState(null);
  useEffect(() => {
    const controller = new AbortController();
    setPhase("loading"); setLoadError(null); setSelected(null);
    Promise.resolve().then(() => source.list({ signal: controller.signal })).then((rows) => {
      if (!controller.signal.aborted) { setTools(rows); setPhase("ready"); }
    }).catch((error) => { if (!controller.signal.aborted) {setPhase("error");setLoadError(error instanceof McpToolsApiError?error.message:null);} });
    return () => controller.abort();
  }, [source, revision]);
  const reset = () => { setQuery(""); setCategory(""); setStatus(""); };
  const rows = tools.filter((tool) => searchText(`${tool.name} ${tool.description}`).includes(searchText(query))
    && (!category || tool.category === category) && (!status || tool.status === status));
  const columns = [
    { key: "name", header: "Tool" },
    { key: "description", header: "Description", render: (value) => <span className="mcp-description">{value}</span> },
    { key: "category", header: "Catégorie" },
    { key: "status", header: "Statut", render: (value) => <Status status={value} /> },
    { key: "lastUsed", header: "Dernière utilisation", render: value=>remote?"Non fournie":lastUsedLabel(value) },
    { key: "actions", header: "Détails", render: (_, tool) => <Button aria-label={`Voir ${tool.name}`} onClick={() => setSelected(tool)}>Voir les détails</Button> },
  ];
  return <section className="mcp-page" aria-label="MCP Tools">
    <Card className="mcp-header"><div><h1>MCP Tools</h1><p>{remote?"Catalogue publié par le serveur MCP, en consultation uniquement.":"Catalogue et derniers appels fictifs, en consultation uniquement."}</p></div><Badge>{remote?"API backend":"Mode démo"}</Badge>
      <Button className="mcp-refresh" disabled={phase === "loading"} onClick={() => setRevision((value) => value + 1)}>Actualiser</Button>
    </Card>
    {remote && <Alert title="Catalogue en consultation">« Publié » indique la présence du tool dans le catalogue, sans garantir l’accès aux données métier. Le backend ne fournit pas l’historique des appels, leurs résultats ni leur dernière utilisation.</Alert>}
    <Card className="mcp-toolbar">
      <Input label="Rechercher un tool" placeholder="Nom ou description…" fieldClassName="mcp-search" value={query} onChange={(event) => setQuery(event.target.value)} />
      <Select label="Catégorie" fieldClassName="mcp-filter" value={category} onChange={(event) => setCategory(event.target.value)}><option value="">Toutes les catégories</option>
        {[...new Set(tools.map((tool) => tool.category))].map((value) => <option key={value}>{value}</option>)}
      </Select>
      <Select label="Statut" fieldClassName="mcp-filter" value={status} onChange={(event) => setStatus(event.target.value)}><option value="">Tous les statuts</option>
        {Object.entries(remote?{published:statuses.published}:toolStatuses).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
      </Select><Button onClick={reset}>Réinitialiser</Button>
    </Card>
    <Card className="mcp-catalogue">{phase === "error" ? <ErrorState message={loadError||"Impossible de charger les tools. Réessayez."} onRetry={() => setRevision((value) => value + 1)} />
      : <Table caption="Catalogue des MCP Tools" columns={columns} rows={rows} rowKey="name" loading={phase === "loading"} className="mcp-table"
        emptyState={tools.length ? <EmptyState title="Aucun résultat" description="Modifiez la recherche ou les filtres." action={<Button onClick={reset}>Réinitialiser les filtres</Button>} />
          : <EmptyState title="Aucun tool" description={remote?"Le serveur MCP n’a publié aucun tool.":"Le catalogue de démonstration est vide."} />} />}
    </Card>
    {phase === "ready" && <p aria-live="polite">{rows.length} {rows.length === 1 ? "tool affiché" : "tools affichés"}</p>}
    <Modal open={Boolean(selected)} title={selected ? `Détails — ${selected.name}` : "Détails du tool"} onClose={() => setSelected(null)} actions={<Button onClick={() => setSelected(null)}>Fermer</Button>}>
      {selected && <div className="mcp-details">
        <p>{selected.description}</p><p>Catégorie : {selected.category}</p><div>Statut : <Status status={selected.status} /></div>
        <p>Dernière utilisation : {remote?"Non fournie par le backend":lastUsedLabel(selected.lastUsed)}</p>
        <Table className="mcp-table" caption="Paramètres attendus" rowKey="name" rows={selected.parameters} columns={[
          { key: "name", header: "Nom" }, { key: "type", header: "Type" },
          { key: "required", header: "Obligatoire", render: (value) => value ? "Oui" : "Non" }, { key: "description", header: "Description" },
        ]} />
        {remote && <section aria-label="Schéma d’entrée"><h3>Schéma d’entrée</h3><pre className="mcp-json">{JSON.stringify(selected.inputSchema,null,2)}</pre></section>}
        <section aria-label="Paramètres du dernier appel"><h3>Paramètres du dernier appel</h3>{selected.lastInput ? <pre className="mcp-json">{JSON.stringify(selected.lastInput, null, 2)}</pre> : <EmptyState title={remote?"Historique non exposé par le backend":"Aucun appel enregistré"} />}</section>
        <section aria-label="Résultat"><h3>Résultat</h3>{selected.result !== null ? <pre className="mcp-json">{JSON.stringify(selected.result, null, 2)}</pre> : <EmptyState title={remote?"Résultats non exposés par le backend":"Aucun résultat disponible"} />}</section>
        <section aria-label="Erreur"><h3>Erreur</h3>{selected.error ? <ErrorState title={selected.error.code} message={selected.error.message} /> : <p>{remote?"Historique des erreurs non exposé par le backend.":"Aucune erreur enregistrée."}</p>}</section>
        <p>{remote?"Schémas reçus du serveur MCP. Aucun appel métier ni SQL n’est exécuté depuis cette interface.":"Exemples fictifs. Les paramètres sont consultables et aucun tool n’est exécuté depuis cette interface."}</p>
      </div>}
    </Modal>
  </section>;
}
McpToolsPage.propTypes = { repository: PropTypes.shape({ remote:PropTypes.bool, list: PropTypes.func.isRequired }) };
