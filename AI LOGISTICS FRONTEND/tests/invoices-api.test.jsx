import {render,screen,within,fireEvent,waitFor} from "@testing-library/react";
import {describe,it,expect,vi} from "vitest";
import SalesInvoicesPage from "../src/SalesInvoices/SalesInvoicesPage";
import {createInvoicesService} from "../src/services/invoicesService";
import {invoiceDto,pageDto} from "./fixtures/invoices";
function setup({readError=false,empty=false,writeStatus=200,refreshError=false}={}){let failed=readError,written=false;const request=vi.fn(async(url,opt)=>{let body,status=200;
if(opt.method==="POST"){written=true;status=writeStatus;body=status===200?invoiceDto():{detail:"SQL diagnostics"};}
else if(url.includes("/clients?"))body=pageDto([{keyClient:"C1",raisonSociale:"Client API"}],1,20);
else if(url.includes("/reference/devises"))body=[{codeDevise:"EUR"},{codeDevise:"MAD"},{codeDevise:"CHF"}];
else if(url.includes("factures-vente?")){if(failed||written&&refreshError){failed=false;status=503;body={};}else{const params=new URL(url,"http://localhost").searchParams;body=pageDto(empty||params.get("search")==="absent"?[]:[invoiceDto()],Number(params.get("page")),Number(params.get("pageSize")),empty||params.get("search")==="absent"?0:6);}}
else if(url.endsWith("/absent")){status=404;body={};}else body=invoiceDto();return new Response(JSON.stringify(body),{status});});render(<SalesInvoicesPage repository={createInvoicesService({request})}/>);return {request};}
async function open(){fireEvent.click(await screen.findByRole("button",{name:"FV-001",exact:true}));await screen.findByRole("heading",{name:"FV-001",exact:true});}
const click=name=>fireEvent.click(within(screen.getByRole("dialog")).getByRole("button",{name,exact:true}));
describe("Connected sales invoices UI",()=>{
 it("searches by number/reference/client, resets pagination and removes exact-key instructions",async()=>{
  const {request}=setup();await screen.findByRole("button",{name:"FV-001",exact:true});
  expect(screen.getByLabelText("Rechercher une facture").placeholder).toBe("Numéro, référence ou client");
  expect(screen.queryByText("Recherche par clé exacte. Les autres filtres s’appliquent au document trouvé.")).toBeNull();
  fireEvent.click(screen.getByRole("button",{name:"Suivant",exact:true}));
  await waitFor(()=>expect(request.mock.calls.at(-1)[0]).toContain("page=2"));
  fireEvent.change(screen.getByLabelText("Rechercher une facture"),{target:{value:"absent"}});
  await screen.findByText("Aucun résultat");
  expect(new URL(request.mock.calls.at(-1)[0],"http://local").searchParams.get("search")).toBe("absent");
  expect(new URL(request.mock.calls.at(-1)[0],"http://local").searchParams.get("page")).toBe("1");
  expect(request.mock.calls.some(([url])=>url==="/api/v1/factures-vente/absent")).toBe(false);
  fireEvent.click(screen.getAllByRole("button",{name:"Réinitialiser les filtres",exact:true})[0]);
  await screen.findByRole("button",{name:"FV-001",exact:true});
 });
 it("shows loading then server totals and pagination",async()=>{const {request}=setup();expect(screen.getByRole("status",{name:"Chargement des factures…"})).toBeTruthy();await screen.findByRole("button",{name:"FV-001",exact:true});expect(screen.queryByText("Mode démo")).toBeNull();expect(screen.getByText("1–5 sur 6 documents")).toBeTruthy();fireEvent.click(screen.getByRole("button",{name:"Suivant",exact:true}));await waitFor(()=>expect(request.mock.calls.some(([url])=>url.includes("page=2"))).toBe(true));});
 it("sends date filters and refuses backwards dates",async()=>{const {request}=setup();await screen.findByRole("button",{name:"FV-001",exact:true});fireEvent.change(screen.getByLabelText("Factures du"),{target:{value:"2026-10-31"}});fireEvent.change(screen.getByLabelText("Factures au"),{target:{value:"2026-10-01"}});await screen.findByText("La date de fin doit être postérieure ou égale à la date de début.");expect(request.mock.calls.some(([url])=>url.includes("from=2026-10-31"))).toBe(true);});
 it("retries a read failure and displays the initial empty state",async()=>{setup({readError:true,empty:true});await screen.findByText("Le service Factures est indisponible. Réessayez.");fireEvent.click(screen.getByRole("button",{name:"Réessayer"}));await screen.findByText("Aucune facture");});
 it("shows authoritative net, VAT recap and missing line VAT without fabrication",async()=>{setup();await open();const modal=within(screen.getByRole("dialog"));expect(modal.getByLabelText("Net à payer (EUR)").value).toBe("113,00");expect(modal.getByLabelText("Solde (serveur)").value).toBe("93,00");expect(modal.getByText("Non fourni")).toBeTruthy();const recap=modal.getByRole("table",{name:"Récapitulatif TVA des lignes"});expect(within(recap).getByText("20%")).toBeTruthy();expect(modal.queryByLabelText("Référence société")).toBeNull();});
 it("uses real client and currency references and no fictitious preview",async()=>{setup();await screen.findByRole("button",{name:"FV-001",exact:true});fireEvent.click(screen.getByRole("button",{name:"Créer une facture",exact:true}));await screen.findByRole("option",{name:"CHF"});const modal=within(screen.getByRole("dialog"));expect(modal.getAllByDisplayValue("Calculé par le serveur")).toHaveLength(9);expect(modal.getByText("Client API")).toBeTruthy();expect(modal.getByText(/ne génère pas de numéro automatique/)).toBeTruthy();});
 it("blocks duplicate submission after a lost write confirmation",async()=>{const {request}=setup({writeStatus:503});await open();click("Recalculer les montants");click("Confirmer");await screen.findAllByText("La confirmation n’a pas été reçue. Vérifiez la facture avant de recommencer.");expect(within(screen.getByRole("dialog")).getByRole("button",{name:"Confirmer",exact:true}).disabled).toBe(true);expect(request.mock.calls.filter(([,opt])=>opt.method==="POST")).toHaveLength(1);});
 it("keeps confirmed success when the following list refresh fails",async()=>{setup({refreshError:true});await open();click("Recalculer les montants");click("Confirmer");await screen.findByText("Action enregistrée.");await screen.findByText("Le service Factures est indisponible. Réessayez.");});
});
