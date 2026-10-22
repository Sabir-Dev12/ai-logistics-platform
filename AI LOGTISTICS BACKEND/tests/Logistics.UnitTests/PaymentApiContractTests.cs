using Logistics.Application.Abstractions;
using Logistics.Application.Features.Payments;
using Logistics.Domain.Common;
using Logistics.Domain.Entities.Payments;
using Logistics.Domain.Enums;
using System.Reflection;
namespace Logistics.UnitTests;
public sealed class PaymentApiContractTests
{
 [Fact]
 public async Task Duplicate_allocation_is_rejected_before_touching_the_invoice_or_persistence()
 {
  var payment=new EnteteReglement("R1","S1",TypeReglement.Client,"C1","MAD",DateTime.UtcNow);
  payment.Allocate("F1",100,SensReglement.Vente);
  var invoiceCalls=0;var saves=0;
  var service=Create(payment,()=>invoiceCalls++,()=>saves++);
  var error=await Assert.ThrowsAsync<BusinessRuleException>(()=>service.AllocateAsync("R1",new("F1",100,SensReglement.Vente)));
  Assert.Contains("déjà affectée",error.Message);Assert.Equal(0,invoiceCalls);Assert.Equal(0,saves);
  Assert.Single(payment.Allocations);Assert.Equal(100,payment.Allocations.Single().Montant);
 }
 [Fact]
 public async Task Detail_exposes_payment_and_remittance_references_without_guessing_them()
 {
  var payment=new EnteteReglement("R1","S1",TypeReglement.Client,"C1","MAD",DateTime.UtcNow);
  payment.SetReferencePaiement("VIR-123");payment.SetTresorerieRemise("T1",DateTime.UtcNow,"REM-42","COMPTA-12");
  var dto=await Create(payment,()=>{},()=>{}).GetAsync("R1");
  Assert.Equal("VIR-123",dto.ReferencePaiement);Assert.Equal("REM-42",dto.KeyRemiseSys);Assert.Equal("COMPTA-12",dto.RefCompta);
 }
 private static ReglementService Create(EnteteReglement payment,Action invoiceRead,Action save)=>new(
  Proxy.Make<IReglementRepository>((m,args)=>m.Name switch {"GetAsync"=>Task.FromResult<EnteteReglement?>(payment),"SaveChangesAsync"=>Save(save),_=>throw new NotSupportedException(m.Name)}),
  Proxy.Make<IFactureVenteRepository>((m,args)=>{invoiceRead();throw new InvalidOperationException("Invoice must remain untouched");}),
  Proxy.Make<IReferenceDataRepository>((m,args)=>throw new NotSupportedException(m.Name)),
  Proxy.Make<IKeyGenerator>((m,args)=>Task.FromResult("unused")),
  Proxy.Make<IAuditTrail>((m,args)=>Task.CompletedTask),
  Proxy.Make<ITenantContext>((m,args)=>m.Name=="get_SocieteKey"?"S1":null));
 private static Task Save(Action save){save();return Task.CompletedTask;}
 public class Proxy:DispatchProxy
 {
  public Func<MethodInfo,object?[]?,object?> Handler=null!;
  public static T Make<T>(Func<MethodInfo,object?[]?,object?> handler) where T:class{var value=Create<T,Proxy>();((Proxy)(object)value).Handler=handler;return value;}
  protected override object? Invoke(MethodInfo? method,object?[]? args)=>Handler(method!,args);
 }
}
