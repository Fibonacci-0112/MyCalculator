using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PaycheckCalc.Blazor.Auth;
using PaycheckCalc.Blazor.Data;
using PaycheckCalc.Blazor.Services;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Storage;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.State;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Tests for <see cref="EfPaycheckRepository"/>. Mirrors the matrix of
/// <see cref="JsonPaycheckRepositoryTest"/> against an in-memory SQLite
/// database. The connection is kept open for the test lifetime — closing
/// it drops the database. Two seeded users let us also verify cross-user
/// isolation, which the JSON repo did not need.
/// </summary>
public sealed class EfPaycheckRepositoryTest : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private const string PrimaryUserId = "user-1";
    private const string OtherUserId = "user-2";

    public EfPaycheckRepositoryTest()
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

    // ── GetAllAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_EmptyWhenNoRows()
    {
        var repo = CreateRepo(PrimaryUserId);
        var all = await repo.GetAllAsync();
        Assert.Empty(all);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllSavedForCurrentUser()
    {
        var repo = CreateRepo(PrimaryUserId);
        await repo.SaveAsync(CreateSample("A"));
        await repo.SaveAsync(CreateSample("B"));
        await repo.SaveAsync(CreateSample("C"));

        var all = await repo.GetAllAsync();
        Assert.Equal(3, all.Count);
    }

    // ── SaveAsync ───────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_PersistsToDatabase()
    {
        var paycheck = CreateSample("Persisted");
        var repo = CreateRepo(PrimaryUserId);
        await repo.SaveAsync(paycheck);

        // Fresh repo and DbContext to verify the write hit storage, not just a cache.
        var repo2 = CreateRepo(PrimaryUserId);
        var all = await repo2.GetAllAsync();
        Assert.Single(all);
        Assert.Equal("Persisted", all[0].Name);
    }

    [Fact]
    public async Task SaveAsync_Upserts_ExistingById()
    {
        var paycheck = CreateSample("Original");
        var repo = CreateRepo(PrimaryUserId);
        await repo.SaveAsync(paycheck);

        paycheck.Name = "Updated";
        paycheck.UpdatedAt = DateTimeOffset.UtcNow;
        await repo.SaveAsync(paycheck);

        var repo2 = CreateRepo(PrimaryUserId);
        var all = await repo2.GetAllAsync();
        Assert.Single(all);
        Assert.Equal("Updated", all[0].Name);
    }

    [Fact]
    public async Task SaveAsync_PreservesInputAndResult()
    {
        var paycheck = CreateSample("Full Data");
        var repo = CreateRepo(PrimaryUserId);
        await repo.SaveAsync(paycheck);

        var repo2 = CreateRepo(PrimaryUserId);
        var loaded = (await repo2.GetAllAsync())[0];

        Assert.Equal(paycheck.Input.Frequency, loaded.Input.Frequency);
        Assert.Equal(paycheck.Input.HourlyRate, loaded.Input.HourlyRate);
        Assert.Equal(paycheck.Input.RegularHours, loaded.Input.RegularHours);
        Assert.Equal(paycheck.Input.State, loaded.Input.State);
        Assert.Equal(paycheck.Result.GrossPay, loaded.Result.GrossPay);
        Assert.Equal(paycheck.Result.NetPay, loaded.Result.NetPay);
        Assert.Equal(paycheck.Result.FederalWithholding, loaded.Result.FederalWithholding);
    }

    [Fact]
    public async Task SaveAsync_PreservesFederalW4()
    {
        var paycheck = CreateSample("W4 Test");
        var repo = CreateRepo(PrimaryUserId);
        await repo.SaveAsync(paycheck);

        var repo2 = CreateRepo(PrimaryUserId);
        var loaded = (await repo2.GetAllAsync())[0];

        Assert.Equal(paycheck.Input.FederalW4.FilingStatus, loaded.Input.FederalW4.FilingStatus);
        Assert.Equal(paycheck.Input.FederalW4.Step2Checked, loaded.Input.FederalW4.Step2Checked);
        Assert.Equal(paycheck.Input.FederalW4.Step3TaxCredits, loaded.Input.FederalW4.Step3TaxCredits);
    }

    [Fact]
    public async Task SaveAsync_PreservesDeductions()
    {
        var paycheck = CreateSample("Deduction Test");
        var repo = CreateRepo(PrimaryUserId);
        await repo.SaveAsync(paycheck);

        var repo2 = CreateRepo(PrimaryUserId);
        var loaded = (await repo2.GetAllAsync())[0];

        Assert.Equal(paycheck.Input.Deductions.Count, loaded.Input.Deductions.Count);
        Assert.Equal("401k", loaded.Input.Deductions[0].Name);
        Assert.Equal(DeductionType.PreTax, loaded.Input.Deductions[0].Type);
    }

    [Fact]
    public async Task SaveAsync_PromotesNetPayAndStateCode()
    {
        var paycheck = CreateSample("Promoted");
        var repo = CreateRepo(PrimaryUserId);
        await repo.SaveAsync(paycheck);

        using var db = new AppDbContext(_options);
        var entity = await db.Paychecks.FirstAsync(p => p.Id == paycheck.Id);
        Assert.Equal(paycheck.Result.NetPay, entity.NetPay);
        Assert.Equal(paycheck.Result.State.ToString(), entity.StateCode);
    }

    // ── GetByIdAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var repo = CreateRepo(PrimaryUserId);
        var result = await repo.GetByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_FindsSavedPaycheck()
    {
        var paycheck = CreateSample("Findable");
        var repo = CreateRepo(PrimaryUserId);
        await repo.SaveAsync(paycheck);

        var repo2 = CreateRepo(PrimaryUserId);
        var found = await repo2.GetByIdAsync(paycheck.Id);
        Assert.NotNull(found);
        Assert.Equal("Findable", found!.Name);
    }

    // ── DeleteAsync ─────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_RemovesPaycheck()
    {
        var paycheck = CreateSample("To Delete");
        var repo = CreateRepo(PrimaryUserId);
        await repo.SaveAsync(paycheck);

        await repo.DeleteAsync(paycheck.Id);

        var all = await repo.GetAllAsync();
        Assert.Empty(all);
    }

    [Fact]
    public async Task DeleteAsync_NoOp_WhenIdNotFound()
    {
        var paycheck = CreateSample("Keep");
        var repo = CreateRepo(PrimaryUserId);
        await repo.SaveAsync(paycheck);

        await repo.DeleteAsync(Guid.NewGuid());

        var all = await repo.GetAllAsync();
        Assert.Single(all);
        Assert.Equal("Keep", all[0].Name);
    }

    [Fact]
    public async Task DeleteAsync_OnlyRemovesTarget()
    {
        var a = CreateSample("A");
        var b = CreateSample("B");
        var c = CreateSample("C");
        var repo = CreateRepo(PrimaryUserId);
        await repo.SaveAsync(a);
        await repo.SaveAsync(b);
        await repo.SaveAsync(c);

        await repo.DeleteAsync(b.Id);

        var all = await repo.GetAllAsync();
        Assert.Equal(2, all.Count);
        Assert.DoesNotContain(all, p => p.Name == "B");
        Assert.Contains(all, p => p.Name == "A");
        Assert.Contains(all, p => p.Name == "C");
    }

    // ── Multi-user isolation — the most important new property ─────────

    [Fact]
    public async Task GetAllAsync_OnlyReturnsCurrentUsersRows()
    {
        var primaryRepo = CreateRepo(PrimaryUserId);
        var otherRepo = CreateRepo(OtherUserId);

        await primaryRepo.SaveAsync(CreateSample("primary-A"));
        await primaryRepo.SaveAsync(CreateSample("primary-B"));
        await otherRepo.SaveAsync(CreateSample("other-A"));

        var primarySeen = await CreateRepo(PrimaryUserId).GetAllAsync();
        var otherSeen = await CreateRepo(OtherUserId).GetAllAsync();

        Assert.Equal(2, primarySeen.Count);
        Assert.All(primarySeen, p => Assert.StartsWith("primary-", p.Name));
        Assert.Single(otherSeen);
        Assert.Equal("other-A", otherSeen[0].Name);
    }

    [Fact]
    public async Task GetByIdAsync_DoesNotLeakAcrossUsers()
    {
        var paycheck = CreateSample("Owned by primary");
        await CreateRepo(PrimaryUserId).SaveAsync(paycheck);

        var leaked = await CreateRepo(OtherUserId).GetByIdAsync(paycheck.Id);
        Assert.Null(leaked);
    }

    [Fact]
    public async Task DeleteAsync_DoesNotDeleteOtherUsersRow()
    {
        var paycheck = CreateSample("Owned by primary");
        await CreateRepo(PrimaryUserId).SaveAsync(paycheck);

        await CreateRepo(OtherUserId).DeleteAsync(paycheck.Id);

        var stillThere = await CreateRepo(PrimaryUserId).GetByIdAsync(paycheck.Id);
        Assert.NotNull(stillThere);
    }

    // ── Anonymous user is read-only-empty, write-throws ─────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsEmpty_WhenAnonymous()
    {
        // Anonymous users see an empty list rather than an exception so the
        // Home / Saved Paychecks pages render gracefully when logged out.
        var repo = CreateRepo(userId: null);
        var all = await repo.GetAllAsync();
        Assert.Empty(all);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenAnonymous()
    {
        var repo = CreateRepo(userId: null);
        var found = await repo.GetByIdAsync(Guid.NewGuid());
        Assert.Null(found);
    }

    [Fact]
    public async Task SaveAsync_Throws_WhenAnonymous()
    {
        // Writes still require auth — Save Paycheck in the UI must be gated.
        var repo = CreateRepo(userId: null);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repo.SaveAsync(CreateSample("anon")));
    }

    [Fact]
    public async Task DeleteAsync_Throws_WhenAnonymous()
    {
        var repo = CreateRepo(userId: null);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repo.DeleteAsync(Guid.NewGuid()));
    }

    // ── Helpers ────────────────────────────────────────────────

    private EfPaycheckRepository CreateRepo(string? userId)
    {
        var db = new AppDbContext(_options);
        return new EfPaycheckRepository(db, new TestUserContext(userId));
    }

    private static SavedPaycheck CreateSample(string name) => new()
    {
        Name = name,
        Input = new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            HourlyRate = 25m,
            RegularHours = 80m,
            OvertimeHours = 5m,
            OvertimeMultiplier = 1.5m,
            State = UsState.OK,
            PaycheckNumber = 1,
            FederalW4 = new FederalW4Input
            {
                FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
                Step2Checked = true,
                Step3TaxCredits = 2000m
            },
            Deductions =
            [
                new Deduction { Name = "401k", Type = DeductionType.PreTax, Amount = 200m }
            ]
        },
        Result = new PaycheckResult
        {
            GrossPay = 2187.50m,
            PreTaxDeductions = 200m,
            PostTaxDeductions = 0m,
            State = UsState.OK,
            StateTaxableWages = 1987.50m,
            StateWithholding = 75.00m,
            SocialSecurityWithholding = 135.63m,
            MedicareWithholding = 31.72m,
            FederalTaxableIncome = 1987.50m,
            FederalWithholding = 100.00m,
            NetPay = 1545.15m
        }
    };

    private sealed class TestUserContext : IUserContext
    {
        private readonly string? _userId;
        public TestUserContext(string? userId) { _userId = userId; }
        public Task<string?> GetUserIdAsync() => Task.FromResult(_userId);
    }
}
