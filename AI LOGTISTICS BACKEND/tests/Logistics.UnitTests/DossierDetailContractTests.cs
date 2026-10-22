using System.Reflection;
using System.Text.Json;
using Logistics.Application.Abstractions;
using Logistics.Application.Features.Shipments;
using Logistics.Domain.Entities.Shipments;
namespace Logistics.UnitTests;
public sealed class DossierDetailContractTests
{
 [Fact]
 public async Task Editing_a_visible_field_preserves_hidden_and_out_of_contract_values()
 {
  var date=new DateTime(2026,10,17);
  var dossier=new Dossier("D1","S01","DOS1","RI",date);
  dossier.UpdateEntete("DOS1","RI","C1","Client","Transport","Route",3,"Commentaire");
  dossier.SetAgences("AE","Agence exploitation","AC","Agence commerciale","AS","Agence saisie");
  dossier.SetParties("E1","Expéditeur","DE1","Destinataire","P1","Payeur","A1","Agent","Réf agent");
  dossier.SetMarchandise("Marchandise",true,true,"3",true,true,123,"MAD",1,4,"Palette",2,"Marque");
  dossier.SetMesures(100,2,3,120,4,5,6,7);
  dossier.SetCustoms("HS","R","B","DUM",date,456,"Transporteur");
  dossier.SetTransport(true,2,8,1000,"MAD");
  dossier.SetMagasinDouane("MEAD");dossier.Depotage(date,LieuDepotage.Tc,"Conforme",4,"REF1");
  dossier.AssignVoyage("V1");dossier.LinkBooking("BK1");
  var repo=DispatchProxy.Create<IDossierRepository,RepositoryProxy>();((RepositoryProxy)(object)repo).Dossier=dossier;
  var audit=DispatchProxy.Create<IAuditTrail,AuditProxy>();
  var service=new DossierService(repo,null!,null!,audit,null!,null!);
  var before=await service.GetAsync("D1");
  Assert.NotNull(before.EditableFields);
  var request=before.EditableFields! with {NumDossier="DOS2"};
  var after=await service.UpdateAsync("D1",request);
  var options=new JsonSerializerOptions(JsonSerializerDefaults.Web);
  var expected=JsonSerializer.SerializeToElement(request,options);
  var actual=JsonSerializer.SerializeToElement(after.EditableFields,options);
  foreach(var property in expected.EnumerateObject())Assert.Equal(property.Value.GetRawText(),actual.GetProperty(property.Name).GetRawText());
  Assert.Equal("AC",dossier.KeyAgenceCommerciale);Assert.Equal("AS",dossier.KeyAgenceSaisie);
  Assert.Equal("Réf agent",dossier.RefAgent);Assert.Equal(123,dossier.ValeurMarchandise);
  Assert.Equal("MAD",dossier.DeviseValeurMarchandise);Assert.Equal(1,dossier.TauxChangeValMarch);Assert.Equal(2,dossier.Suspendu);
  Assert.Equal(3,dossier.Mpl);Assert.Equal(5,dossier.EquivalenceVolumePoids);Assert.Equal(6,dossier.EquivalenceMplPoids);Assert.Equal(7,dossier.EquivalenceMplVolume);
  Assert.Equal("DUM",dossier.NumDum);Assert.Equal(date,dossier.DateDum);Assert.Equal(456,dossier.ValeurDeclareeDouane);Assert.Equal("Transporteur",dossier.LivraisonPar);
  Assert.Equal("Conforme",after.ConstatDepotage);Assert.Equal("REF1",after.RefDepotage);
  Assert.Equal("V1",after.KeyVoyageInter);Assert.Equal("BK1",after.KeyBooking);
 }
 public class RepositoryProxy:DispatchProxy
 {
  public Dossier Dossier{get;set;}=null!;
  protected override object? Invoke(MethodInfo? method,object?[]? args)=>method?.Name switch
  {"GetAsync"=>Task.FromResult<Dossier?>(Dossier),"NumDossierExistsAsync"=>Task.FromResult(false),"SaveChangesAsync"=>Task.CompletedTask,_=>throw new NotSupportedException()};
 }
 public class AuditProxy:DispatchProxy{protected override object? Invoke(MethodInfo? method,object?[]? args)=>Task.CompletedTask;}
}
