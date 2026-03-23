# PaycheckCalc

A cross-platform .NET MAUI paycheck calculator that computes net pay, tax withholdings, and deductions for all 50 US states plus DC using 2026 tax tables.

## Features

- **Gross Pay Calculation** — Computes gross pay from hourly rate, regular hours, and overtime hours with a configurable overtime multiplier.
- **Federal Income Tax** — Implements the IRS Publication 15-T (2026) percentage method for automated payroll systems, supporting all W-4 inputs (filing status, Step 2 checkbox, Step 3 credits, Step 4 adjustments).
- **FICA Taxes** — Calculates Social Security (6.2%, capped at $184,500), Medicare (1.45%), and Additional Medicare (0.9% above $200,000).
- **State Income Tax** — Covers all 50 states and DC with state-specific calculators:
  - **9 no-income-tax states** — AK, FL, NV, NH, SD, TN, TX, WA, WY
  - **Flat-rate state** — Pennsylvania (3.07%)
  - **Custom formula states** — Alabama (graduated + dependents + federal deduction), Arkansas (DFA formula method), California (Method B, EDD DE 44 withholding tables), Colorado (flat 4.4% + DR 0004 Table 1 allowance + FMLI), Oklahoma (OW-2)
  - **Percentage method states** — 36 states using an annualized graduated bracket approach with state-specific standard deductions, allowances, and tax brackets
- **Pre-Tax & Post-Tax Deductions** — Supports configurable deductions that reduce taxable wages.
- **Dynamic State Inputs** — Each state declares its own input schema (filing status, allowances, dependents, extra withholding), and the UI renders fields dynamically.
- **Annual Projection** — Estimates annualized gross, taxes, and net pay; tracks current paycheck number and remaining pay periods, and projects year-end over/under withholding.
- **Export Results** — Exports per-period paycheck results to CSV or PDF (powered by QuestPDF) and shares via the native OS share sheet.
- **Results Visualization** — Doughnut chart breakdown of gross pay by category (federal tax, state tax, Social Security, Medicare, net pay).
- **Side-by-Side Comparison** — Save a snapshot of one calculation and compare it against the current inputs.
- **Multiple Pay Frequencies** — Weekly, Bi-Weekly, Semi-Monthly, Monthly, Quarterly, Semi-Annual, Annual, and Daily.

## Project Structure

```
PaycheckCalc.slnx
├── PaycheckCalc.App/          # .NET MAUI frontend (Android & Windows)
│   ├── Views/                 # XAML pages (Inputs, Results, Compare)
│   ├── ViewModels/            # MVVM view models
│   ├── Mappers/               # Domain-to-UI mappers (ResultCard, AnnualProjection, Scenario)
│   ├── Models/                # UI presentation models (ResultCardModel, AnnualProjectionModel, ScenarioSnapshot)
│   ├── Controls/              # Custom controls (DoughnutChartDrawable)
│   ├── Behaviors/             # Input formatting behaviors
│   ├── Helpers/               # Enum display helpers
│   └── MauiProgram.cs         # DI configuration & app startup
├── PaycheckCalc.Core/         # Business logic (no UI dependencies)
│   ├── Models/                # PaycheckInput, PaycheckResult, Enums, Deduction, AnnualProjection, CalculationScenario
│   ├── Pay/                   # PayCalculator (main orchestrator), AnnualProjectionCalculator
│   ├── Export/                # CsvPaycheckExporter, PdfPaycheckExporter (QuestPDF)
│   ├── Data/                  # JSON tax bracket tables (IRS 15-T, OK OW-2, CA Method B, AR, CO DR 0004)
│   └── Tax/
│       ├── Federal/           # IRS 15-T percentage calculator
│       ├── Fica/              # Social Security & Medicare calculator
│       ├── State/             # State tax interfaces, registry, and generic calculator
│       ├── Alabama/           # Alabama-specific formula calculator
│       ├── Arkansas/          # Arkansas DFA formula calculator
│       ├── California/        # California Method B calculator
│       ├── Colorado/          # Colorado flat-rate + DR 0004 calculator
│       ├── Oklahoma/          # Oklahoma OW-2 percentage calculator
│       └── Pennsylvania/      # Pennsylvania flat-rate calculator
├── PaycheckCalc.Tests/        # xUnit test suite
└── docs/                      # Class diagrams and documentation
```

## Technology Stack

| Component | Technology |
|---|---|
| **Framework** | .NET 11 Preview / .NET MAUI |
| **Target Platforms** | Android, Windows 10+ |
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
| **Custom Formula** | AL (graduated + dependents + federal deduction), AR (DFA formula method), CA (Method B, EDD DE 44 withholding tables), CO (flat 4.4% + DR 0004 Table 1 allowance + FMLI), OK (OW-2 percentage tables) |
| **Annualized Percentage Method** | AZ, CT, DC, DE, GA, HI, IA, ID, IL, IN, KS, KY, LA, MA, MD, ME, MI, MN, MO, MS, MT, NC, ND, NE, NJ, NM, NY, OH, OR, RI, SC, UT, VA, VT, WI, WV |

## UI Overview

The app has three main tabs:

- **Inputs** — A four-section form for Pay & Hours, Federal W-4, State, and Deductions.
- **Results** — Two sub-tabs: **Period** (per-paycheck itemized taxes, deductions, net pay, doughnut chart, Save to Compare, and CSV/PDF export) and **Annual** (annualized projections, current paycheck number, and estimated year-end over/under withholding).
- **Compare** — Saves a calculation snapshot and shows a side-by-side diff against the current calculation.

## Documentation

- [UML Class Diagram](docs/class-diagram.md) — Mermaid-based class diagram of the full architecture.
