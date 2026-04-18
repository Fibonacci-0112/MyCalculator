# PaycheckCalc

A cross-platform .NET MAUI paycheck calculator that computes net pay, tax withholdings, and deductions for all 50 US states plus DC using 2026 tax tables. Also includes self-employment (Schedule SE + QBI) and annual Form 1040 tax estimation modules.

## Features

- **Gross Pay Calculation** — Computes gross pay from hourly rate, regular hours, and overtime hours with a configurable overtime multiplier.
- **Federal Income Tax** — Implements the IRS Publication 15-T (2026) percentage method for automated payroll systems, supporting all W-4 inputs (filing status, Step 2 checkbox, Step 3 credits, Step 4 adjustments).
- **FICA Taxes** — Calculates Social Security (6.2%, capped at $184,500), Medicare (1.45%), and Additional Medicare (0.9% above $200,000).
- **State Income Tax** — Covers all 50 states and DC with state-specific calculators:
  - **9 no-income-tax states** — AK, FL, NV, NH, SD, TN, TX, WA, WY
  - **Flat-rate state** — Pennsylvania (3.07%)
  - **Custom formula states** — Alabama (graduated + dependents + federal deduction), Arkansas (DFA formula method), California (Method B, EDD DE 44 withholding tables + SDI), Colorado (flat 4.4% + DR 0004 Table 1 allowance + FMLI), Connecticut (TPG-211 table-driven withholding + PFMLI), Delaware (DE W-4, 7 graduated brackets + $110 personal credit), Illinois (flat 4.95% + IL-W-4 allowances), Oklahoma (OW-2)
  - **Percentage method states** — 33 states using an annualized graduated bracket approach with state-specific standard deductions, allowances, and tax brackets
- **State Disability / Family Leave Insurance** — California SDI and Connecticut PFMLI are computed alongside state withholding with dynamic labels on the results screen and exports.
- **Pre-Tax & Post-Tax Deductions** — Supports configurable deductions that reduce taxable wages.
- **Dynamic State Inputs** — Each state declares its own input schema (filing status, allowances, dependents, extra withholding), and the UI renders fields dynamically.
- **Annual Projection** — Estimates annualized gross, taxes, and net pay; tracks current paycheck number and remaining pay periods, and projects year-end over/under withholding.
- **Self-Employment Tax** — Schedule SE calculator with FICA coordination for W-2 wages (reduces remaining Social Security wage base and Additional Medicare threshold) plus Form 8995/8995-A QBI deduction.
- **Annual Form 1040 Estimation** — Orchestrates full-year federal tax estimates using 2026 Rev. Proc. 2025-32 brackets and standard deductions, Schedule 1 adjustments, Child Tax Credit, Form 8863 education credits, Form 8880 saver's credit, and Form 8960 Net Investment Income Tax.
- **Export Results** — Exports per-period paycheck results to CSV or PDF (powered by QuestPDF) and shares via the native OS share sheet.
- **Results Visualization** — Doughnut chart breakdown of gross pay by category (federal tax, state tax, Social Security, Medicare, net pay).
- **Side-by-Side Comparison** — Save a snapshot of one calculation and compare it against the current inputs.
- **Saved Paychecks** — Persist named paycheck calculations to local JSON storage, reload them into the calculator, and manage saved entries from a dedicated page.
- **Multiple Pay Frequencies** — Weekly, Bi-Weekly, Semi-Monthly, Monthly, Quarterly, Semi-Annual, Annual, and Daily.

## Project Structure

