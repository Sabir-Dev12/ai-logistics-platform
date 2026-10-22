using System.Reflection;
using Logistics.Application.Features.Quotations;
using Logistics.Domain.Entities.Quotations;
namespace Logistics.UnitTests;
public sealed class QuotationEditContractTests
{
 [Fact]
 public void Detail_preserves_every_editable_header_field_for_a_safe_put()
 {
  var now=new DateTime(2026,10,1);var q=new Cotation("Q1","S1","DEV1","RI","C1",now);
  q.SetEntete("DEV1","Groupage","Pièces","Mode","Traitement","Spot","Hebdomadaire","48h","FR","Note","Prix");
  q.SetValidite(now.AddMonths(1).AddHours(16),now.AddDays(4).AddHours(11),null);
  var dto=Map(q);var e=Assert.IsType<UpdateCotationRequest>(dto.EditableFields);
  Assert.Equal(q.NumCotation,e.NumCotation);Assert.Equal(q.TypeTransport,e.TypeTransport);
  Assert.Equal(q.NatureMarchandise,e.NatureMarchandise);Assert.Equal(q.Mode,e.Mode);
  Assert.Equal(q.ModeTraitement,e.ModeTraitement);Assert.Equal(q.TypeTarif,e.TypeTarif);
  Assert.Equal(q.FrequenceDepart,e.FrequenceDepart);Assert.Equal(q.TransitTime,e.TransitTime);
  Assert.Equal(q.LangueClient,e.LangueClient);Assert.Equal(q.CommentaireCotation,e.Commentaire);
  Assert.Equal(q.CommentairePrix,e.CommentairePrix);Assert.Equal(q.DateValidite,e.DateValidite);
  Assert.Equal(q.DateLimiteEnlevement,e.DateLimiteEnlevement);
 }
 [Fact]
 public void Purchase_justification_reference_survives_the_detail_projection()
 {
  var q=new Cotation("Q1","S1","DEV1","RI","C1",new DateTime(2026,10,1));
  var achat=new AchatCotation("A1","Q1");achat.SetContenu("FR","S1","Fret",10,"MAD","F1","Fournisseur",null,"archive/achat.pdf","Preuve");
  q.AddAchat(achat);
  Assert.Equal("archive/achat.pdf",Assert.Single(Map(q).Achats).CheminJustif);
 }
 private static CotationDto Map(Cotation q)=>(CotationDto)typeof(QuotationService).GetMethod("Map",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new object[]{q,"MAD"})!;
}
