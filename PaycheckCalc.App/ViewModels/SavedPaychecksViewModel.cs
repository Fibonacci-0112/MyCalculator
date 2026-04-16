using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaycheckCalc.App.Helpers;
using PaycheckCalc.App.Mappers;
using PaycheckCalc.Core.Export;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Storage;
using System.Collections.ObjectModel;

namespace PaycheckCalc.App.ViewModels;

/// <summary>
/// ViewModel for the Saved Paychecks page.
/// Manages the collection of persisted paychecks and provides commands
/// for load, rename, delete, export, and compare operations.
/// </summary>
public partial class SavedPaychecksViewModel : ObservableObject
{
    private readonly IPaycheckRepository _repo;
    private readonly CalculatorViewModel _calculatorVm;

    public SavedPaychecksViewModel(IPaycheckRepository repo, CalculatorViewModel calculatorVm)
    {
        _repo = repo;
        _calculatorVm = calculatorVm;
    }

    public ObservableCollection<SavedPaycheckViewModel> SavedPaychecks { get; } = new();

    [ObservableProperty] public partial bool IsEmpty { get; set; } = true;

    [RelayCommand]
    public async Task LoadListAsync()
    {
        var all = await _repo.GetAllAsync();
        SavedPaychecks.Clear();
        foreach (var item in all.OrderByDescending(p => p.UpdatedAt))
            SavedPaychecks.Add(SavedPaycheckMapper.MapToListItem(item));
        IsEmpty = SavedPaychecks.Count == 0;
    }

    [RelayCommand]
    private async Task DeleteAsync(Guid id)
    {
        await _repo.DeleteAsync(id);

        // If the deleted paycheck was the one currently loaded, clear the tracking ID
        if (_calculatorVm.LoadedPaycheckId == id)
            _calculatorVm.LoadedPaycheckId = null;

        await LoadListAsync();
    }

    [RelayCommand]
    private async Task RenameAsync(SavedPaycheckViewModel item)
    {
        // The actual prompt is handled by the page code-behind (DisplayPromptAsync),
        // which calls this after obtaining the new name.
        // This is a no-op placeholder; see RenameWithNameAsync.
    }

    /// <summary>
    /// Renames a saved paycheck. Called by the page after a DisplayPromptAsync dialog.
    /// </summary>
    public async Task RenameWithNameAsync(Guid id, string newName)
    {
        var paycheck = await _repo.GetByIdAsync(id);
        if (paycheck is null) return;

        paycheck.Name = newName;
        paycheck.UpdatedAt = DateTimeOffset.UtcNow;
        await _repo.SaveAsync(paycheck);
        await LoadListAsync();
    }

    /// <summary>
    /// Loads a saved paycheck into the calculator, restoring all inputs and results.
    /// </summary>
    public async Task LoadIntoCalculatorAsync(Guid id)
    {
        var paycheck = await _repo.GetByIdAsync(id);
        if (paycheck is null) return;

        PaycheckInputRestorer.Restore(_calculatorVm, paycheck.Input);
        _calculatorVm.LoadedPaycheckId = paycheck.Id;
        _calculatorVm.LoadedPaycheckName = paycheck.Name;

        // Recalculate to regenerate the result card and projections
        _calculatorVm.CalculateCommand.Execute(null);
    }

    /// <summary>
    /// Exports a saved paycheck as CSV via the platform share sheet.
    /// </summary>
    public async Task ExportCsvAsync(Guid id)
    {
        var paycheck = await _repo.GetByIdAsync(id);
        if (paycheck is null) return;

        var csv = CsvPaycheckExporter.Generate(paycheck.Result);
        var fileName = $"paycheck_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
        await File.WriteAllTextAsync(filePath, csv);

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Export Paycheck CSV",
            File = new ShareFile(filePath)
        });
    }

    /// <summary>
    /// Exports a saved paycheck as PDF via the platform share sheet.
    /// </summary>
    public async Task ExportPdfAsync(Guid id)
    {
        var paycheck = await _repo.GetByIdAsync(id);
        if (paycheck is null) return;

        var pdf = PdfPaycheckExporter.Generate(paycheck.Result);
        var fileName = $"paycheck_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
        var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
        await File.WriteAllBytesAsync(filePath, pdf);

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Export Paycheck PDF",
            File = new ShareFile(filePath)
        });
    }

    /// <summary>
    /// Sets a saved paycheck as the comparison scenario on the calculator.
    /// </summary>
    public async Task SetAsComparisonAsync(Guid id)
    {
        var paycheck = await _repo.GetByIdAsync(id);
        if (paycheck is null) return;

        _calculatorVm.SavedScenario = SavedPaycheckMapper.MapToScenarioSnapshot(paycheck);
    }
}
