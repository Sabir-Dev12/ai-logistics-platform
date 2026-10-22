using Logistics.Application.Abstractions;
using Logistics.Application.Features.Resources;
using Logistics.Domain.Common;
using Logistics.Domain.Entities.Resources;
namespace Logistics.UnitTests;
public sealed class EquipmentApiContractTests
{
    private static ResourceService Service(Materiel? row=null,Action<Materiel>? add=null,Action? save=null)=>new(
        PaymentApiContractTests.Proxy.Make<IResourceRepository>((m,a)=>m.Name switch {
            "FindMaterielAsync"=>Task.FromResult(row),"Add"=>Add((Materiel)a![0]!,add),
            "SaveChangesAsync"=>Save(save),_=>throw new NotSupportedException(m.Name)}),
        PaymentApiContractTests.Proxy.Make<IKeyGenerator>((m,a)=>Task.FromResult("M1")),
        PaymentApiContractTests.Proxy.Make<IAuditTrail>((m,a)=>Task.CompletedTask),
        PaymentApiContractTests.Proxy.Make<ITenantContext>((m,a)=>m.Name=="get_SocieteKey"?"S1":null));
    private static object? Add(Materiel row,Action<Materiel>? add){add?.Invoke(row);return null;}
    private static Task Save(Action? save){save?.Invoke();return Task.CompletedTask;}
    [Fact]
    public async Task Creation_persists_all_editable_fields_in_one_save_and_returns_them()
    {
        Materiel? created=null;var saves=0;
        var dto=await Service(add:r=>created=r,save:()=>saves++).CreateMaterielAsync(new("AA-001","TRACTEUR","Diesel","Tracteur",null,"Loué",new(2026,1,1),new(2026,2,1),new(2028,1,1)));
        Assert.NotNull(created);Assert.Equal(1,saves);Assert.Equal("Loué",dto.MoyenPropre);Assert.Equal(new DateTime(2028,1,1),dto.DateFinGarantie);
        Assert.Equal(new DateTime(2026,1,1),created.DateAcquisition);Assert.Equal("Disponible",dto.Statut);
    }
    [Fact]
    public async Task Update_returns_ownership_and_warranty_without_losing_monthly_cost()
    {
        var row=new Materiel("M1","S1","AA-001","TRACTEUR");row.SetCout(12000,"MAD");
        var dto=await Service(row).UpdateMaterielAsync("M1",new("AA-002","Tracteur","TRACTEUR","Diesel","Propre",new(2026,1,1),new(2026,2,1),new(2028,1,1)));
        Assert.Equal("Propre",dto.MoyenPropre);Assert.Equal(new DateTime(2028,1,1),dto.DateFinGarantie);Assert.Equal(12000,dto.Cout);
    }
    [Fact]
    public async Task Invalid_type_is_rejected_without_saving()
    {
        var row=new Materiel("M1","S1","AA-001","TRACTEUR");var saves=0;
        await Assert.ThrowsAsync<BusinessRuleException>(()=>Service(row,save:()=>saves++).UpdateMaterielAsync("M1",new("AA-002",null," ",null,null,null,null,null)));
        Assert.Equal(0,saves);Assert.Equal("AA-001",row.Immatriculation);
    }
    [Fact]
    public async Task Invalid_disposal_preserves_status_and_does_not_save()
    {
        var row=new Materiel("M1","S1","AA-001","TRACTEUR");row.SetDates(new(2026,1,1),null,null);var saves=0;var service=Service(row,save:()=>saves++);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.SortirDuParcAsync("M1",new(new(2025,1,1),"Vente",10,0)));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.SortirDuParcAsync("M1",new(new(2026,10,18),"Vente",-1,0)));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.SortirDuParcAsync("M1",new(new(2026,10,18),"Vente",10,-1)));
        Assert.Equal(0,saves);Assert.Null(row.DateSortie);Assert.Equal(StatutMateriel.Disponible,row.Statut);
    }
    [Fact]
    public async Task Disposal_preserves_cost_and_prevents_repair_and_second_disposal()
    {
        var row=new Materiel("M1","S1","AA-001","TRACTEUR");row.SetCout(100,"MAD");var service=Service(row);
        var dto=await service.SortirDuParcAsync("M1",new(new(2026,10,18),"Vente",0,0));
        Assert.Equal(100,dto.Cout);Assert.Equal(0,dto.MontantVente);Assert.True(dto.IsRetire);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.EntrerReparationAsync("M1"));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.SortirDuParcAsync("M1",new(new(2026,10,19),null,null,null)));
    }
}
