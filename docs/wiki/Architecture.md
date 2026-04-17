# Architecture

PaycheckCalc follows a clean separation between UI and business logic using the MVVM pattern with .NET MAUI.

---

## Solution Structure

```
PaycheckCalc.slnx
├── PaycheckCalc.App/              # .NET MAUI frontend (Android & Windows)
│   ├── Views/                     # XAML pages
│   ├── ViewModels/                # MVVM view models
│   ├── Mappers/                   # Domain ↔ UI translation
│   ├── Models/                    # UI presentation models
│   ├── Controls/                  # Custom controls (e.g., DoughnutChartDrawable)
│   ├── Behaviors/                 # Input formatting behaviors
│   ├── Helpers/                   # Enum display helpers
│   ├── Storage/                   # JsonPaycheckRepository (local persistence)
│   └── MauiProgram.cs             # DI configuration & app startup
├── PaycheckCalc.Core/             # Business logic (no UI dependencies)
│   ├── Models/                    # Domain models and enums
│   ├── Pay/                       # PayCalculator, AnnualProjectionCalculator
│   ├── Export/                    # CSV and PDF exporters
│   ├── Storage/                   # IPaycheckRepository interface
│   ├── Data/                      # JSON tax bracket tables
│   └── Tax/
│       ├── Federal/               # IRS 15-T percentage calculator
│       ├── Fica/                  # Social Security & Medicare
│       ├── State/                 # State tax interfaces, registry, generic calculator
│       ├── SelfEmployment/        # SE tax, QBI deduction
│       └── <StateName>/           # State-specific calculators
└── PaycheckCalc.Tests/            # xUnit test suite
```

---

## Key Architectural Principles

1. **Core stays UI-agnostic.** `PaycheckCalc.Core` has zero dependency on MAUI, XAML, or any UI framework. All tax math, models, and export logic live here.

2. **PayCalculator is an orchestrator.** It composes gross pay, deductions, FICA, federal withholding, state withholding, and net pay — but does not contain state-specific branching or tax rules.

3. **State calculators are plugins.** Each state implements `IStateWithholdingCalculator` and is registered in `StateCalculatorRegistry`. The registry is built at startup in `MauiProgram.cs`.

4. **Schema-driven state UI.** State-specific input fields (filing status, allowances, dependents, etc.) are not hard-coded in XAML. Instead, each state calculator declares its input schema via `GetInputSchema()`, and the UI renders fields dynamically.

5. **`decimal` everywhere for money.** All monetary values, tax rates, thresholds, and deductions use `decimal`. No `double` or `float` in calculation code.

---

## MVVM Data Flow

```
User Input (XAML Pages)
    ↓
CalculatorViewModel  ←  builds PaycheckInput from UI state
    ↓
PayCalculator.Calculate(input)  →  PaycheckResult
    ↓
ResultCardMapper  →  ResultCardModel (UI presentation)
    ↓
ResultsPage (XAML binding)
```

### View Models

| View Model | Responsibility |
|---|---|
| `CalculatorViewModel` | Central VM shared across input/result pages. Owns all input state, triggers calculation, maps results. |
| `SavedPaychecksViewModel` | Manages the list of persisted paycheck entries (load, rename, delete). |
| `SelfEmploymentViewModel` | Owns input state and results for the SE tax estimator. |
| `StateFieldViewModel` | Wraps a single `StateFieldDefinition` for dynamic state input rendering. |
| `DeductionItemViewModel` | Wraps a single deduction entry for the deductions list. |

### Mappers

Mappers translate between domain models and UI presentation models:

| Mapper | From → To |
|---|---|
| `ResultCardMapper` | `PaycheckResult` → `ResultCardModel` |
| `AnnualProjectionMapper` | `AnnualProjection` → `AnnualProjectionModel` |
| `ScenarioMapper` | `CalculationScenario` → `ScenarioSnapshot` |
| `PaycheckInputMapper` | UI state → `PaycheckInput` |
| `PaycheckInputRestorer` | `PaycheckInput` → UI state (reload saved paychecks) |
| `SavedPaycheckMapper` | `SavedPaycheck` → UI display model |
| `SelfEmploymentInputMapper` | UI state → `SelfEmploymentInput` |
| `SelfEmploymentResultMapper` | `SelfEmploymentResult` → UI display model |

---

## Dependency Injection

All services are registered as singletons in [`MauiProgram.cs`](../../PaycheckCalc.App/MauiProgram.cs):

### Core Services

- `FicaCalculator` — Social Security and Medicare computation.
- `Irs15TPercentageCalculator` — Federal income tax (loaded from JSON at startup).
- `StateCalculatorRegistry` — Central registry of all 51 state calculators.
- `PayCalculator` — Main paycheck calculation orchestrator.
- `AnnualProjectionCalculator` — Year-to-date and projection logic.
- `IPaycheckRepository` → `JsonPaycheckRepository` — Local JSON persistence.

### Self-Employment Services

- `SelfEmploymentTaxCalculator` — Schedule SE computation.
- `QbiDeductionCalculator` — Section 199A QBI deduction.
- `SelfEmploymentCalculator` — SE orchestrator (peer to `PayCalculator`).

### JSON-Backed Calculators

Several state calculators load their tax tables from JSON at startup:

- `ArkansasFormulaCalculator` ← `ar_withholding_2026.json`
- `OklahomaOw2PercentageCalculator` ← `ok_ow2_2026_percentage.json`
- `CaliforniaPercentageCalculator` ← `ca_method_b_2026.json`
- `ColoradoWithholdingCalculator` ← `co_dr0004_2026.json`
- `ConnecticutWithholdingCalculator` ← `connecticut_withholding_2026.json`

### Pages and View Models

All pages and view models are registered as singletons and resolved via constructor injection through Shell `DataTemplate` bindings.

---

## Persistence

Saved paychecks use a repository pattern:

- **`IPaycheckRepository`** (Core) — defines load/save/delete/rename operations.
- **`JsonPaycheckRepository`** (App) — stores `saved_paychecks.json` in `FileSystem.AppDataDirectory`.
- **`SavedPaycheck`** — wraps `PaycheckInput` + `PaycheckResult` with an ID, display name, and timestamps.

The `CalculatorViewModel` tracks a `LoadedPaycheckId` so re-saving an already-loaded paycheck overwrites the existing entry rather than creating a duplicate.

---

## Class Diagram

A full Mermaid UML class diagram is available at [`docs/class-diagram.md`](../class-diagram.md).
