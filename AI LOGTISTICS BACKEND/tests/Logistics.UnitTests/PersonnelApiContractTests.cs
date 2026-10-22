using Logistics.Application.Abstractions;
using Logistics.Application.Features.Resources;
using Logistics.Domain.Common;
using Logistics.Domain.Entities.Resources;
namespace Logistics.UnitTests;
public sealed class PersonnelApiContractTests
{
    private static Personnel Employee()
    {
        var p = new Personnel("P1", "S1", "Ben Ali", "Nora Marie");
        p.SetIdentite("M1", "$CHAUFFEUR", "SERV", "DEP", "Ben Ali", "Nora Marie", new(1990, 1, 2), "Mariée");
        p.SetCoordonnees("Rue 1", "Etage 2", "20000", "Casablanca", "MA", "0600", "0500", "a@example.test", "AG1");
        p.SetEmbauche(new(2020, 1, 1));p.SetCout(100, "MAD");
        return p;
    }
    private static ResourceService Service(Personnel p, Action? save = null) => new(
        PaymentApiContractTests.Proxy.Make<IResourceRepository>((m,a)=>m.Name switch {
            "FindPersonnelAsync"=>Task.FromResult<Personnel?>(p),
            "SaveChangesAsync"=>Save(save), _=>throw new NotSupportedException(m.Name)}),
        PaymentApiContractTests.Proxy.Make<IKeyGenerator>((m,a)=>Task.FromResult("P2")),
        PaymentApiContractTests.Proxy.Make<IAuditTrail>((m,a)=>Task.CompletedTask),
        PaymentApiContractTests.Proxy.Make<ITenantContext>((m,a)=>m.Name=="get_SocieteKey"?"S1":null));
    private static Task Save(Action? save){save?.Invoke();return Task.CompletedTask;}
    [Fact]
    public async Task Update_preserves_unexposed_identity_and_coordinates_and_returns_separate_names()
    {
        var p=Employee();var dto=await Service(p).UpdatePersonnelAsync("P1",new("M2","F2","S2","DEP","Ben Ali","Nora Marie","0611","b@example.test",new(2020,1,1)));
        Assert.Equal("Ben Ali",dto.Nom);Assert.Equal("Nora Marie",dto.Prenom);Assert.Equal("DEP",dto.KeyDepartement);
        Assert.Equal("Casablanca",p.VillePersonnel);Assert.Equal("AG1",p.AgencePersonnel);
        Assert.Equal("Rue 1",p.Adresse1Personnel);Assert.Equal("Etage 2",p.Adresse2Personnel);
        Assert.Equal("MA",p.PaysPersonnel);Assert.Equal("20000",p.CpPersonnel);Assert.Equal("0500",p.FixePersonnel);
        Assert.Equal(new DateTime(1990,1,2),p.DateNaissance);Assert.Equal("Mariée",p.SituationFamiliale);
        Assert.Equal("0611",p.GsmPersonnel);Assert.Equal(100,p.Cout);
    }
    [Fact]
    public async Task Update_changes_city_and_agency_without_clearing_other_coordinates()
    {
        var p=Employee();await Service(p).UpdatePersonnelAsync("P1",new("M1","F1","S1","DEP","Ali","Nora","0600",null,null,"Rabat","AG2"));
        Assert.Equal("Rabat",p.VillePersonnel);Assert.Equal("AG2",p.AgencePersonnel);Assert.Equal("Rue 1",p.Adresse1Personnel);
    }
    [Fact]
    public async Task Second_departure_cannot_overwrite_history()
    {
        var p=Employee();p.EnregistrerSortie(new(2026,10,1),"Fin de contrat");var saves=0;
        await Assert.ThrowsAsync<BusinessRuleException>(()=>Service(p,()=>saves++).EnregistrerSortieAsync("P1",new(new(2026,10,18),"Autre")));
        Assert.Equal(0,saves);Assert.Equal("Fin de contrat",p.MotifSortie);Assert.Equal(100,p.Cout);
    }
    [Fact]
    public async Task Empty_name_is_rejected_before_persistence()
    {
        var p=Employee();var saves=0;await Assert.ThrowsAsync<BusinessRuleException>(()=>Service(p,()=>saves++).UpdatePersonnelAsync("P1",new(null,null,null,null," ",null,null,null,null)));
        Assert.Equal("Ben Ali",p.NomPersonnel);Assert.Equal(0,saves);
    }
}
