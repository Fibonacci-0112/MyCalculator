# PaycheckCalc repository instructions for GitHub Copilot

This repository is a cross-platform .NET MAUI paycheck calculator with a strict separation between UI and payroll logic.

## Repository shape

- `PaycheckCalc.App` is the MAUI front end for Android and Windows.
- `PaycheckCalc.Core` is the calculation engine and must stay free of MAUI/UI dependencies.
- `PaycheckCalc.Tests` is the xUnit regression suite and is part of the development workflow, not an afterthought.
- JSON tax tables in `PaycheckCalc.Core/Data` are source data for federal and state withholding logic.

## Architecture rules

- Keep calculation logic in `PaycheckCalc.Core`. Do not move tax math into XAML pages, code-behind, converters, or view models.
- Treat `PayCalculator` as the orchestration layer only. It should compose gross pay, deductions, FICA, federal withholding, state withholding, and net pay rather than absorb state-specific rules.
- Keep the existing domain → mapper → presentation flow. UI should consume `ResultCardModel`, not raw domain result types.
- Preserve the schema-driven state-tax architecture. State-specific UI must come from `IStateWithholdingCalculator.GetInputSchema()`, `StateFieldDefinition`, `StateInputValues`, and `StateFieldViewModel`.
- Do not hardcode per-state fields directly into `InputsPage.xaml` when the same outcome can be achieved by extending the state schema.
- Keep state registration centralized in `MauiProgram` through `StateCalculatorRegistry`.

## Money and tax logic rules

- Use `decimal` for all money, wages, taxes, rates, thresholds, and deduction values. Never introduce `double` or `float` into calculation code.
- Preserve the repo's current rounding behavior unless the user explicitly asks for a tax-law-backed change and corresponding tests.
- Prefer small, explicit calculation steps with comments when implementing tax logic. Readability matters because this code encodes legal/business rules.
- Do not “simplify” away state-specific branches, annualization rules, allowance handling, low-income exemptions, or per-period table logic.
- Keep federal withholding in `Irs15TPercentageCalculator`, FICA in `FicaCalculator`, and state-specific rules in their own state modules.
- Keep JSON-backed tax logic data-driven where the repository already uses JSON tables.
- If a tax rule appears odd, assume it may be intentional and inspect tests before changing it.

## State-tax extension rules

- New states or major state changes should fit the plugin model behind `IStateWithholdingCalculator`.
- If a state truly has unique inputs or formulas, add a dedicated module under `PaycheckCalc.Core/Tax/<StateName>` and expose a schema through the calculator.
- When adding or changing state input fields, make sure the schema, validation, UI field resolution, and tests all stay aligned.
- Keep supported-state coverage complete for all 50 states plus DC.

## Existing repository quirks that are intentional until replaced with verified fixes

- California uses Method B, has SDI, and currently includes a deliberate 3-cent single-status adjustment in `CaliforniaWithholdingCalculator`.
- Oklahoma uses OW-2 JSON tables and whole-dollar rounding behavior.
- Alabama withholding depends on annualized federal withholding and dependent deductions.
- No-income-tax states are implemented through `NoIncomeTaxWithholdingAdapter`.
- Some docs/comments may lag behind code; prefer the actual implementation and tests when they disagree.

## MAUI and MVVM rules

- Use CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`) consistently with the current codebase.
- Keep pages thin. View models own state and commands. Mappers translate between UI models and domain models.
- Follow the existing naming and folder conventions: `Views`, `ViewModels`, `Models`, `Mappers`, `Helpers`, `Controls`, `Behaviors`.
- Do not add business logic to code-behind unless it is strictly view-specific behavior.
- Do not reload static tax JSON on every calculation. Startup-time DI loading is the current pattern.

## Testing expectations

- Any non-trivial tax logic change should be accompanied by test updates or new tests.
- Prefer explicit expected dollar amounts in tests rather than reusing production logic to compute the assertion value.
- Add regression tests for edge cases such as threshold boundaries, allowance handling, extra withholding, pre-tax deductions, rounding boundaries, and state-specific quirks.
- Keep tests readable and scenario-based. This repository already uses descriptive payroll examples.

## Change safety rules

- Do not change target frameworks, preview package versions, tax data file names, or MAUI asset wiring unless the task explicitly calls for it.
- If you touch JSON schemas or asset file names, update all loaders, project files, and tests that depend on them.
- Prefer focused edits over broad refactors in tax code.
- Before proposing a “cleanup,” check whether the current shape exists to preserve tax accuracy, dynamic UI behavior, or test compatibility.
