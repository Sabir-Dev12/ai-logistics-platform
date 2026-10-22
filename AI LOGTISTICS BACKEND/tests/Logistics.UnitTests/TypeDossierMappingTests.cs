using Logistics.Domain.Entities.ReferenceData;
using Logistics.Domain.Enums;
using Logistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Logistics.UnitTests;

public sealed class TypeDossierMappingTests
{
    [Theory]
    [InlineData("I", Sens.Import)]
    [InlineData("E", Sens.Export)]
    [InlineData("N", Sens.National)]
    [InlineData("Import", Sens.Import)]
    [InlineData("Export", Sens.Export)]
    [InlineData("National", Sens.National)]
    [InlineData(" import ", Sens.Import)]
    public void Direction_accepts_seed_codes_and_legacy_labels(string stored, Sens expected)
    {
        using var context = Context();
        var converter = context.Model.FindEntityType(typeof(TypeDossier))!.FindProperty(nameof(TypeDossier.Sens))!.GetValueConverter()!;
        Assert.Equal(expected, converter.ConvertFromProvider(stored));
        Assert.Equal(expected.ToString(), converter.ConvertToProvider(expected));
        Assert.Null(converter.ConvertFromProvider(null));
    }

    [Fact]
    public void Unknown_direction_is_not_assumed_to_be_import()
    {
        using var context = Context();
        var converter = context.Model.FindEntityType(typeof(TypeDossier))!.FindProperty(nameof(TypeDossier.Sens))!.GetValueConverter()!;
        Assert.Throws<InvalidOperationException>(() => converter.ConvertFromProvider("Unknown"));
    }

    private static LogisticsDbContext Context() => new(new DbContextOptionsBuilder<LogisticsDbContext>()
        .UseSqlServer("Server=unused;Database=unused;Integrated Security=true").Options);
}
