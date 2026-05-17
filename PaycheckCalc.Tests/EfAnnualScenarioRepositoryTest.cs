using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PaycheckCalc.Blazor.Auth;
using PaycheckCalc.Blazor.Data;
using PaycheckCalc.Blazor.Services;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.State;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Tests for <see cref="EfAnnualScenarioRepository"/>. Mirrors
/// <see cref="EfPaycheckRepositoryTest"/>'s shape, including the multi-user
/// isolation checks.
/// </summary>
public sealed class EfAnnualScenarioRepositoryTest : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private const string PrimaryUserId = "user-1";
    private const string OtherUserId = "user-2";

    public EfAnnualScenarioRepositoryTest()
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

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task GetAllAsync_EmptyWhenNoRows()
    {
        var repo = CreateRepo(PrimaryUserId);
        var all = await repo.GetAllAsync();
        Assert.Empty(all);
    }

    [Fact]
    public async Task SaveAsync_PersistsScenario()
    {
        var scenario = CreateSample("Baseline 2026");
        var repo = CreateRepo(PrimaryUserId);
        await repo.SaveAsync(scenario);

        var repo2 = CreateRepo(PrimaryUserId);
        var all = await repo2.GetAllAsync();
        Assert.Single(all);
        Assert.Equal("Baseline 2026", all[0].Name);
    }

    [Fact]
    public async Task SaveAsync_PreservesProfileFields()
    {
        var scenario = CreateSample("Full Profile");
        var repo = CreateRepo(PrimaryUserId);
        await repo.SaveAsync(scenario);

        var loaded = (await CreateRepo(PrimaryUserId).GetAllAsync())[0];

        Assert.Equal(scenario.Profile.TaxYear, loaded.Profile.TaxYear);
        Assert.Equal(scenario.Profile.FilingStatus, loaded.Profile.FilingStatus);
        Assert.Equal(scenario.Profile.ResidenceState, loaded.Profile.ResidenceState);
        Assert.Equal(scenario.Profile.QualifyingChildren, loaded.Profile.QualifyingChildren);
        Assert.Single(loaded.Profile.W2Jobs);
        Assert.Equal(120_000m, loaded.Profile.W2Jobs[0].WagesBox1);
        Assert.Equal(W2JobHolder.Taxpayer, loaded.Profile.W2Jobs[0].Holder);
        Assert.Equal(250m, loaded.Profile.OtherIncome.TaxableInterest);
        Assert.Equal(3_000m, loaded.Profile.Adjustments.HsaDeduction);
    }

    [Fact]
    public async Task SaveAsync_Upserts_ExistingById()
    {
        var scenario = CreateSample("Original");
        var repo = CreateRepo(PrimaryUserId);
        await repo.SaveAsync(scenario);

        var updated = new SavedAnnualScenario
        {
            Id = scenario.Id,
            Name = "Updated",
            CreatedAt = scenario.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow,
            Profile = scenario.Profile
        };
        await repo.SaveAsync(updated);

        var all = await CreateRepo(PrimaryUserId).GetAllAsync();
        Assert.Single(all);
        Assert.Equal("Updated", all[0].Name);
    }

    [Fact]
    public async Task GetByIdAsync_FindsScenario()
    {
        var scenario = CreateSample("Findable");
        await CreateRepo(PrimaryUserId).SaveAsync(scenario);

        var found = await CreateRepo(PrimaryUserId).GetByIdAsync(scenario.Id);
        Assert.NotNull(found);
        Assert.Equal("Findable", found!.Name);
    }

    [Fact]
    public async Task DeleteAsync_RemovesScenario()
    {
        var scenario = CreateSample("To Delete");
        var repo = CreateRepo(PrimaryUserId);
        await repo.SaveAsync(scenario);
        await repo.DeleteAsync(scenario.Id);

        var all = await CreateRepo(PrimaryUserId).GetAllAsync();
        Assert.Empty(all);
    }

    // ── Multi-user isolation ────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_OnlyReturnsCurrentUsersRows()
    {
        await CreateRepo(PrimaryUserId).SaveAsync(CreateSample("primary-A"));
        await CreateRepo(OtherUserId).SaveAsync(CreateSample("other-A"));

        var primarySeen = await CreateRepo(PrimaryUserId).GetAllAsync();
        var otherSeen = await CreateRepo(OtherUserId).GetAllAsync();

        Assert.Single(primarySeen);
        Assert.Equal("primary-A", primarySeen[0].Name);
        Assert.Single(otherSeen);
        Assert.Equal("other-A", otherSeen[0].Name);
    }

    [Fact]
    public async Task GetByIdAsync_DoesNotLeakAcrossUsers()
    {
        var scenario = CreateSample("Owned by primary");
        await CreateRepo(PrimaryUserId).SaveAsync(scenario);

        var leaked = await CreateRepo(OtherUserId).GetByIdAsync(scenario.Id);
        Assert.Null(leaked);
    }

    [Fact]
    public async Task DeleteAsync_DoesNotDeleteOtherUsersScenario()
    {
        var scenario = CreateSample("Owned by primary");
        await CreateRepo(PrimaryUserId).SaveAsync(scenario);

        await CreateRepo(OtherUserId).DeleteAsync(scenario.Id);

        var stillThere = await CreateRepo(PrimaryUserId).GetByIdAsync(scenario.Id);
        Assert.NotNull(stillThere);
    }

    // ── Helpers ────────────────────────────────────────────────

    private EfAnnualScenarioRepository CreateRepo(string? userId)
    {
        var db = new AppDbContext(_options);
        return new EfAnnualScenarioRepository(db, new TestUserContext(userId));
    }

    private static SavedAnnualScenario CreateSample(string name) => new()
    {
        Name = name,
        Profile = new TaxYearProfile
        {
            TaxYear = 2026,
            FilingStatus = FederalFilingStatus.MarriedFilingJointly,
            ResidenceState = UsState.CO,
            QualifyingChildren = 2,
            W2Jobs = new[]
            {
                new W2JobInput
                {
                    Name = "Employer A",
                    WagesBox1 = 120_000m,
                    FederalWithholdingBox2 = 15_000m,
                    SocialSecurityWagesBox3 = 120_000m,
                    SocialSecurityTaxBox4 = 7_440m,
                    MedicareWagesBox5 = 120_000m,
                    MedicareTaxBox6 = 1_740m,
                    StateWagesBox16 = 120_000m,
                    StateWithholdingBox17 = 5_000m,
                    Holder = W2JobHolder.Taxpayer
                }
            },
            OtherIncome = new OtherIncomeInput
            {
                TaxableInterest = 250m,
                OrdinaryDividends = 400m,
                QualifiedDividends = 350m
            },
            Adjustments = new AdjustmentsInput
            {
                StudentLoanInterest = 1_000m,
                HsaDeduction = 3_000m
            },
            EstimatedTaxPayments = 500m,
            AdditionalExpectedWithholding = 200m
        }
    };

    private sealed class TestUserContext : IUserContext
    {
        private readonly string? _userId;
        public TestUserContext(string? userId) { _userId = userId; }
        public Task<string?> GetUserIdAsync() => Task.FromResult(_userId);
    }
}
