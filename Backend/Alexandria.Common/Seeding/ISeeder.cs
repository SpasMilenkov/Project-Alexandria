namespace Alexandria.Common.Seeding;

public interface ISeeder
{
    Task SeedAsync(CancellationToken ct = default);
}