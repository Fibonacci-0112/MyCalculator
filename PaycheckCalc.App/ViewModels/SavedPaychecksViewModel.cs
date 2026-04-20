using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaycheckCalc.App.Helpers;
using PaycheckCalc.App.Mappers;
using PaycheckCalc.App.Services;
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
    private readonly ComparisonSession _comparisonSession;

    public SavedPaychecksViewModel(
        IPaycheckRepository repo,
        CalculatorViewModel calculatorVm,
        ComparisonSession comparisonSession)
    {
        _repo = repo;
        _calculatorVm = calculatorVm;
        _comparisonSession = comparisonSession;
    }

    public ObservableCollection<SavedPaycheckViewModel> SavedPaychecks { get; } = new();

    [ObservableProperty] public partial bool IsEmpty { get; set; } = true;

    // ── Multi-scenario selection mode ────────────────────────────
    /// <summary>
    /// When true, the page shows a checkbox per item, the action buttons
    /// hide, and the user can select several saved paychecks to compare
    /// side-by-side on the Compare page.
    /// </summary>
    [ObservableProperty] public partial bool IsSelectionMode { get; set; }

    /// <summary>Count of currently selected saved paychecks.</summary>
    [ObservableProperty] public partial int SelectedCount { get; set; }

    /// <summary>True when enough items are selected to run a comparison.</summary>
    public bool CanCompareSelected => SelectedCount >= 2;

    partial void OnIsSelectionModeChanged(bool value)
    {
        // Leaving selection mode: clear all check marks so the row buttons
        // return to their normal "action buttons" state.
        if (!value)
        {
            foreach (var p in SavedPaychecks)
                p.IsSelected = false;
            SelectedCount = 0;
        }
        OnPropertyChanged(nameof(ShowActionButtons));
    }

    partial void OnSelectedCountChanged(int value)
        => OnPropertyChanged(nameof(CanCompareSelected));

    /// <summary>
    /// Inverse of <see cref="IsSelectionMode"/> for XAML binding: the
    /// per-row action buttons (Load / Compare / CSV / PDF / Rename / Delete)
    /// are visible only outside selection mode.
    /// </summary>
    public bool ShowActionButtons => !IsSelectionMode;

    [RelayCommand]
    public async Task LoadListAsync()
    {
        var all = await _repo.GetAllAsync();
        SavedPaychecks.Clear();
        foreach (var item in all.OrderByDescending(p => p.UpdatedAt))
        {
            var vm = SavedPaycheckMapper.MapToListItem(item);
            vm.SelectionChanged = OnSelectionChanged;
            SavedPaychecks.Add(vm);
        }
        IsEmpty = SavedPaychecks.Count == 0;
        SelectedCount = 0;
    }

    private void OnSelectionChanged()
    {
        SelectedCount = SavedPaychecks.Count(p => p.IsSelected);
    }

    [RelayCommand]
    public void ToggleSelectionMode()
        => IsSelectionMode = !IsSelectionMode;

    /// <summary>
    /// Publishes the selected saved paychecks to the shared
    /// <see cref="ComparisonSession"/> so the Compare page can render them
    /// side-by-side. No-op if fewer than two are selected.
    /// </summary>
    [RelayCommand]
    public async Task CompareSelectedAsync()
    {
        if (!CanCompareSelected) return;

        var selectedIds = SavedPaychecks
            .Where(p => p.IsSelected)
            .Select(p => p.Id)
            .ToList();

        var all = await _repo.GetAllAsync();
        var byId = all.ToDictionary(p => p.Id);

        var snapshots = selectedIds
            .Where(byId.ContainsKey)
            .Select(id => SavedPaycheckMapper.MapToScenarioSnapshot(byId[id]))
            .ToList();

        _comparisonSession.SetScenarios(snapshots);

        // Leave selection mode now that the compare set is published.
        IsSelectionMode = false;
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

        // 1-vs-1 (Saved vs Current) mode takes over; clear any stale
        // multi-scenario set from the session so the Compare page does not
        // show both views at once.
        _comparisonSession.Clear();
    }
}

