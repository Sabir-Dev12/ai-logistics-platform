using System.Reflection;
using Logistics.Application.Abstractions;
using Logistics.Application.Features.Quotations;
using Logistics.Domain.Common;
using Logistics.Domain.Entities.Quotations;
namespace Logistics.UnitTests;
public sealed class BaremeCreationContractTests
{
 [Fact]
 public async Task Creation_saves_initial_columns_once_and_the_result_can_be_validated()
 {
  var context=new Context();var service=context.Service();
  var result=await service.CreateAsync(new("Tarif",Colonnes:[new(1,"Poids",0,100,"valeur",5,"Note")]));
  Assert.Equal(1,context.Saves);Assert.Single(result.Colonnes);Assert.Equal(StatutBareme.NonValide,result.Statut);
  Assert.Equal("S1",context.Stored!.SocieteKey);Assert.Equal(5,result.Colonnes[0].MtMin);
  var valid=await service.ValiderAsync(result.KeyBareme);Assert.True(valid.IsModifiable);
 }
 [Fact]
 public async Task Invalid_initial_columns_are_rejected_before_adding_or_saving_the_aggregate()
 {
  foreach(var columns in new IReadOnlyList<ColonneBaremeAchatInput>[] { [new(1,"A",0,1),new(1,"B",1,2)], [new(0,"A",0,1)], [new(1,"A",100,1)] })
  {var context=new Context();await Assert.ThrowsAsync<BusinessRuleException>(()=>context.Service().CreateAsync(new("Tarif",Colonnes:columns)));Assert.Null(context.Stored);Assert.Equal(0,context.Saves);}
 }
 [Fact]
 public async Task Failed_initial_save_does_not_report_success_or_issue_an_audit_event()
 {var context=new Context{Fail=true};await Assert.ThrowsAsync<InvalidOperationException>(()=>context.Service().CreateAsync(new("Tarif",Colonnes:[new(1,"A",0,100)])));Assert.Equal(0,context.Audits);}
 [Fact]
 public async Task Existing_header_only_callers_remain_compatible()
 {var context=new Context();var result=await context.Service().CreateAsync(new("Tarif"));Assert.Empty(result.Colonnes);Assert.False(result.IsModifiable);Assert.Equal(1,context.Saves);}
 private sealed class Context
 {
  public BaremeAchatCotation? Stored;public int Saves;public int Audits;public bool Fail;
  public BaremeAchatService Service()=>new(
   Proxy.Make<IBaremeAchatRepository>((m,args)=>m.Name switch {
    "LibelleExistsAsync"=>Task.FromResult(false),"FindAsync"=>Task.FromResult(Stored),
    "Add"=>Add(args![0]),"SaveChangesAsync"=>Save(),_=>throw new NotSupportedException(m.Name)}),
   Proxy.Make<IKeyGenerator>((m,args)=>Task.FromResult("B1")),
   Proxy.Make<IAuditTrail>((m,args)=>{Audits++;return Task.CompletedTask;}),
   Proxy.Make<ITenantContext>((m,args)=>m.Name=="get_SocieteKey"?"S1":null));
  object? Add(object? item){Stored=(BaremeAchatCotation)item!;return null;}
  Task Save(){if(Fail)throw new InvalidOperationException("Persistence failed");Saves++;return Task.CompletedTask;}
 }
 public class Proxy:DispatchProxy
 {
  public Func<MethodInfo,object?[]?,object?> Handler=null!;
  public static T Make<T>(Func<MethodInfo,object?[]?,object?> handler) where T:class {var value=Create<T,Proxy>();((Proxy)(object)value).Handler=handler;return value;}
  protected override object? Invoke(MethodInfo? method,object?[]? args)=>Handler(method!,args);
 }
}
