using System.Reflection;
using System.Text.Json;
using Logistics.Application.Abstractions;
using Logistics.Application.Features.Shipments;
using Logistics.Domain.Entities.Shipments;
namespace Logistics.UnitTests;
public sealed class VoyageDetailContractTests
{
 [Fact]
 public async Task Header_update_preserves_hidden_values_and_trailer_capacity()
 {
  var date=new DateTime(2026,10,17);
  var voyage=new Voyage("V1","S01","VI","VOY1",date);
  voyage.UpdateEntete("VOY1","VI",NatureVoyage.Groupage,CategorieVoyage.International,"Route","Tanger","Lyon","Conserver");
  voyage.SetAgences("AS","Agence saisie","AE","Agence exploitation");
  voyage.SetItineraire(date,date.AddDays(2),date,date.AddDays(2),"Tanger","Marseille","N1","N2",date.AddDays(1),date.AddDays(2),"Transbordement");
  voyage.SetPersonnel("EX","Exploitant","C1","Chauffeur 1","C2","Chauffeur 2");
  voyage.SetDouane("AC",date,"MA",date,"DS",date,"CNT");
  voyage.SetMoyens("REM","TRA","TC","CAISSE","TRIP",date,1500,80);
  voyage.SetMagasinMead("M1",date);
  var repo=DispatchProxy.Create<IVoyageRepository,RepositoryProxy>();((RepositoryProxy)(object)repo).Voyage=voyage;
  var service=new VoyageService(repo,null!,null!,DispatchProxy.Create<IAuditTrail,AuditProxy>(),null!);
  var before=await service.GetAsync("V1");Assert.NotNull(before.EditableFields);
  var request=before.EditableFields! with {Commentaire="Modifié"};
  var after=await service.UpdateAsync("V1",request);
  var options=new JsonSerializerOptions(JsonSerializerDefaults.Web);
  var expected=JsonSerializer.SerializeToElement(request,options);var actual=JsonSerializer.SerializeToElement(after.EditableFields,options);
  foreach(var property in expected.EnumerateObject())Assert.Equal(property.Value.GetRawText(),actual.GetProperty(property.Name).GetRawText());
  Assert.Equal(80,voyage.CapaciteRemorque);Assert.Equal(1500,voyage.Tare);Assert.Equal("TRIP",voyage.NumTriptyque);Assert.Equal(date,voyage.DateTriptyque);
  Assert.Equal("Transbordement",voyage.PortTransbordement);Assert.Equal(date.AddDays(1),voyage.DateDepartTrans);Assert.Equal(date.AddDays(2),voyage.DateArriveeTrans);
  Assert.Equal("Exploitant",voyage.IntituleExploitant);Assert.Equal("Chauffeur 1",voyage.IntituleChauffeur1);Assert.Equal("Chauffeur 2",voyage.IntituleChauffeur2);
  Assert.Equal(date,voyage.DateAcquit);Assert.Equal(date,voyage.DateManifeste);Assert.Equal(date,voyage.DateDs);Assert.Equal(date,voyage.EntreeMead);
 }
 public class RepositoryProxy:DispatchProxy
 {
  public Voyage Voyage{get;set;}=null!;
  protected override object? Invoke(MethodInfo? method,object?[]? args)=>method?.Name switch
  {"GetAsync"=>Task.FromResult<Voyage?>(Voyage),"NumVoyageExistsAsync"=>Task.FromResult(false),"SaveChangesAsync"=>Task.CompletedTask,_=>throw new NotSupportedException()};
 }
 public class AuditProxy:DispatchProxy{protected override object? Invoke(MethodInfo? method,object?[]? args)=>Task.CompletedTask;}
}
