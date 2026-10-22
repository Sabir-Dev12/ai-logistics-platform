import {describe,it,expect,vi} from "vitest";
import {createPaymentsService} from "../src/services/paymentsService";
import {paymentDto,pageDto,createValue,referenceResponse} from "./fixtures/payments";
const response=(body,status=200)=>new Response(JSON.stringify(body),{status});
const setup=handler=>{const request=vi.fn(handler||(()=>Promise.resolve(response(paymentDto()))));return {request,service:createPaymentsService({request,timeoutMs:10})};};
describe("Payments API contract",()=>{
 it("searches the server list by text and party while preserving totals and other filters",async()=>{
  const {service,request}=setup(()=>Promise.resolve(response(pageDto([paymentDto()],2,5,8))));
  const result=await service.list({search:" VIR ",party:" Atlas ",type:"Client",status:"Impaye",page:2});
  const params=new URL(request.mock.calls[0][0],"http://local").searchParams;
  expect(Object.fromEntries(params)).toEqual({page:"2",pageSize:"5",search:"VIR",type:"0",tiers:"Atlas",statut:"6"});
  expect(result.totalCount).toBe(8);expect(request).toHaveBeenCalledTimes(1);
 });
 it("keeps an unmatched search empty without calling detail",async()=>{
  const {service,request}=setup(()=>Promise.resolve(response(pageDto([],1,5,0))));
  expect((await service.list({search:"absent"})).payments).toEqual([]);
  expect(request.mock.calls[0][0]).toContain("reglements?");expect(request).toHaveBeenCalledTimes(1);
 });
 it.each(["", null, undefined, "   "])("creates without optional payment mode %s when no mode is configured",async(mode)=>{
  const {service,request}=setup((url,opt)=>Promise.resolve(response(url.endsWith("/modes-paiement")?[]:referenceResponse(url)||paymentDto({modePaiement:null}),opt.method==="POST"?201:200)));
  await service.create({...createValue(),mode});
  const writes=request.mock.calls.filter(([,opt])=>opt.method==="POST");
  expect(writes).toHaveLength(1);
  expect(JSON.parse(writes[0][1].body)).toMatchObject({modePaiement:null,devise:"EUR",montantDevise:100});
 });
 it("keeps an explicitly selected real payment mode",async()=>{
  const {service,request}=setup((url,opt)=>Promise.resolve(response(referenceResponse(url)||paymentDto(),opt.method==="POST"?201:200)));
  await service.create(createValue());
  expect(JSON.parse(request.mock.calls.at(-1)[1].body).modePaiement).toBe("VIR");
 });
 it.each(["UNKNOWN", {}, 123])("rejects an unavailable or malformed payment mode %s before writing",async(mode)=>{
  const {service,request}=setup(url=>Promise.resolve(response(referenceResponse(url))));
  await expect(service.create({...createValue(),mode})).rejects.toThrow("mode de paiement");
  expect(request.mock.calls.some(([,opt])=>opt.method==="POST")).toBe(false);
 });
 it("still rejects an unknown currency when payment mode is absent",async()=>{
  const {service,request}=setup(url=>Promise.resolve(response(referenceResponse(url))));
  await expect(service.create({...createValue(),currency:"UNKNOWN",mode:""})).rejects.toThrow("devise");
  expect(request.mock.calls.some(([,opt])=>opt.method==="POST")).toBe(false);
 });
 it("uses server pagination and filter enum values",async()=>{const {service,request}=setup(()=>Promise.resolve(response(pageDto([paymentDto()],2,5,8))));expect((await service.list({type:"Fournisseur",party:"F1",status:"Impaye",page:2})).totalCount).toBe(8);expect(request.mock.calls[0][0]).toBe("/api/v1/reglements?page=2&pageSize=5&type=1&tiers=F1&statut=6");expect(request).toHaveBeenCalledTimes(1);});
 it.each([[0,"Saisie"],[1,"Portefeuille"],[2,"RemisEnBanque"],[3,"RemisAuFournisseur"],[4,"Signe"],[5,"Regle"],[6,"Impaye"]])("maps status %s correctly",async(code,name)=>{const {service}=setup(()=>Promise.resolve(response(paymentDto({statut:code}))));expect((await service.get("R1")).status).toBe(name);});
 it("preserves signed amounts, bank references and allocations",async()=>{const {service}=setup(()=>Promise.resolve(response(paymentDto({montantDevise:-100,keyRemiseSys:"REM1",refCompta:"COMPTA",allocations:[{keyFacture:"F1",montant:20,devise:"EUR",sens:1}]}))));const row=await service.get("R1");expect(row).toMatchObject({amount:-100,reference:"VIR-123",remittanceId:"REM1",accountingReference:"COMPTA"});expect(row.allocations[0]).toMatchObject({invoiceId:"F1",amount:20,side:"Achat"});});
 it("loads real references and company currency",async()=>{const {service,request}=setup(url=>Promise.resolve(response(referenceResponse(url))));const refs=await service.references();expect(refs).toMatchObject({companyCurrency:"MAD",modes:[{id:"VIR"}],treasuries:[{id:"T1",label:"Banque"}],currencies:["EUR","MAD"]});expect(request).toHaveBeenCalledTimes(4);});
 it("remittance lookup needs only treasury references",async()=>{const {service,request}=setup(url=>Promise.resolve(response(referenceResponse(url))));expect((await service.references({treasuryOnly:true})).treasuries).toHaveLength(1);expect(request).toHaveBeenCalledTimes(1);});
 it("creates with negative disbursement, enum, dates and a whitelisted payload",async()=>{const {service,request}=setup((url,opt)=>Promise.resolve(response(referenceResponse(url)||paymentDto({montantDevise:-100}),opt.method==="POST"?201:200)));await service.create({...createValue(),type:"Fournisseur",direction:"out",societeKey:"forged",statut:6});const body=JSON.parse(request.mock.calls.at(-1)[1].body);expect(body).toMatchObject({typeReglement:1,keyTiers:"C1",montantDevise:-100,tauxDevise:10,dateOperation:"2026-10-01T00:00:00",referencePaiement:"VIR-123"});expect(body).not.toHaveProperty("societeKey");expect(body).not.toHaveProperty("statut");});
 it("posts allocation amounts, side and optional withholding without a post-write read",async()=>{const {service,request}=setup((url,opt)=>Promise.resolve(response(opt.method==="POST"?{allocated:40,montantMad:1000,ecart:0.5,ecartDecision:0,odKeyFacture:"OD1"}:paymentDto())));const result=await service.allocate("R1",{invoiceId:"F1",amount:40,side:"Achat",rasIsGlobal:5,netAPayerGlobal:100});expect(result.allocationResult.odKeyFacture).toBe("OD1");expect(JSON.parse(request.mock.calls[1][1].body)).toEqual({keyFacture:"F1",montant:40,sens:1,rasIsGlobal:5,netAPayerGlobal:100});expect(request).toHaveBeenCalledTimes(2);});
 it("refuses duplicate allocation before an HTTP write",async()=>{const {service,request}=setup(()=>Promise.resolve(response(paymentDto({allocations:[{keyFacture:"F1",montant:20,devise:"EUR",sens:0}]}))));await expect(service.allocate("R1",{invoiceId:"F1",amount:40})).rejects.toThrow("déjà affectée");expect(request.mock.calls.some(([,opt])=>opt.method==="POST")).toBe(false);});
 it.each([["deallocate","/allocations/F%2F1",{invoiceId:"F/1"},"DELETE"],["remit","/remise?keyTresorie=T1&dateRemise=2026-10-01T00%3A00%3A00&refCompta=REF+%26",{treasury:"T1",date:"2026-10-01",reference:"REF &"},"POST"],["unpaid","/impaye?motif=Refus&commentaire=Note+%26",{reason:"Refus",comment:"Note &"},"POST"]])("calls %s via the existing contract",async(method,suffix,value,verb)=>{const {service,request}=setup();await service[method]("R1",value);expect(request.mock.calls[0][0]).toBe("/api/v1/reglements/R1"+suffix);expect(request.mock.calls[0][1].method).toBe(verb);});
 it.each([400,403,404,409,422])("handles HTTP %s safely",async(status)=>{const {service}=setup(()=>Promise.resolve(response({detail:"SQL secret"},status)));await expect(service.get("R1")).rejects.not.toHaveProperty("message","SQL secret");});
 it.each([500,503])("never retries ambiguous HTTP %s writes",async(status)=>{const {service,request}=setup(()=>Promise.resolve(response({},status)));await expect(service.unpaid("R1",{reason:"Refus"})).rejects.toMatchObject({uncertain:true});expect(request).toHaveBeenCalledTimes(1);});
 it("distinguishes failed reads from uncertain writes",async()=>{const {service}=setup(()=>Promise.reject(new TypeError("offline")));await expect(service.get("R1")).rejects.toMatchObject({code:"network"});await expect(service.unpaid("R1",{reason:"Refus"})).rejects.toMatchObject({uncertain:true});});
 it("rejects invalid confirmations and wrong keys",async()=>{const {service}=setup(()=>Promise.resolve(response({})));await expect(service.deallocate("R1",{invoiceId:"F1"})).rejects.toMatchObject({uncertain:true});const wrong=setup(()=>Promise.resolve(response(paymentDto({keyReglement:"OTHER"}))));await expect(wrong.service.get("R1")).rejects.toMatchObject({code:"invalid_response"});});
 it("supports cancellation and read timeout",async()=>{const {service}=setup((url,opt)=>new Promise((resolve,reject)=>{if(opt.signal.aborted)reject(new DOMException("Aborted","AbortError"));else opt.signal.addEventListener("abort",()=>reject(new DOMException("Aborted","AbortError")));}));const controller=new AbortController();controller.abort();await expect(service.get("R1",{signal:controller.signal})).rejects.toMatchObject({name:"AbortError"});await expect(service.get("R1")).rejects.toMatchObject({code:"timeout"});});
 it("rejects invalid dates, missing incident reason and nonpositive allocation before writing",async()=>{const {service,request}=setup();expect(()=>service.remit("R1",{treasury:"T1",date:"2026-02-31"})).toThrow("date");expect(()=>service.unpaid("R1",{reason:""})).toThrow("motif");await expect(service.allocate("R1",{invoiceId:"F1",amount:0})).rejects.toThrow("montant");expect(request).not.toHaveBeenCalled();});
});
