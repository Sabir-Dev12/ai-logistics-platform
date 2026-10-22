import { describe, expect, it, vi } from "vitest";
import { createClientsService } from "../src/services/clientsService";
import { createQuotationsService } from "../src/services/quotationsService";
import { createScalesService } from "../src/services/scalesService";
import { createBusinessApi } from "../src/services/businessApi";
const response = (body, status = 200) => new Response(status === 204 ? null : JSON.stringify(body), {status});
const page = (items, extra = {}) => ({items,page:1,pageSize:5,totalCount:items.length,...extra});
const client = (extra = {}) => ({keyClient:"C1",raisonSociale:"Client serveur",actif:true,valide:false,adresses:[],banques:[],finMois:true,releveFacture:0,plafond:4200,...extra});
const quote = (extra = {}) => ({keyCotation:"Q1",numCotation:"DEV-1",keyClient:"C1",statut:"Saisie",dateCotation:"2026-10-18T12:00:00",dateValidite:null,prix:[],achats:[],colisages:[],tauxChange:[],documents:[],totaux:{totalVente:120,totalAchat:80,marge:40,devisesSansTaux:[]},editableFields:{numCotation:"DEV-1",mode:"Mer",frequenceDepart:"Hebdomadaire",transitTime:"4 jours",langueClient:"FR",commentairePrix:"À conserver"},...extra});
const scale = (extra = {}) => ({keyBareme:"S1",libelleBareme:"Tarif serveur",statut:1,typeBareme:"Groupage",debutValidite:"2026-01-01T00:00:00",nbColonnes:2,nbPrestations:3,colonnes:[],details:[],prestations:[],...extra});

