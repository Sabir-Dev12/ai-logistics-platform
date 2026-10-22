import { useState } from "react";
import PropTypes from "prop-types";
import { Badge, Button, Card, EmptyState, ErrorState, LoadingState, Modal, Select, Table } from "../design-system";
import MessageList from "../Chat/MessageList";
import MessageInput from "../Chat/MessageInput";
import StreamingIndicator from "../Chat/StreamingIndicator";
import { durationLabel, executionLabels, mockAgentRuns } from "./mockAgentRuns";
import "../Chat/chat.css";
import "./agent.css";

const connectionLabels={connecting:"Connexion…",connected:"Connecté",reconnecting:"Reconnexion…",disconnected:"Déconnecté",failed:"Connexion indisponible",unauthorized:"Session requise"};
const labels={...executionLabels,cancelled:"Arrêté"};
const statusBadge = (status) => <Badge variant={status === "complete" ? "success" : status === "error" ? "error" : status === "running" ? "warning" : "info"}>{labels[status]}</Badge>;

export default function AgentPage({session}={}) {
  const remote=Boolean(session);
  const [conversation,setConversation]=useState(0);
  const [runId, setRunId] = useState(mockAgentRuns[0].id);
  const [detail, setDetail] = useState(null);
  const run = session?.run || mockAgentRuns.find((item) => item.id === runId);
  const reset=()=>{session.reset();setConversation(value=>value+1);setDetail(null);};
  const sourceColumns = [
    { key: "document", header: "Document" }, { key: "passage", header: "Passage utilisé" },
    { key: "actions", header: "Source", render: (_, source) => <Button aria-label={`Lire ${source.document}`} onClick={() => setDetail({ kind: "source", value: source })}>Lire le passage</Button> },
  ];
  const toolColumns = [
    { key: "name", header: "Tool" }, { key: "status", header: "Statut", render: statusBadge },
    { key: "durationMs", header: "Durée", render: durationLabel },
    { key: "actions", header: "Appel", render: (_, tool) => <Button aria-label={`Détails ${tool.name}`} onClick={() => setDetail({ kind: "tool", value: tool })}>Voir les détails</Button> },
  ];
  return <section className="agent-page" aria-label="AI Agent">
    <Card className="agent-header"><div><h1>AI Agent</h1><p>{remote?"Conversation avec l’agent logistique du backend.":"Conversation, sources et parcours d’exécution fictifs."}</p></div><Badge>{remote?connectionLabels[session.connectionStatus]:"Mode démo"}</Badge>
      {remote?<Button className="agent-scenario" onClick={reset}>Nouvelle conversation</Button>:<Select label="Scénario de démonstration" fieldClassName="agent-scenario" value={runId} onChange={(event) => { setRunId(event.target.value); setDetail(null); }}>
        {mockAgentRuns.map((item) => <option key={item.id} value={item.id}>{item.label}</option>)}
      </Select>}
    </Card>
    <Card className="agent-summary" aria-label="Statut de l’agent"><div>Statut de l’agent : {statusBadge(run.status)}</div>
      <p>{remote?"Durée observée dans le navigateur":"Temps de réponse"} : {run.durationMs === null ? remote?(session.busy?"Réponse en cours":"—"):"En attente de la fin de l’exécution" : durationLabel(run.durationMs)}</p>
      <p>{remote?"Le statut décrit le flux de réponse. Les éventuelles erreurs métier restent dans le texte renvoyé par l’agent.":"Temps et résultats simulés."}</p>
    </Card>
    <div className="agent-workspace">
      <Card className="agent-conversation"><h2>Conversation</h2><MessageList key={remote?conversation:run.id} messages={run.messages} busy={run.status === "running"} />
        {remote?<StreamingIndicator phase={session.phase}/>:run.status === "running" && <LoadingState label="Agent en cours — attente de GetShipment…" />}
      </Card>
      <Card className="agent-execution"><h2>Étapes d’exécution</h2><ol className="agent-steps" aria-label="Étapes d’exécution">
        {run.steps.map((step) => <li key={step.id} aria-current={step.status === "running" ? "step" : undefined}>
          <div>{step.label}</div><div className="agent-step-meta">{statusBadge(step.status)}{step.durationMs !== null && <span>{durationLabel(step.durationMs)}</span>}</div>
        </li>)}
      </ol>{remote&&<p>Seuls l’envoi et le flux sont observables ici. Le backend ne diffuse pas les étapes internes RAG, MCP ou LLM.</p>}</Card>
    </div>
    {(run.error||session?.connectionError) && <ErrorState title={run.error?.code||"Connexion à l’agent"} message={session?.connectionError?.message||run.error?.message} onRetry={remote&&session.canRetry?session.retry:undefined}/>}
    {remote&&<MessageInput key={conversation} busy={session.busy} canSend={session.canSend} onSend={session.send} onStop={session.stop}/>}
    <Card className="agent-data"><Table caption="Sources RAG utilisées" columns={sourceColumns} rows={run.sources} emptyState={<EmptyState title={remote?"Sources structurées non exposées":"Aucune source utilisée"} description={remote?"Les passages et références RAG éventuels figurent dans la réponse de l’agent.":undefined}/>} className="agent-table" /></Card>
    <Card className="agent-data"><Table caption="MCP tools utilisés" columns={toolColumns} rows={run.tools} rowKey="name" emptyState={<EmptyState title={remote?"Traces MCP structurées non exposées":"Aucun tool utilisé"} description={remote?"Les données ou erreurs MCP éventuelles figurent dans la réponse de l’agent.":undefined}/>} className="agent-table" /></Card>
    <p>{remote?"Les questions sont traitées par ChatHub → ChatService → AgentService. Chaque question est indépendante ; indiquez les références métier nécessaires dans votre message.":"Les scénarios sont statiques et consultables uniquement. Aucun appel RAG, MCP ou LLM n’est effectué."}</p>
    <Modal open={Boolean(detail)} title={detail?.kind === "source" ? "Source RAG — passage utilisé" : "Appel MCP — détails fictifs"} onClose={() => setDetail(null)} actions={<Button onClick={() => setDetail(null)}>Fermer</Button>}>
      {detail?.kind === "source" && <div className="agent-details"><h3>{detail.value.document}</h3><p>{detail.value.passage}</p><p>{detail.value.excerpt}</p><Badge>Source fictive</Badge></div>}
      {detail?.kind === "tool" && <div className="agent-details"><h3>{detail.value.name}</h3><div>{statusBadge(detail.value.status)}</div>
        <p>Durée : {durationLabel(detail.value.durationMs)}</p>
        <section aria-label="Paramètres"><h3>Paramètres</h3><pre>{JSON.stringify(detail.value.input, null, 2)}</pre></section>
        <section aria-label="Résultat"><h3>Résultat</h3>{detail.value.result !== null ? <pre>{JSON.stringify(detail.value.result, null, 2)}</pre>
          : <EmptyState title={detail.value.status === "running" ? "Résultat en attente" : "Aucun résultat disponible"} />}</section>
        {detail.value.error ? <ErrorState title={detail.value.error.code} message={detail.value.error.message} /> : <p>Aucune erreur enregistrée.</p>}
      </div>}
    </Modal>
  </section>;
}

AgentPage.propTypes={session:PropTypes.shape({run:PropTypes.object.isRequired,phase:PropTypes.oneOf(["idle","waiting","streaming"]).isRequired,busy:PropTypes.bool.isRequired,canSend:PropTypes.bool.isRequired,canRetry:PropTypes.bool.isRequired,connectionStatus:PropTypes.string.isRequired,connectionError:PropTypes.object,send:PropTypes.func.isRequired,stop:PropTypes.func.isRequired,reset:PropTypes.func.isRequired,retry:PropTypes.func.isRequired})};
