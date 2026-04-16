using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Storage;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.State;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Tests for <see cref="IPaycheckRepository"/> implementations.
/// Uses a file-based approach against a temp directory to verify CRUD
/// operations and persistence across load/save cycles.
/// </summary>
public sealed class JsonPaycheckRepositoryTest : IDisposable
{
    private readonly string _tempDir;

    public JsonPaycheckRepositoryTest()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"PaycheckCalcTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── GetAllAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_EmptyWhenNoFile()
    {
        var repo = CreateRepo();
        var all = await repo.GetAllAsync();
        Assert.Empty(all);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllSaved()
    {
        var repo = CreateRepo();
        await repo.SaveAsync(CreateSample("A"));
        await repo.SaveAsync(CreateSample("B"));
        await repo.SaveAsync(CreateSample("C"));

        var all = await repo.GetAllAsync();
        Assert.Equal(3, all.Count);
    }

    // ── SaveAsync ───────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_PersistsToFile()
    {
        var repo = CreateRepo();
        var paycheck = CreateSample("Persisted");
        await repo.SaveAsync(paycheck);

        // Create a new repo instance to verify persistence
        var repo2 = CreateRepo();
        var all = await repo2.GetAllAsync();
        Assert.Single(all);
        Assert.Equal("Persisted", all[0].Name);
    }

    [Fact]
    public async Task SaveAsync_Upserts_ExistingById()
    {
        var repo = CreateRepo();
        var paycheck = CreateSample("Original");
        await repo.SaveAsync(paycheck);

        // Update name and re-save with same ID
        paycheck.Name = "Updated";
        paycheck.UpdatedAt = DateTimeOffset.UtcNow;
        await repo.SaveAsync(paycheck);

        var all = await repo.GetAllAsync();
        Assert.Single(all);
        Assert.Equal("Updated", all[0].Name);
    }

    [Fact]
    public async Task SaveAsync_PreservesInputAndResult()
    {
        var repo = CreateRepo();
        var paycheck = CreateSample("Full Data");
        await repo.SaveAsync(paycheck);

        var repo2 = CreateRepo();
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
        var repo = CreateRepo();
        var paycheck = CreateSample("W4 Test");
        await repo.SaveAsync(paycheck);

        var repo2 = CreateRepo();
        var loaded = (await repo2.GetAllAsync())[0];

        Assert.Equal(paycheck.Input.FederalW4.FilingStatus, loaded.Input.FederalW4.FilingStatus);
        Assert.Equal(paycheck.Input.FederalW4.Step2Checked, loaded.Input.FederalW4.Step2Checked);
        Assert.Equal(paycheck.Input.FederalW4.Step3TaxCredits, loaded.Input.FederalW4.Step3TaxCredits);
    }

    [Fact]
    public async Task SaveAsync_PreservesDeductions()
    {
        var repo = CreateRepo();
        var paycheck = CreateSample("Deduction Test");
        await repo.SaveAsync(paycheck);

        var repo2 = CreateRepo();
        var loaded = (await repo2.GetAllAsync())[0];

        Assert.Equal(paycheck.Input.Deductions.Count, loaded.Input.Deductions.Count);
        Assert.Equal("401k", loaded.Input.Deductions[0].Name);
        Assert.Equal(DeductionType.PreTax, loaded.Input.Deductions[0].Type);
    }

    // ── GetByIdAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var repo = CreateRepo();
        var result = await repo.GetByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_FindsSavedPaycheck()
    {
        var repo = CreateRepo();
        var paycheck = CreateSample("Findable");
        await repo.SaveAsync(paycheck);

        var found = await repo.GetByIdAsync(paycheck.Id);
        Assert.NotNull(found);
        Assert.Equal("Findable", found!.Name);
    }

    // ── DeleteAsync ─────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_RemovesPaycheck()
    {
        var repo = CreateRepo();
        var paycheck = CreateSample("To Delete");
        await repo.SaveAsync(paycheck);

        await repo.DeleteAsync(paycheck.Id);

        var all = await repo.GetAllAsync();
        Assert.Empty(all);
    }

    [Fact]
    public async Task DeleteAsync_PersistsRemoval()
    {
        var repo = CreateRepo();
        var paycheck = CreateSample("To Delete");
        await repo.SaveAsync(paycheck);
        await repo.DeleteAsync(paycheck.Id);

        // Verify with fresh instance
        var repo2 = CreateRepo();
        var all = await repo2.GetAllAsync();
        Assert.Empty(all);
    }

    [Fact]
    public async Task DeleteAsync_NoOp_WhenIdNotFound()
    {
        var repo = CreateRepo();
        var paycheck = CreateSample("Keep");
        await repo.SaveAsync(paycheck);

        await repo.DeleteAsync(Guid.NewGuid()); // nonexistent ID

        var all = await repo.GetAllAsync();
        Assert.Single(all);
        Assert.Equal("Keep", all[0].Name);
    }

    [Fact]
    public async Task DeleteAsync_OnlyRemovesTarget()
    {
        var repo = CreateRepo();
        var a = CreateSample("A");
        var b = CreateSample("B");
        var c = CreateSample("C");
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

    // ── Corrupted file handling ─────────────────────────────────

    [Fact]
    public async Task GetAllAsync_HandlesCorruptedFile()
    {
        var filePath = Path.Combine(_tempDir, "saved_paychecks.json");
        await File.WriteAllTextAsync(filePath, "NOT VALID JSON{{{");

        var repo = CreateRepo();
        var all = await repo.GetAllAsync();
        Assert.Empty(all); // Graceful degradation — empty list
    }

    // ── Helper ───────────────────────────────────────────────────

    /// <summary>
    /// Creates a repository backed by the shared <c>_tempDir</c>.
    /// Uses the same serializer options as production code.
    /// </summary>
    private IPaycheckRepository CreateRepo() => new FilePaycheckRepository(_tempDir);

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
}

/// <summary>
/// Testable JSON-file-backed implementation of <see cref="IPaycheckRepository"/>
/// that mirrors <c>JsonPaycheckRepository</c> in the App project without
/// depending on MAUI's <c>FileSystem</c>.
/// </summary>
internal sealed class FilePaycheckRepository : IPaycheckRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<SavedPaycheck>? _cache;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public FilePaycheckRepository(string dataDirectory)
    {
        _filePath = Path.Combine(dataDirectory, "saved_paychecks.json");
    }

    public async Task<IReadOnlyList<SavedPaycheck>> GetAllAsync()
    {
        await EnsureLoadedAsync();
        return _cache!.AsReadOnly();
    }

    public async Task<SavedPaycheck?> GetByIdAsync(Guid id)
    {
        await EnsureLoadedAsync();
        return _cache!.FirstOrDefault(p => p.Id == id);
    }

    public async Task SaveAsync(SavedPaycheck paycheck)
    {
        await _lock.WaitAsync();
        try
        {
            await EnsureLoadedAsync();
            var index = _cache!.FindIndex(p => p.Id == paycheck.Id);
            if (index >= 0)
                _cache[index] = paycheck;
            else
                _cache.Add(paycheck);
            await PersistAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        await _lock.WaitAsync();
        try
        {
            await EnsureLoadedAsync();
            _cache!.RemoveAll(p => p.Id == id);
            await PersistAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task EnsureLoadedAsync()
    {
        if (_cache is not null) return;
        if (File.Exists(_filePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_filePath);
                _cache = JsonSerializer.Deserialize<List<SavedPaycheck>>(json, JsonOptions) ?? [];
            }
            catch (JsonException)
            {
                _cache = [];
            }
        }
        else
        {
            _cache = [];
        }
    }

    private async Task PersistAsync()
    {
        var json = JsonSerializer.Serialize(_cache, JsonOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }
}
