using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PaycheckCalc.Blazor.Data.Entities;
using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Blazor.Data;

/// <summary>
/// EF Core database context for PaycheckCalc. Combines ASP.NET Core
/// Identity (users, roles, external logins, tokens) with our four
/// per-user tables: paychecks, annual scenarios, session state,
/// preferences.
///
/// Deeply-nested DTOs (<see cref="PaycheckInput"/>, <see cref="PaycheckResult"/>,
/// <see cref="TaxYearProfile"/>, <see cref="AnnualTaxResult"/>) are persisted
/// as TEXT JSON columns via value converters. The serializer options
/// match those used by <c>LocalStoragePaycheckRepository</c> and
/// <c>JsonPaycheckRepository</c> (camelCase + JsonStringEnumConverter),
/// so the existing JSON-shape tests in <c>SavedPaycheckTest</c> and
/// <c>SavedAnnualScenarioJsonTest</c> continue to pin the wire format.
/// </summary>
public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<StoredPaycheck> Paychecks => Set<StoredPaycheck>();
    public DbSet<StoredAnnualScenario> AnnualScenarios => Set<StoredAnnualScenario>();
    public DbSet<UserSessionStateEntity> SessionStates => Set<UserSessionStateEntity>();
    public DbSet<UserPreferencesEntity> Preferences => Set<UserPreferencesEntity>();

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        var inputConverter = new ValueConverter<PaycheckInput, string>(
            v => JsonSerializer.Serialize(v, JsonOptions),
            v => JsonSerializer.Deserialize<PaycheckInput>(v, JsonOptions)!);

        var resultConverter = new ValueConverter<PaycheckResult, string>(
            v => JsonSerializer.Serialize(v, JsonOptions),
            v => JsonSerializer.Deserialize<PaycheckResult>(v, JsonOptions)!);

        var profileConverter = new ValueConverter<TaxYearProfile, string>(
            v => JsonSerializer.Serialize(v, JsonOptions),
            v => JsonSerializer.Deserialize<TaxYearProfile>(v, JsonOptions)!);

        var annualResultConverter = new ValueConverter<AnnualTaxResult?, string?>(
            v => v == null ? null : JsonSerializer.Serialize(v, JsonOptions),
            v => v == null ? null : JsonSerializer.Deserialize<AnnualTaxResult>(v, JsonOptions));

        builder.Entity<StoredPaycheck>(b =>
        {
            b.HasKey(p => p.Id);
            b.HasIndex(p => new { p.UserId, p.UpdatedAt });
            b.Property(p => p.UserId).IsRequired();
            b.Property(p => p.Name).IsRequired().HasMaxLength(200);
            b.Property(p => p.NetPay).HasPrecision(18, 2);
            b.Property(p => p.StateCode).HasMaxLength(8);
            b.Property(p => p.Input).HasConversion(inputConverter).IsRequired();
            b.Property(p => p.Result).HasConversion(resultConverter).IsRequired();
            b.HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<StoredAnnualScenario>(b =>
        {
            b.HasKey(s => s.Id);
            b.HasIndex(s => new { s.UserId, s.UpdatedAt });
            b.Property(s => s.UserId).IsRequired();
            b.Property(s => s.Name).IsRequired().HasMaxLength(200);
            b.Property(s => s.Profile).HasConversion(profileConverter).IsRequired();
            b.Property(s => s.Result).HasConversion(annualResultConverter!);
            b.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserSessionStateEntity>(b =>
        {
            b.HasKey(s => s.UserId);
            b.HasOne(s => s.User)
                .WithOne()
                .HasForeignKey<UserSessionStateEntity>(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserPreferencesEntity>(b =>
        {
            b.HasKey(p => p.UserId);
            b.Property(p => p.DefaultOvertimeMultiplier).HasPrecision(5, 3);
            b.HasOne(p => p.User)
                .WithOne()
                .HasForeignKey<UserPreferencesEntity>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
