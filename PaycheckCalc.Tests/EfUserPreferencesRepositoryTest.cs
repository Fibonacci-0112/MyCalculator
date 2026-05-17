using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PaycheckCalc.Blazor.Auth;
using PaycheckCalc.Blazor.Data;
using PaycheckCalc.Blazor.Services;
using PaycheckCalc.Core.Models;
using Xunit;

namespace PaycheckCalc.Tests;

public sealed class EfUserPreferencesRepositoryTest : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private const string PrimaryUserId = "user-1";
    private const string OtherUserId = "user-2";

    public EfUserPreferencesRepositoryTest()
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
    public async Task GetAsync_ReturnsNull_WhenNoPreferences()
    {
        var prefs = await CreateRepo(PrimaryUserId).GetAsync();
        Assert.Null(prefs);
    }

    [Fact]
    public async Task SaveAsync_Inserts_FirstTime()
    {
        var updatedAt = new DateTimeOffset(2026, 5, 17, 12, 0, 0, TimeSpan.Zero);
        await CreateRepo(PrimaryUserId).SaveAsync(new UserPreferences(
            DefaultState: "CA",
            DefaultFilingStatus: "MarriedFilingJointly",
            DefaultFrequency: "Biweekly",
            DefaultOvertimeMultiplier: 1.5m,
            UpdatedAt: updatedAt));

        var loaded = await CreateRepo(PrimaryUserId).GetAsync();
        Assert.NotNull(loaded);
        Assert.Equal("CA", loaded!.DefaultState);
        Assert.Equal("MarriedFilingJointly", loaded.DefaultFilingStatus);
        Assert.Equal("Biweekly", loaded.DefaultFrequency);
        Assert.Equal(1.5m, loaded.DefaultOvertimeMultiplier);
        Assert.Equal(updatedAt, loaded.UpdatedAt);
    }

    [Fact]
    public async Task SaveAsync_Upserts_OnSecondCall()
    {
        await CreateRepo(PrimaryUserId).SaveAsync(new UserPreferences(
            "CA", "Single", "Weekly", 1.5m, DateTimeOffset.UtcNow));

        await CreateRepo(PrimaryUserId).SaveAsync(new UserPreferences(
            "TX", "HeadOfHousehold", "Biweekly", 2.0m, DateTimeOffset.UtcNow));

        var loaded = await CreateRepo(PrimaryUserId).GetAsync();
        Assert.NotNull(loaded);
        Assert.Equal("TX", loaded!.DefaultState);
        Assert.Equal("HeadOfHousehold", loaded.DefaultFilingStatus);
        Assert.Equal("Biweekly", loaded.DefaultFrequency);
        Assert.Equal(2.0m, loaded.DefaultOvertimeMultiplier);
    }

    [Fact]
    public async Task GetAsync_DoesNotLeakAcrossUsers()
    {
        await CreateRepo(PrimaryUserId).SaveAsync(new UserPreferences(
            "CA", null, null, null, DateTimeOffset.UtcNow));

        var otherSeen = await CreateRepo(OtherUserId).GetAsync();
        Assert.Null(otherSeen);
    }

    private EfUserPreferencesRepository CreateRepo(string? userId)
    {
        var db = new AppDbContext(_options);
        return new EfUserPreferencesRepository(db, new TestUserContext(userId));
    }

    private sealed class TestUserContext : IUserContext
    {
        private readonly string? _userId;
        public TestUserContext(string? userId) { _userId = userId; }
        public Task<string?> GetUserIdAsync() => Task.FromResult(_userId);
    }
}