```
PaycheckCalc.slnx
├── PaycheckCalc.App/          # .NET MAUI frontend (Android & Windows)
│   ├── Views/                 # XAML pages (Inputs, Results, Compare, Saved Paychecks, Self-Employment, Annual Tax)
│   ├── ViewModels/            # MVVM view models (Calculator, SavedPaychecks, SelfEmployment, AnnualTax, StateField, DeductionItem)
│   ├── Mappers/               # Domain-to-UI mappers (ResultCard, AnnualProjection, Scenario, SavedPaycheck, PaycheckInput)
│   ├── Models/                # UI presentation models (ResultCardModel, AnnualProjectionModel, ScenarioSnapshot)
│   ├── Controls/              # Custom controls (DoughnutChartDrawable)
│   ├── Behaviors/             # Input formatting behaviors
│   ├── Helpers/               # Enum display helpers
│   ├── Storage/               # JsonPaycheckRepository (local JSON file persistence)
│   └── MauiProgram.cs         # DI configuration & app startup
├── PaycheckCalc.Core/         # Business logic (no UI dependencies)
│   ├── Models/                # PaycheckInput, PaycheckResult, Enums, Deduction, AnnualProjection, CalculationScenario, SavedPaycheck, SelfEmploymentInput, TaxYearProfile, AnnualTaxResult, W2JobInput
│   ├── Pay/                   # PayCalculator (main orchestrator), AnnualProjectionCalculator
│   ├── Export/                # CsvPaycheckExporter, PdfPaycheckExporter (QuestPDF)
│   ├── Storage/               # IPaycheckRepository interface
│   ├── Data/                  # JSON tax bracket tables (IRS 15-T, Federal 1040, OK OW-2, CA Method B, AR, CO DR 0004, CT TPG-211)
│   └── Tax/
│       ├── Federal/           # IRS 15-T percentage calculator
│       │   └── Annual/        # Form 1040 orchestrator, Federal 1040 brackets, Schedule 1, CTC, Form 8863/8880/8960
│       ├── Fica/              # Social Security & Medicare calculator
│       ├── SelfEmployment/    # Schedule SE tax & Form 8995/8995-A QBI deduction
│       ├── State/             # State tax interfaces, registry, and generic calculator
│       ├── Alabama/           # Alabama-specific formula calculator
│       ├── Arkansas/          # Arkansas DFA formula calculator
│       ├── California/        # California Method B calculator (+ SDI)
│       ├── Colorado/          # Colorado flat-rate + DR 0004 calculator (+ FMLI)
│       ├── Connecticut/       # Connecticut TPG-211 table-driven calculator (+ PFMLI)
│       ├── Delaware/          # Delaware DE W-4 graduated calculator (+ personal credit)
│       ├── Illinois/          # Illinois flat-rate + IL-W-4 allowance calculator
│       ├── Oklahoma/          # Oklahoma OW-2 percentage calculator
│       └── Pennsylvania/      # Pennsylvania flat-rate calculator
├── PaycheckCalc.Tests/        # xUnit test suite
├── PaycheckCalc.Web/          # Blazor WebAssembly head (browser-hosted)
│   ├── Pages/                 # Razor pages (Calculator, SavedPaychecks, About)
│   ├── Components/            # Reusable Razor components (SchemaFieldEditor, FederalW4Editor, DeductionsEditor, ResultsCard)
│   ├── Layout/                # MainLayout + NavMenu
│   ├── Services/              # HttpClientTaxDataAssetLoader + LocalStorage{Paycheck,AnnualScenario}Repository
│   ├── wwwroot/               # Static site (linked tax JSON tables under wwwroot/data/)
│   └── Program.cs             # WASM startup & DI configuration
└── docs/                      # Class diagrams and documentation
```

## Technology Stack

