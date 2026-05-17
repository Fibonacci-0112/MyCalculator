using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PaycheckCalc.Blazor.Auth;
using PaycheckCalc.Blazor.Data;
using PaycheckCalc.Blazor.Services;
using PaycheckCalc.Core.Models;
using Xunit;

namespace PaycheckCalc.Tests;

public sealed class EfSessionStateRepositoryTest : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private const string PrimaryUserId = "user-1";
    private const string OtherUserId = "user-2";

    public EfSessionStateRepositoryTest()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new AppDbContext(_options);
        db.Database.EnsureCreated();
        db.Users.Add(new ApplicationUser { Id = PrimaryUserId, UserName = "u1@test", Email = "u1@test" });
        db.Users.Add(new ApplicationUser { Id = OtherUserId, UserName = "u2@test", Email = "u2@test" });
        db.SaveChanges();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenNoSnapshot()
    {
        var snapshot = await CreateRepo(PrimaryUserId).GetAsync();
        Assert.Null(snapshot);
    }

    [Fact]
    public async Task SaveAsync_Inserts_FirstTime()
    {
        var updatedAt = new DateTimeOffset(2026, 5, 17, 12, 0, 0, TimeSpan.Zero);
        await CreateRepo(PrimaryUserId).SaveAsync(new SessionStateSnapshot(
            CalculatorState: "{\"hourlyRate\":25}",
            SelfEmploymentState: null,
            AnnualTaxState: "{\"taxYear\":2026}",
            UpdatedAt: updatedAt));

        var loaded = await CreateRepo(PrimaryUserId).GetAsync();
        Assert.NotNull(loaded);
        Assert.Equal("{\"hourlyRate\":25}", loaded!.CalculatorState);
        Assert.Null(loaded.SelfEmploymentState);
        Assert.Equal("{\"taxYear\":2026}", loaded.AnnualTaxState);
        Assert.Equal(updatedAt, loaded.UpdatedAt);
    }

    [Fact]
    public async Task SaveAsync_Upserts_OnSecondCall()
    {
        await CreateRepo(PrimaryUserId).SaveAsync(new SessionStateSnapshot(
            "v1", null, null, DateTimeOffset.UtcNow));

        await CreateRepo(PrimaryUserId).SaveAsync(new SessionStateSnapshot(
            "v2", "se", "annual", DateTimeOffset.UtcNow));

        var loaded = await CreateRepo(PrimaryUserId).GetAsync();
        Assert.NotNull(loaded);
        Assert.Equal("v2", loaded!.CalculatorState);
        Assert.Equal("se", loaded.SelfEmploymentState);
        Assert.Equal("annual", loaded.AnnualTaxState);
    }

    [Fact]
    public async Task GetAsync_DoesNotLeakAcrossUsers()
    {
        await CreateRepo(PrimaryUserId).SaveAsync(new SessionStateSnapshot(
            "primary-state", null, null, DateTimeOffset.UtcNow));

        var otherSeen = await CreateRepo(OtherUserId).GetAsync();
        Assert.Null(otherSeen);
    }

    private EfSessionStateRepository CreateRepo(string? userId)
    {
        var db = new AppDbContext(_options);
        return new EfSessionStateRepository(db, new TestUserContext(userId));
    }

    private sealed class TestUserContext : IUserContext
    {
        private readonly string? _userId;
        public TestUserContext(string? userId) { _userId = userId; }
        public Task<string?> GetUserIdAsync() => Task.FromResult(_userId);
    }
}
