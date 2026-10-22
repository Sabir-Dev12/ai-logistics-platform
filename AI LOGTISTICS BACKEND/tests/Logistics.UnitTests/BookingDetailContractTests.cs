using System.Reflection;
using System.Text.Json;
using Logistics.Application.Features.Bookings;
using Logistics.Domain.Entities.Bookings;
namespace Logistics.UnitTests;
public sealed class BookingDetailContractTests
{
 [Fact]
 public async Task Detail_returns_editable_header_and_recorded_unloading_fields()
 {
  var date=new DateTime(2026,10,17);
  var booking=new Booking("B1","S01","RES1","RI",date);
  booking.UpdateEntete("RES1","RI",null,"FCA","Route",date.AddDays(3),null,"Conserver ce commentaire","Transport");
  booking.SetItineraire("Tanger",null,date.AddDays(2),null,null,date.AddDays(1),null,null);
  booking.Depotage(date,"Colis vérifiés",4,"M1","D1");
  var repo=DispatchProxy.Create<IBookingRepository, BookingRepositoryProxy>();
  ((BookingRepositoryProxy)(object)repo).Booking=booking;
  var service=new BookingService(repo,null!,null!,null!,null!,null!,null!,null!);
  var detail=await service.GetAsync("B1");
  var options=new JsonSerializerOptions(JsonSerializerDefaults.Web);
  var update=JsonSerializer.Deserialize<UpdateBookingRequest>(JsonSerializer.Serialize(detail,options),options)!;
  Assert.Equal("Conserver ce commentaire",update.Commentaire);
  Assert.Equal("Transport",update.NatureOperation);
  Assert.Equal("Tanger",detail.PortDepart);
  Assert.Equal(date.AddDays(2),detail.DateDepartPrevue);
  Assert.Equal(date.AddDays(1),detail.DateDisponibiliteMarchandise);
  Assert.Equal("M1",detail.Magasin);
  Assert.Equal("D1",detail.CodeBureauDouane);
  Assert.Equal("Colis vérifiés",detail.ConstatDepotageBooking);
 }
 public class BookingRepositoryProxy : DispatchProxy
 {
  public Booking Booking {get;set;}=null!;
  protected override object? Invoke(MethodInfo? method,object?[]? args) => method?.Name=="FindAsync" ? Task.FromResult<Booking?>(Booking) : throw new NotSupportedException();
 }
}