describe("Existing business API contracts",()=>{
 it.each(["true","false",""])("clients sends the validation filter %s before server paging",async validated=>{
  const request=vi.fn(async()=>response(page([],{page:2,totalCount:0})));
  const service=createClientsService({request});await service.list({validated,active:"true",page:2,pageSize:5});
  const url=new URL(request.mock.calls[0][0],"http://local");
  expect(url.searchParams.get("valide")).toBe(validated||null);
  expect(url.searchParams.get("actif")).toBe("true");expect(url.searchParams.get("page")).toBe("2");
  expect(request).toHaveBeenCalledTimes(1);
 });
 it("clients uses server paging/search and does not fetch every detail",async()=>{
  const request=vi.fn(async()=>response(page([client()],{page:2,totalCount:6})));
  const service=createClientsService({request}); const result=await service.list({query:"Atlas",active:"true",page:2,pageSize:5});
  const url=new URL(request.mock.calls[0][0],"http://local");expect(url.searchParams.get("page")).toBe("2");expect(url.searchParams.get("search")).toBe("Atlas");expect(url.searchParams.get("actif")).toBe("true");expect(result.totalCount).toBe(6);expect(request).toHaveBeenCalledTimes(1);
 });
 it("clients preserves undisplayed writable fields and excludes read-only flags on PUT",async()=>{
  const request=vi.fn(async(path,options)=>response(options.method==="GET"?client({societeKey:"S01",valide:true,supprime:false}):client({raisonSociale:"Renommé"})));
  const service=createClientsService({request});await service.get("C1");await service.update("C1",{raisonSociale:"Renommé",villeClient:"Tanger"});
  const body=JSON.parse(request.mock.calls.find(([,o])=>o.method==="PUT")[1].body);
  expect(body).toMatchObject({raisonSociale:"Renommé",finMois:true,plafond:4200});expect(body).not.toHaveProperty("societeKey");expect(body).not.toHaveProperty("valide");expect(body).not.toHaveProperty("adresses");
 });
 it("clients uses the existing validate route and refreshes child resources",async()=>{
  const request=vi.fn(async(path,o)=>o.method==="POST"&&path.endsWith("adresses")?response({keyAdresse:"A1"},201):response(client()));
  const service=createClientsService({request});await service.validate("C1");expect(request.mock.calls[0][0]).toBe("/api/v1/clients/C1/validate");await service.addAddress("C1",{ville:"Tanger",adresse1:"Adresse de test"});expect(request.mock.calls.at(-1)[0]).toBe("/api/v1/clients/C1");
 });
 it.each(["clients","quotes","scales"])("%s rejects malformed paging rather than inventing empty data",async kind=>{
  const service=({clients:createClientsService,quotes:createQuotationsService,scales:createScalesService})[kind]({request:async()=>response({items:[],page:1,pageSize:5,totalCount:-1})});await expect(service.list()).rejects.toThrow("invalide");
 });
 it("quotation summaries do not invent totals or document sizes",async()=>{
  const service=createQuotationsService({request:async()=>response(page([{keyCotation:"Q1",numCotation:"DEV-1",keyClient:"C1",attachement:true}]))});
  const result=await service.list();expect(result.quotations[0].serverTotals).toBeUndefined();expect(result.quotations[0].remote).toBe(true);
 });
 it("a malformed quotation read is not reported as an uncertain write",async()=>{
  const service=createQuotationsService({request:async()=>response({})});
  await expect(service.get("Q1")).rejects.toMatchObject({code:"invalid_response",uncertain:false});
 });
 it("quotation PUT retains the full editable server snapshot",async()=>{
  const request=vi.fn(async()=>response(quote()));const service=createQuotationsService({request});await service.get("Q1");await service.update("Q1",{numCotation:"DEV-1",commentaire:"Nouveau",dateValidite:"2026-12-01"});
  const body=JSON.parse(request.mock.calls.find(([,o])=>o.method==="PUT")[1].body);expect(body).toMatchObject({mode:"Mer",frequenceDepart:"Hebdomadaire",transitTime:"4 jours",langueClient:"FR",commentairePrix:"À conserver",commentaire:"Nouveau"});
 });
 it("quotation maps numeric calculations and keeps hidden price fields on line replacement",async()=>{
  const request=vi.fn(async()=>response(quote({prix:[{keyLignePrix:"P1",libelleRubrique:"Fret",typeUtilisation:1,position:7,codeRubrique:"FR",codeTva:"TV20",montantValeur:5,quantite:1,devise:"MAD",baseCalcul:"FRET"}]})));
  const service=createQuotationsService({request});const row=await service.get("Q1");expect(row.prix[0].typeUtilisation).toBe("Pourcentage");await service.setLines("Q1",{kind:"prix",lines:row.prix});
  expect(JSON.parse(request.mock.calls.at(-1)[1].body)[0]).toMatchObject({position:7,codeRubrique:"FR",codeTva:"TV20",baseCalcul:"FRET",typeUtilisation:1});
 });
 it("quotation associates metadata without pretending to upload files",async()=>{
  const request=vi.fn(async()=>response(quote()));await createQuotationsService({request}).addDocument("Q1",{name:"Conditions",path:"documents/conditions.pdf",type:"PDF"});expect(JSON.parse(request.mock.calls[0][1].body)).toEqual({libelleDocument:"Conditions",cheminDocument:"documents/conditions.pdf",typePieceDocument:"PDF"});
 });
 it("scales maps summary counts and lifecycle enum",async()=>{
  const service=createScalesService({request:async()=>response(page([scale()]))});const result=await service.list();expect(result.scales[0]).toMatchObject({status:"Validé",from:"2026-01-01",columnCount:2,serviceCount:3});
 });
 it("scales uses backend tariff resolution and encoded identifiers",async()=>{
  const request=vi.fn(async()=>response({trouve:true,idColonne:1,montant:12,devise:"MAD",libelleColonne:"Tranche"}));const result=await createScalesService({request}).resolve("S/1","75",100);expect(request.mock.calls[0][0]).toBe("/api/v1/baremes-achat/S%2F1/tarif?departement=75&valeur=100");expect(result).toMatchObject({found:true,amount:12});
 });
 it("scales sends existing column and cell contracts",async()=>{
  const request=vi.fn(async()=>response(scale()));const service=createScalesService({request});await service.setLines("S1",{kind:"cells",lines:[{columnId:1,department:"75",amount:42}]});expect(JSON.parse(request.mock.calls[0][1].body)).toEqual([{idColonne:1,departement:"75",montant:42}]);
 });
 it("does not retry a write and marks 5xx as uncertain",async()=>{
  const request=vi.fn(async()=>response({},503));await expect(createBusinessApi(request)("/write",{method:"POST"})).rejects.toMatchObject({uncertain:true});expect(request).toHaveBeenCalledTimes(1);
 });
 it("distinguishes server refusal, network loss and cancellation",async()=>{
  await expect(createBusinessApi(async()=>response({},422))("/read")).rejects.toMatchObject({code:"422",uncertain:false});
  await expect(createBusinessApi(async()=>{throw new TypeError("Network");})("/read")).rejects.toMatchObject({code:"network"});
  const controller=new AbortController();controller.abort();await expect(createBusinessApi(async()=>{throw new DOMException("Aborted","AbortError");})("/read",{signal:controller.signal})).rejects.toMatchObject({name:"AbortError"});
 });
});
