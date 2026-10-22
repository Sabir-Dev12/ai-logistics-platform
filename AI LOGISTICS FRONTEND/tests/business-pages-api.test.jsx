import { fireEvent, render, screen, within, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import ClientsPage from "../src/Clients/ClientsPage";
import QuotationsPage from "../src/Quotations/QuotationsPage";
import PurchaseScalesPage from "../src/PurchaseScales/PurchaseScalesPage";
const client={keyClient:"C1",raisonSociale:"Client API",actif:true,valide:true,adresses:[],banques:[],dateCreation:null};
describe("Connected business page states",()=>{
 it("validation is enabled, sent to the server, resets paging and clears with the other filters",async()=>{
  const repository={remote:true,list:vi.fn(async({validated})=>({clients:validated?[]:[client],totalCount:validated?0:6}))};
  render(<ClientsPage repository={repository}/>);
  await screen.findByText("1–5 sur 6 clients");
  const validation=screen.getByRole("combobox",{name:"Validation du client"});
  expect(validation.disabled).toBe(false);
  expect(screen.queryByText("Filtre de validation non exposé par cette API.")).toBeNull();
  fireEvent.click(screen.getByRole("button",{name:"Suivant"}));
  await screen.findByText("6–6 sur 6 clients");
  fireEvent.change(validation,{target:{value:"false"}});
  await screen.findByText("Aucun résultat");
  expect(repository.list.mock.calls.at(-1)[0]).toMatchObject({validated:"false",page:1});
  expect(screen.queryByText("Créez un premier client pour commencer.")).toBeNull();
  fireEvent.click(screen.getAllByRole("button",{name:"Réinitialiser les filtres"})[0]);
  await waitFor(()=>expect(repository.list.mock.calls.at(-1)[0]).toMatchObject({validated:"",query:"",active:"",page:1}));
 });
 it("an empty client search offers filter reset instead of first-client onboarding",async()=>{
  const repository={remote:true,list:vi.fn(async()=>({clients:[],totalCount:0}))};render(<ClientsPage repository={repository}/>);
  await screen.findByText("Aucun client");
  fireEvent.change(screen.getByRole("textbox",{name:"Rechercher un client"}),{target:{value:"INEXISTANT"}});
  await screen.findByText("Aucun résultat");
  expect(screen.queryByText("Créez un premier client pour commencer.")).toBeNull();
  expect(screen.getByRole("textbox",{name:"Rechercher un client"}).placeholder).toBe("Nom, référence ou identifiant");
 });
 it("client pagination uses server totals and loads full detail only on demand",async()=>{
  const repository={remote:true,list:vi.fn(async({page})=>({clients:[{...client,keyClient:`C${page}`}],totalCount:6})),get:vi.fn(async id=>({...client,keyClient:id}))};render(<ClientsPage repository={repository}/>);
  await screen.findByText("1–5 sur 6 clients");expect(screen.queryByText("Mode démo")).toBeNull();expect(repository.get).not.toHaveBeenCalled();fireEvent.click(screen.getByRole("button",{name:"Suivant"}));await screen.findByText("6–6 sur 6 clients");fireEvent.click(screen.getByRole("button",{name:"Client API",exact:true}));await within(screen.getByRole("dialog")).findByText("Référence interne : C2 · Créé le —");expect(repository.get).toHaveBeenCalledWith("C2",expect.anything());expect(screen.getByRole("button",{name:"Supprimer le client",exact:true}).disabled).toBe(true);
 });
 it.each([["clients",ClientsPage,"Aucun client"],["quotes",QuotationsPage,"Aucune cotation"],["scales",PurchaseScalesPage,"Aucun barème"]])("%s shows failure and retry without a false empty state",async(name,Page,empty)=>{
  const repository={remote:true,list:vi.fn(async()=>{throw new Error("Lecture indisponible");}),clients:async()=>({items:[],totalCount:0})};render(<Page repository={repository}/>);await screen.findByText("Lecture indisponible");expect(screen.queryByText(empty)).toBeNull();expect(screen.queryByText("0 client")).toBeNull();
 });
 it("scale summaries display server line counts",async()=>{
  const repository={remote:true,list:async()=>({scales:[{id:"S1",label:"Grille API",supplier:"Fournisseur",type:"Groupage",status:"Validé",from:"2026-01-01",to:"2026-12-31",currency:"MAD",columns:[],services:[],columnCount:12,serviceCount:4}],totalCount:1})};render(<PurchaseScalesPage repository={repository}/>);const table=await screen.findByRole("table",{name:"Liste des barèmes d’achat"});expect(within(table).getByText("12")).toBeTruthy();expect(within(table).getByText("4")).toBeTruthy();expect(screen.queryByText("Mode démo")).toBeNull();
 });
});
