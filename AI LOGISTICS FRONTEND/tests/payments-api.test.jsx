import {render,screen,within,fireEvent,waitFor} from "@testing-library/react";
import {describe,it,expect,vi} from "vitest";
import PaymentsPage from "../src/Payments/PaymentsPage";
import {createPaymentsService} from "../src/services/paymentsService";
import {paymentDto,pageDto,referenceResponse} from "./fixtures/payments";
function setup({readError=false,writeStatus=200,refreshError=false,empty=false,noModes=false}={}){let failed=readError,written=false;const request=vi.fn(async(url,opt)=>{let body,status=200;
 if(opt.method!=="GET"){written=true;status=writeStatus;body=status>=200&&status<300?(url.endsWith("/allocations")?{allocated:40,montantMad:1000,ecart:0.5,ecartDecision:0,odKeyFacture:"OD1"}:paymentDto({modePaiement:noModes?null:"VIR"})):{detail:"SQL private"};}
 else if(url.startsWith("/api/v1/reference/"))body=noModes&&url.endsWith("/modes-paiement")?[]:referenceResponse(url);
 else if(url.includes("reglements?")){if(failed||written&&refreshError){failed=false;status=503;body={};}else{const q=new URL(url,"http://localhost").searchParams;body=pageDto(empty||q.get("search")==="absent"?[]:[paymentDto()],Number(q.get("page")),Number(q.get("pageSize")),empty||q.get("search")==="absent"?0:6);}}
 else if(url.endsWith("/absent")){status=404;body={};}else body=paymentDto();return new Response(JSON.stringify(body),{status});});render(<PaymentsPage repository={createPaymentsService({request})}/>);return {request};}
