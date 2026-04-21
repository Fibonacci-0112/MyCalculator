# Architecture

PaycheckCalc follows a clean separation between UI and business logic. The MAUI app uses MVVM with CommunityToolkit.Mvvm; the Blazor Server head uses Razor components. Both front-ends delegate to the same UI-agnostic `PaycheckCalc.Core` library.

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
│   ├── Services/                  # AnnualTaxSession, ComparisonSession, geocoding/jurisdiction
│   ├── Storage/                   # JsonPaycheckRepository, JsonAnnualScenarioRepository
│   └── MauiProgram.cs             # DI configuration & app startup
├── PaycheckCalc.Blazor/           # Blazor Web App (Server rendering) web head
│   ├── Components/                # Razor components and pages (Inputs, Results)
│   ├── Services/                  # CalculatorSessionState (scoped per circuit)
│   ├── wwwroot/                   # Static assets; tax JSON content-linked from Core/Data
│   └── Program.cs                 # Web host + DI wiring
├── PaycheckCalc.Core/             # Business logic (no UI dependencies)
│   ├── Models/                    # Domain models and enums
│   ├── Pay/                       # PayCalculator, AnnualProjectionCalculator
│   ├── Export/                    # CSV and PDF exporters
│   ├── Geocoding/                 # Address and geocoding abstractions
│   ├── Storage/                   # IPaycheckRepository, IAnnualScenarioRepository
│   ├── Data/                      # JSON tax bracket tables (state + local + federal)
│   └── Tax/
│       ├── Federal/               # IRS 15-T percentage calculator
│       │   └── Annual/            # Form 1040, 1040-ES, Schedule 1, CTC, 8863/8880/8960, withholding suggestion
│       ├── Fica/                  # Social Security & Medicare
│       ├── State/                 # State tax interfaces, registry, generic percentage-method adapter, annual state tax
│       ├── Local/                 # Local (city/county/school district) plugin model + registry
│       │   ├── Maryland/          # County surtax
│       │   ├── NewYork/           # NYC withholding
│       │   ├── Ohio/              # RITA + CCA
│       │   └── Pennsylvania/      # Act 32 EIT + LST
│       ├── SelfEmployment/        # SE tax, QBI deduction
│       └── <StateName>/           # State-specific calculators (AL, AR, CA, CO, CT, DE, GA, IL, OK, PA)
└── PaycheckCalc.Tests/            # xUnit test suite
```

---

## Key Architectural Principles

1. **Core stays UI-agnostic.** `PaycheckCalc.Core` has zero dependency on MAUI, XAML, Blazor, or any UI framework. All tax math, models, and export logic live here.

2. **PayCalculator is an orchestrator.** It composes gross pay, deductions, FICA, federal withholding, state withholding, local withholding, and net pay — but does not contain state- or locality-specific branching or tax rules.

3. **State and local calculators are plugins.** Each state implements `IStateWithholdingCalculator` and is registered in `StateCalculatorRegistry`; each locality implements `ILocalWithholdingCalculator` and is registered in `LocalCalculatorRegistry`. Both registries are built at startup (`MauiProgram.cs` for MAUI, `Program.cs` for Blazor).

4. **Schema-driven state UI.** State-specific input fields (filing status, allowances, dependents, etc.) are not hard-coded in XAML or Razor. Instead, each state calculator declares its input schema via `GetInputSchema()`, and the UI renders fields dynamically.

5. **`decimal` everywhere for money.** All monetary values, tax rates, thresholds, and deductions use `decimal`. No `double` or `float` in calculation code.

---

## MVVM Data Flow (MAUI)

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
| `CompareViewModel` | Drives the Compare page; falls back to saved-vs-current and also consumes the multi-scenario `ComparisonSession`. |
| `SavedPaychecksViewModel` | Manages the list of persisted paycheck entries (load, rename, delete) and pushes scenarios into the comparison. |
| `SelfEmploymentViewModel` | Owns input state and results for the SE tax estimator. |
| `AnnualTaxViewModel`, `AnnualProjectionViewModel`, `JobsAndYtdViewModel`, `OtherIncomeAdjustmentsViewModel`, `CreditsViewModel`, `QuarterlyEstimatesViewModel`, `WhatIfViewModel` | Phase 8 flyout VMs that share the singleton `AnnualTaxSession` to collect Form 1040 inputs and view the annual result. |
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
| `AnnualTaxInputMapper` | `AnnualTaxSession` → `TaxYearProfile` |

---

## Blazor Server Data Flow

```
Razor Component (Inputs.razor)
    ↓
CalculatorSessionState  ←  scoped-per-circuit PaycheckInput / PaycheckResult
    ↓
