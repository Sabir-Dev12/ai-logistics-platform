using Logistics.Application.Abstractions;
using Logistics.Application.Features.Tracking;
using Logistics.Domain.Common;
using Logistics.Domain.Entities.Audit;
using Logistics.Domain.Entities.Shipments;
using Logistics.Infrastructure;
using Logistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
namespace Logistics.UnitTests;
public sealed class TrackingWriteContractTests
{
 [Fact]
 public async Task Explicit_event_preserves_business_date_and_returns_the_confirmed_row()
 {
  var repo=new Repository();var service=new TrackingService(repo);var date=new DateTime(2020,1,2,3,4,0,DateTimeKind.Utc);
  var result=await service.RecordEventAsync(TrackingEntity.Client,"C1",new("NOTE"," Note confirmée ",DateEvenement:date));
  Assert.Equal(date,result.DateTraceUser);Assert.Equal(repo.Stored!.DateTraceSystem,result.DateTraceSystem);
  Assert.Equal("Auteur serveur",result.Utilisateur);Assert.Equal("Note confirmée",result.Commentaire);Assert.Equal("Note",result.LibelleStatut);
  Assert.Equal(1,repo.Writes);Assert.Equal(0,repo.TimelineReads);
 }
 [Theory]
 [InlineData("$CREATION")][InlineData("WRONG_SCOPE")][InlineData("RETIRED")][InlineData("UNKNOWN")][InlineData("")]
 public async Task Invalid_statuses_are_rejected_before_any_write(string status)
 {
  var repo=new Repository();var service=new TrackingService(repo);
  await Assert.ThrowsAsync<BusinessRuleException>(()=>service.RecordEventAsync(TrackingEntity.Client,"C1",new(status,"Note")));
  Assert.Equal(0,repo.Writes);
 }
 [Fact]
 public async Task Missing_entity_and_invalid_inputs_are_rejected_before_persistence()
 {
  var repo=new Repository{Exists=false};var service=new TrackingService(repo);
  await Assert.ThrowsAsync<EntityNotFoundException>(()=>service.RecordEventAsync(TrackingEntity.Client,"C1",new("NOTE","Note")));
  foreach(var comment in new[]{"",new string('x',2001)})await Assert.ThrowsAsync<BusinessRuleException>(()=>service.RecordEventAsync(TrackingEntity.Client,"C1",new("NOTE",comment)));
  await Assert.ThrowsAsync<BusinessRuleException>(()=>service.RecordEventAsync(TrackingEntity.Client,"C1",new("NOTE","Note",Valeur:double.NaN)));
  Assert.Equal(0,repo.Writes);
 }
 [Fact]
 public async Task Persistence_failure_never_reads_an_old_event_as_a_successful_write()
 {
  var repo=new Repository{Fail=true};await Assert.ThrowsAsync<InvalidOperationException>(()=>new TrackingService(repo).RecordEventAsync(TrackingEntity.Client,"C1",new("NOTE","Note")));
  Assert.Equal(0,repo.TimelineReads);
 }
 [Fact]
 public async Task Sql_repository_uses_server_identity_and_propagates_save_failure_without_connecting()
 {
  var options=new DbContextOptionsBuilder<LogisticsDbContext>().UseSqlServer("Server=unreachable.invalid;Database=TrackingContract;Integrated Security=true;TrustServerCertificate=true").AddInterceptors(new FailBeforeDatabase()).Options;
  await using var db=new LogisticsDbContext(options);var repo=new TrackingRepository(db,new Tenant());var date=new DateTime(2020,1,2,0,0,0,DateTimeKind.Utc);
  await Assert.ThrowsAsync<InvalidOperationException>(()=>repo.RecordAsync(TrackingEntity.Client,"C1",new("NOTE","Note"),date));
  var entry=Assert.Single(db.ChangeTracker.Entries<TraceClient>()).Entity;
  Assert.Equal(date,entry.DateTraceUser);Assert.Equal("Auteur serveur",entry.Utilisateur);Assert.Equal("S01",entry.Societe);Assert.Equal("AG1",entry.Agence);
 }
 private sealed class FailBeforeDatabase:SaveChangesInterceptor
 {
  public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,InterceptionResult<int> result,CancellationToken cancellationToken=default)=>throw new InvalidOperationException("Fixture persistence failure before database access.");
 }
 private sealed class Tenant:ITenantContext
 {public string SocieteKey=>"S01";public string? AgenceKey=>"AG1";public string? UserKey=>"U1";public string? UserDisplayName=>"Auteur serveur";public string? Workstation=>"Test";}
 private sealed class Repository:ITrackingRepository
 {
  public bool Exists=true,Fail;public int Writes,TimelineReads;public TraceEntry? Stored;
  public Task<bool> EntityExistsAsync(TrackingEntity entity,string key,CancellationToken ct=default)=>Task.FromResult(Exists);
  public Task<TraceEntry> RecordAsync(TrackingEntity entity,string key,RecordTrackingEventRequest request,DateTime businessDate,CancellationToken ct=default)
  {if(Fail)throw new InvalidOperationException("Fixture failure");Writes++;Stored=new TraceClient(key,"S01","AG1",request.KeyStatut,businessDate,new DateTime(2026,10,17,0,0,0,DateTimeKind.Utc),request.Commentaire,"Auteur serveur",request.KeyDocument,request.Valeur,"Test");return Task.FromResult(Stored);}
  public Task<IReadOnlyList<TraceEntry>> GetTimelineAsync(TrackingEntity entity,string key,int take,CancellationToken ct=default){TimelineReads++;throw new InvalidOperationException("Old event must not be used to confirm an insertion");}
  public Task<IReadOnlyList<TraceEntry>> GetActivityAsync(IReadOnlyCollection<TrackingEntity> entities,DateTime? from,DateTime? to,int take,CancellationToken ct=default)=>throw new NotSupportedException();
  public Task<IReadOnlyDictionary<string,string>> GetStatutLibellesAsync(IEnumerable<string?> keys,CancellationToken ct=default)=>throw new NotSupportedException();
  public Task<IReadOnlyList<Statut>> GetStatutsAsync(string? utilisation,CancellationToken ct=default)
  {var retired=new Statut("RETIRED","Retiré","Client");retired.SoftDelete();return Task.FromResult<IReadOnlyList<Statut>>([new("NOTE","Note"),new("$CREATION","Création"),new("WRONG_SCOPE","Autre","Voyage"),retired]);}
 }
}