async function open(){fireEvent.click(await screen.findByRole("button",{name:"R1",exact:true}));await screen.findByRole("heading",{name:"R1",exact:true});}
const click=name=>fireEvent.click(within(screen.getByRole("dialog")).getByRole("button",{name,exact:true}));
const change=(label,value)=>fireEvent.change(within(screen.getByRole("dialog")).getByLabelText(label),{target:{value}});
describe("Connected payments UI",()=>{
 it("offers readable search and party fields, resets paging and clears both criteria",async()=>{
  const {request}=setup();await screen.findByRole("button",{name:"R1",exact:true});
  expect(screen.getByLabelText("Tiers",{exact:true}).placeholder).toBe("Nom ou référence du tiers");
  expect(screen.getByLabelText("Rechercher un règlement").placeholder).toBe("Référence, paiement ou tiers");
  expect(screen.queryByText("Clé exacte du tiers.")).toBeNull();
  expect(screen.queryByText("Recherche par clé exacte, puis application des filtres.")).toBeNull();
  fireEvent.click(screen.getByRole("button",{name:"Suivant",exact:true}));
  await waitFor(()=>expect(request.mock.calls.at(-1)[0]).toContain("page=2"));
  fireEvent.change(screen.getByLabelText("Tiers",{exact:true}),{target:{value:"Atlas"}});
  await waitFor(()=>expect(request.mock.calls.at(-1)[0]).toContain("tiers=Atlas"));
  fireEvent.change(screen.getByLabelText("Rechercher un règlement"),{target:{value:"absent"}});
  await screen.findByText("Aucun résultat");
  const params=new URL(request.mock.calls.at(-1)[0],"http://local").searchParams;
  expect(params.get("search")).toBe("absent");expect(params.get("tiers")).toBe("Atlas");expect(params.get("page")).toBe("1");
  expect(request.mock.calls.some(([url])=>url==="/api/v1/reglements/absent")).toBe(false);
  fireEvent.click(screen.getAllByRole("button",{name:"Réinitialiser les filtres",exact:true})[0]);
  await screen.findByRole("button",{name:"R1",exact:true});
  expect(screen.getByLabelText("Tiers",{exact:true}).value).toBe("");
 });
 it("creates with an empty modes reference list using the optional backend field",async()=>{
  const {request}=setup({noModes:true,writeStatus:201});
  await screen.findByRole("button",{name:"R1",exact:true});
  fireEvent.click(screen.getByRole("button",{name:"Créer un règlement",exact:true}));
  await screen.findByText("Aucun mode de paiement configuré. Ce champ est facultatif.");
  change("Tiers du règlement","C1");change("Montant du règlement","100");
  click("Enregistrer le règlement");
  await screen.findByText("Action enregistrée.");
  const writes=request.mock.calls.filter(([,opt])=>opt.method==="POST");
  expect(writes).toHaveLength(1);
  expect(JSON.parse(writes[0][1].body)).toMatchObject({modePaiement:null,devise:"MAD"});
  expect(screen.queryByText("Vérifiez la devise et le mode de paiement.")).toBeNull();
 });
 it("shows loading, server data and real pagination",async()=>{const {request}=setup();expect(screen.getByRole("status",{name:"Chargement des règlements…"})).toBeTruthy();await screen.findByRole("button",{name:"R1",exact:true});expect(screen.queryByText("Mode démo")).toBeNull();expect(screen.getByText("1–5 sur 6 règlements")).toBeTruthy();fireEvent.click(screen.getByRole("button",{name:"Suivant",exact:true}));await waitFor(()=>expect(request.mock.calls.some(([url])=>url.includes("page=2"))).toBe(true));});
 it("retries read failure and displays the empty state",async()=>{setup({readError:true,empty:true});await screen.findByText("Le service Règlements est indisponible. Réessayez.");fireEvent.click(screen.getByRole("button",{name:"Réessayer"}));await screen.findByText("Aucun règlement");});
 it("shows references and does not invent a non-advance available amount",async()=>{setup();await open();const modal=within(screen.getByRole("dialog"));expect(modal.getByLabelText("Référence du paiement").value).toBe("VIR-123");expect(modal.getByLabelText("Disponible (EUR)").value).toBe("Non fourni par l’API");expect(modal.getByLabelText("Tiers").value).toBe("C1");expect(modal.queryByLabelText("Référence société")).toBeNull();});
 it("loads payment modes and currencies from the API in the creation form",async()=>{setup();await screen.findByRole("button",{name:"R1",exact:true});fireEvent.click(screen.getByRole("button",{name:"Créer un règlement",exact:true}));await screen.findByRole("option",{name:"VIR"});expect(within(screen.getByRole("dialog")).getByRole("option",{name:"EUR"})).toBeTruthy();expect(within(screen.getByRole("dialog")).getByLabelText("Tiers du règlement").tagName).toBe("INPUT");});
 it("sends allocation via the service and displays the server OD decision",async()=>{const {request}=setup();await open();click("Affecter à une facture");change("Facture à affecter","F1");change("Montant à affecter","40");click("Enregistrer l’affectation");await screen.findByText(/Affectation confirmée.*OD1/);expect(request.mock.calls.filter(([,opt])=>opt.method==="POST")).toHaveLength(1);});
 it("blocks repeat writes when confirmation is lost",async()=>{const {request}=setup({writeStatus:503});await open();click("Déclarer un impayé");change("Motif d’impayé","Refus");click("Enregistrer l’impayé");await screen.findAllByText("La confirmation n’a pas été reçue. Vérifiez le règlement avant de recommencer.");expect(within(screen.getByRole("dialog")).getByRole("button",{name:"Enregistrer l’impayé",exact:true}).disabled).toBe(true);expect(request.mock.calls.filter(([,opt])=>opt.method==="POST")).toHaveLength(1);});
 it("keeps confirmed write success separate from refresh failure",async()=>{setup({refreshError:true});await open();click("Déclarer un impayé");change("Motif d’impayé","Refus");click("Enregistrer l’impayé");await screen.findByText("Action enregistrée.");await screen.findByText("Le service Règlements est indisponible. Réessayez.");});
});