| Component | Technology |
|---|---|
| **Framework** | .NET 11 Preview / .NET MAUI (desktop & mobile) + Blazor WebAssembly (web) |
| **Target Platforms** | Android, Windows 10+, modern web browsers (WASM) |
| **UI Pattern** | MVVM with [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) |
| **Test Framework** | xUnit 2.9.3 |
| **PDF Export** | [QuestPDF](https://www.questpdf.com/) 2025.12.4 |
| **Tax Data** | JSON-based IRS 15-T and state tax bracket tables (2026) |

## Prerequisites

- [.NET 11 SDK](https://dotnet.microsoft.com/) (preview) — pinned in `global.json`
- .NET MAUI workload (for the App project):
  ```bash
  dotnet workload install maui
  ```

## Getting Started

### Build the Core Library

```bash
dotnet build PaycheckCalc.Core
```

### Run Tests

```bash
dotnet test PaycheckCalc.Tests
```

### Run the App

```bash
dotnet build PaycheckCalc.App
dotnet run --project PaycheckCalc.App
```

> **Note:** The MAUI app requires the `maui` workload and a supported target platform (Android emulator/device or Windows).

### Run the Web head locally

The repository also includes a Blazor WebAssembly head, **PaycheckCalc.Web**, that
runs entirely in the browser and reuses the same `PaycheckCalc.Core` engine.

```bash
dotnet run --project PaycheckCalc.Web
```

This serves the app at `http://localhost:5000` by default. No MAUI workload is
required for the Web head.

The Web head is automatically published to GitHub Pages on every push to `main`
by the [`Publish Web (GitHub Pages)`](.github/workflows/publish-web.yml) workflow.
After enabling Pages in the repository settings (Source: *GitHub Actions*), the
live site will be available at `https://<owner>.github.io/<repo>/`.

#### What's available on the Web today

- Single-paycheck calculation across all 50 states + DC, including the schema-driven
  state and locality input forms (CA Method B, OK OW-2, AR DFA, CO DR-0004, CT TPG-211,
  DE percentage method, AL annualized, IL flat, PA flat, plus PA EIT/LST, NYC, OH RITA/CCA,
  MD county surtax).
- Saved paychecks persisted to the browser's `localStorage` with **JSON
  export/import** that round-trips through the desktop and mobile heads
  (file name: `saved_paychecks.json`).

#### Not yet ported to the Web (use the desktop/mobile heads for these)

- Annual Form 1040 projection flow (Jobs & YTD, Other Income, Credits,
  Quarterly Estimates, What-If) — the Core engine is wired up in DI and ready
  for follow-up Razor pages.
- Self-Employment / Schedule SE / QBI flow — likewise.
- Side-by-side scenario comparison.
- Address-based locality lookup (geocoding).

## How It Works

The **PayCalculator** orchestrates the full paycheck calculation pipeline:

1. **Gross Pay** — `(Regular Hours × Hourly Rate) + (Overtime Hours × Hourly Rate × OT Multiplier)`
2. **Deductions** — Pre-tax deductions reduce taxable wages; post-tax deductions are subtracted after taxes.
3. **FICA** — Social Security and Medicare are calculated on gross wages minus applicable pre-tax deductions, with annual wage-base caps.
4. **Federal Withholding** — Wages are annualized, the standard deduction and W-4 adjustments are applied, the tax is computed using graduated brackets from IRS Publication 15-T, and the result is de-annualized back to the pay period.
5. **State Withholding** — The `StateCalculatorRegistry` looks up the registered `IStateWithholdingCalculator` for the selected state and delegates calculation using the state's specific rules and inputs.
6. **Net Pay** — `Gross Pay − Pre-Tax Deductions − Post-Tax Deductions − Federal Tax − State Tax − Social Security − Medicare`

All monetary values are rounded to two decimal places using `MidpointRounding.AwayFromZero` (round half away from zero).

## State Tax Coverage

All 50 states and the District of Columbia are supported. The architecture uses a plugin-based registry where each state implements `IStateWithholdingCalculator`:

| Category | States |
|---|---|
| **No Income Tax** | AK, FL, NV, NH, SD, TN, TX, WA, WY |
| **Flat Rate** | PA (3.07%) |
| **Custom Formula** | AL (graduated + dependents + federal deduction), AR (DFA formula method), CA (Method B, EDD DE 44 + SDI), CO (flat 4.4% + DR 0004 Table 1 allowance + FMLI), CT (TPG-211 table-driven + PFMLI), DE (DE W-4, 7 graduated brackets + $110 credit), IL (flat 4.95% + IL-W-4 allowances), OK (OW-2 percentage tables) |
| **Annualized Percentage Method** | AZ, DC, GA, HI, IA, ID, IN, KS, KY, LA, MA, MD, ME, MI, MN, MO, MS, MT, NC, ND, NE, NJ, NM, NY, OH, OR, RI, SC, UT, VA, VT, WI, WV |

## UI Overview

The app uses flyout navigation with the following sections:

- **Inputs** — A four-tab form for Pay & Hours, Federal W-4, State, and Deductions.
- **Results** — Two sub-tabs: **Period** (per-paycheck itemized taxes, deductions, net pay, doughnut chart, Save to Compare, Save as New Paycheck, and CSV/PDF export) and **Annual** (annualized projections, current paycheck number, and estimated year-end over/under withholding).
- **Compare** — Saves a calculation snapshot and shows a side-by-side diff against the current calculation.
- **Saved Paychecks** — Lists previously saved paycheck calculations with options to reload inputs into the calculator, rename, or delete entries.
- **Self-Employment** — Three-tab input form (business income, expenses, QBI) for Schedule SE / Form 8995 calculations.
- **SE Results** — Two-tab results view showing self-employment tax breakdown and QBI deduction.
- **Annual Tax** — Inputs for full-year Form 1040 estimation: filing status, W-2 jobs, Schedule 1 adjustments, credits, and other taxes.
- **Annual Results** — Year-end estimate combining withholding, credits, NIIT, and balance due / refund.

## Documentation

- [Wiki](docs/wiki/Home.md) — Full project wiki covering architecture, tax engine, state coverage, self-employment module, UI guide, and contributing guidelines.
- [UML Class Diagram](docs/class-diagram.md) — Mermaid-based class diagram of the full architecture.
