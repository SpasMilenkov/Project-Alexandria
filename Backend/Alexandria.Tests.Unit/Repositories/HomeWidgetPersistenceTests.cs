using Alexandria.Data.Context;
using Alexandria.Data.Migrations;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Alexandria.Tests.Unit.Repositories;

public class HomeWidgetPersistenceTests
{
    private static AlexandriaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AlexandriaDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=postgres;Password=postgres")
            .Options;

        return new AlexandriaDbContext(options);
    }

    [Fact]
    public void AddHomeWidgets_creates_only_widget_storage_and_matches_the_current_model()
    {
        var migration = new AddHomeWidgets();
        var table = migration.UpOperations.OfType<CreateTableOperation>().Should().ContainSingle().Which;

        table.Name.Should().Be("HomeWidgets");
        table.Columns.Single(column => column.Name == "Options").ColumnType.Should().Be("jsonb");
        table.Columns.Single(column => column.Name == "ShowTitle").IsNullable.Should().BeTrue();
        table.Columns.Should().NotContain(column => column.Name == "Size");

        var owner = table.ForeignKeys.Should().ContainSingle().Which;
        owner.PrincipalTable.Should().Be("AspNetUsers");
        owner.OnDelete.Should().Be(Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Cascade);

        migration.UpOperations.Should().HaveCount(2);
        var index = migration.UpOperations.OfType<CreateIndexOperation>().Should().ContainSingle().Which;
        index.Columns.Should().Equal("UserId", "Layout");

        migration.DownOperations.Should().ContainSingle()
            .Which.Should().BeOfType<DropTableOperation>().Which.Name.Should().Be("HomeWidgets");

        using var context = CreateContext();

        context.Database.HasPendingModelChanges().Should().BeFalse();
    }
}