PayCalculator.Calculate(input)
    ↓
Results.razor reads the stored result and renders Razor markup
```

`CalculatorSessionState` is registered as a scoped service so each interactive circuit has its own in-memory inputs/results without page parameters.

---

## Dependency Injection

Core services are registered in [`MauiProgram.cs`](../../PaycheckCalc.App/MauiProgram.cs) and mirrored in [`PaycheckCalc.Blazor/Program.cs`](../../PaycheckCalc.Blazor/Program.cs).

### Core Services

- `FicaCalculator` — Social Security and Medicare computation.
- `Irs15TPercentageCalculator` — Federal income tax (loaded from JSON at startup).
- `StateCalculatorRegistry` — Central registry of all 51 state calculators.
- `LocalCalculatorRegistry` — Registry of local (sub-state) calculators (PA EIT + LST, NYC, OH RITA/CCA, MD county surtax).
- `PayCalculator` — Main paycheck calculation orchestrator; consumes both state and local registries.
- `AnnualProjectionCalculator` — Year-to-date and projection logic.
- `IPaycheckRepository` → `JsonPaycheckRepository` — Local JSON persistence (MAUI).

### Self-Employment Services

- `SelfEmploymentTaxCalculator` — Schedule SE computation.
- `QbiDeductionCalculator` — Section 199A QBI deduction.
- `SelfEmploymentCalculator` — SE orchestrator (peer to `PayCalculator`).

### Annual (Form 1040) Services

- `Federal1040TaxCalculator` — 2026 Rev. Proc. 2025-32 brackets, JSON-backed.
- `Schedule1Calculator` — Schedule 1 adjustments.
- `AnnualStateTaxCalculator` — annual state tax over the `StateCalculatorRegistry`.
- `Form1040Calculator` — full-year federal orchestrator.
- `Form1040ESCalculator` — quarterly estimated tax payments.
- `WithholdingSuggestionCalculator` — per-period W-4 Step 4(c) suggestion.
- `IAnnualScenarioRepository` → `JsonAnnualScenarioRepository` — persists Phase 8 annual scenarios.
- `AnnualTaxSession` — singleton shared across Phase 8 flyout view models.

### Geocoding & Jurisdiction (MAUI only)

- `IAddressService`, `IGeocodingService`, `IGeocodingCache`, `IJurisdictionService`, `IGoogleMapsApiKeyProvider` — optional Google Maps–backed address lookup and jurisdiction resolution.

### JSON-Backed Calculators

Several calculators load their tax tables from JSON at startup:

- `ArkansasFormulaCalculator` ← `ar_withholding_2026.json`
- `OklahomaOw2PercentageCalculator` ← `ok_ow2_2026_percentage.json`
- `CaliforniaPercentageCalculator` ← `ca_method_b_2026.json`
- `ColoradoWithholdingCalculator` ← `co_dr0004_2026.json`
- `ConnecticutWithholdingCalculator` ← `connecticut_withholding_2026.json`
- `Irs15TPercentageCalculator` ← `us_irs_15t_2026_percentage_automated.json`
- `Federal1040TaxCalculator` ← `Federal2026/federal_1040_brackets_2026.json`
- `PaEitCalculator` ← `pa_eit_2026.json`
- `NycWithholdingCalculator` ← `nyc_withholding_2026.json`
- `OhRitaCalculator` ← `oh_rita_2026.json`
- `OhCcaCalculator` ← `oh_cca_2026.json`
- `MdCountyCalculator` ← `md_county_surtax_2026.json`

### Pages and View Models

All pages and view models are registered as singletons in MAUI and resolved via Shell `DataTemplate` bindings. The Blazor head uses Razor components directly and does not need page-level DI registration.

---

## Persistence

Saved paychecks and saved annual scenarios both use repository patterns:

- **`IPaycheckRepository`** (Core) — defines load/save/delete/rename operations for paycheck scenarios.
- **`JsonPaycheckRepository`** (App) — stores `saved_paychecks.json` in `FileSystem.AppDataDirectory`.
- **`SavedPaycheck`** — wraps `PaycheckInput` + `PaycheckResult` with an ID, display name, and timestamps.
- **`IAnnualScenarioRepository`** (Core) + **`JsonAnnualScenarioRepository`** (App) — stores `saved_annual_scenarios.json` for the Phase 8 annual flow.

The `CalculatorViewModel` tracks a `LoadedPaycheckId` so re-saving an already-loaded paycheck overwrites the existing entry rather than creating a duplicate.

---

## Class Diagram

A full Mermaid UML class diagram is available at [`docs/class-diagram.md`](../class-diagram.md).
