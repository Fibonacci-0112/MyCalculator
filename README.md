# PaycheckCalc

A cross-platform paycheck calculator that computes net pay, tax withholdings, and deductions for all 50 US states plus DC using 2026 tax tables. The solution ships with two front-ends — a **.NET MAUI** app (Android & Windows) and a **Blazor Server** web head — both backed by a shared UI-agnostic core library. It also includes self-employment (Schedule SE + QBI) and annual Form 1040 tax estimation modules.

## Features

- **Gross Pay Calculation** — Computes gross pay from hourly rate, regular hours, and overtime hours with a configurable overtime multiplier.
- **Federal Income Tax** — Implements the IRS Publication 15-T (2026) percentage method for automated payroll systems, supporting all W-4 inputs (filing status, Step 2 checkbox, Step 3 credits, Step 4 adjustments).
- **FICA Taxes** — Calculates Social Security (6.2%, capped at $184,500), Medicare (1.45%), and Additional Medicare (0.9% above $200,000).
- **State Income Tax** — Covers all 50 states and DC with state-specific calculators:
  - **9 no-income-tax states** — AK, FL, NV, NH, SD, TN, TX, WA, WY
  - **Flat-rate state** — Pennsylvania (3.07%)
  - **Custom formula states** — Alabama (graduated + dependents + federal deduction), Arkansas (DFA formula method), California (Method B, EDD DE 44 withholding tables + SDI), Colorado (flat 4.4% + DR 0004 Table 1 allowance + FMLI), Connecticut (TPG-211 table-driven withholding + PFMLI), Delaware (DE W-4, 7 graduated brackets + $110 personal credit), Georgia (flat 5.19% per HB 111 + G-4 allowances and dependent deductions), Illinois (flat 4.95% + IL-W-4 allowances), Oklahoma (OW-2)
  - **Percentage method states** — 32 states using an annualized graduated bracket approach with state-specific standard deductions, allowances, and tax brackets
- **Local / Sub-State Taxes** — Plugin-based local (city / county / school district) withholding with calculators for Pennsylvania Act 32 EIT + LST, New York City, Ohio RITA and CCA, and Maryland county surtax, all JSON-backed.
- **State Disability / Family Leave Insurance** — California SDI and Connecticut PFMLI are computed alongside state withholding with dynamic labels on the results screen and exports.
- **Pre-Tax & Post-Tax Deductions** — Supports configurable deductions that reduce taxable wages.
- **Dynamic State Inputs** — Each state declares its own input schema (filing status, allowances, dependents, extra withholding), and the UI renders fields dynamically.
- **Annual Projection** — Estimates annualized gross, taxes, and net pay; tracks current paycheck number and remaining pay periods, and projects year-end over/under withholding.
- **Self-Employment Tax** — Schedule SE calculator with FICA coordination for W-2 wages (reduces remaining Social Security wage base and Additional Medicare threshold) plus Form 8995/8995-A QBI deduction.
- **Annual Form 1040 Estimation** — Orchestrates full-year federal tax estimates using 2026 Rev. Proc. 2025-32 brackets and standard deductions, Schedule 1 adjustments, Child Tax Credit, Form 8863 education credits, Form 8880 saver's credit, and Form 8960 Net Investment Income Tax.
- **Quarterly Estimated Taxes** — Form 1040-ES quarterly estimate calculator and a per-period withholding-suggestion calculator for closing projected year-end balances.
- **Address & Jurisdiction Lookup** — Optional Google Maps geocoding + jurisdiction resolver maps a home/work address to the applicable state and local tax jurisdictions.
- **Export Results** — Exports per-period paycheck results to CSV or PDF (powered by QuestPDF) and shares via the native OS share sheet.
- **Results Visualization** — Doughnut chart breakdown of gross pay by category (federal tax, state tax, Social Security, Medicare, net pay).
- **Side-by-Side Comparison** — Save a snapshot of one calculation and compare it against the current inputs.
- **Saved Paychecks** — Persist named paycheck calculations to local JSON storage, reload them into the calculator, and manage saved entries from a dedicated page.
- **Multiple Pay Frequencies** — Weekly, Bi-Weekly, Semi-Monthly, Monthly, Quarterly, Semi-Annual, Annual, and Daily.

## Project Structure

```
PaycheckCalc.slnx
├── PaycheckCalc.App/          # .NET MAUI frontend (Android & Windows)
│   ├── Views/                 # XAML pages (Inputs, Results, Compare, Saved Paychecks, Self-Employment,
│   │                          #   Annual Projection, Jobs & YTD, Other Income & Adjustments,
│   │                          #   Credits, Quarterly Estimates, What-If, Annual Results)
│   ├── ViewModels/            # MVVM view models (Calculator, Compare, SavedPaychecks, SelfEmployment,
│   │                          #   AnnualTax, AnnualProjection, JobsAndYtd, OtherIncomeAdjustments,
│   │                          #   Credits, QuarterlyEstimates, WhatIf, StateField, DeductionItem)
│   ├── Mappers/               # Domain-to-UI mappers (ResultCard, AnnualProjection, Scenario,
│   │                          #   SavedPaycheck, PaycheckInput, AnnualTaxInput, SelfEmployment)
│   ├── Models/                # UI presentation models (ResultCardModel, AnnualProjectionModel, ScenarioSnapshot)
│   ├── Controls/              # Custom controls (DoughnutChartDrawable)
│   ├── Behaviors/             # Input formatting behaviors
│   ├── Helpers/               # Enum display helpers
│   ├── Services/              # AnnualTaxSession, ComparisonSession, geocoding/jurisdiction services
│   ├── Storage/               # JsonPaycheckRepository, JsonAnnualScenarioRepository (local JSON persistence)
│   └── MauiProgram.cs         # DI configuration & app startup
├── PaycheckCalc.Blazor/       # Blazor Server (Blazor Web App, net11.0) web head
│   ├── Components/            # Razor components and pages (Inputs, Results)
│   ├── Services/              # CalculatorSessionState (shared per-circuit state)
│   ├── wwwroot/               # Static assets; tax JSON is content-linked from Core/Data at build
│   └── Program.cs             # Web host, DI, and tax-data loading
├── PaycheckCalc.Core/         # Business logic (no UI dependencies)
│   ├── Models/                # PaycheckInput/Result, Enums, Deduction, AnnualProjection,
│   │                          #   CalculationScenario, SavedPaycheck, SelfEmploymentInput,
│   │                          #   TaxYearProfile, AnnualTaxResult, W2JobInput, Credits & other-income inputs,
│   │                          #   WithholdingSuggestion*, QuarterlyEstimatesResult, SavedAnnualScenario
│   ├── Pay/                   # PayCalculator (main orchestrator), AnnualProjectionCalculator
│   ├── Export/                # CsvPaycheckExporter, PdfPaycheckExporter (QuestPDF)
│   ├── Geocoding/             # Address input, IGeocodingService, GeocodeResult
│   ├── Storage/               # IPaycheckRepository, IAnnualScenarioRepository
│   ├── Data/                  # JSON tax tables (IRS 15-T, Federal 1040, OK OW-2, CA Method B, AR,
│   │                          #   CO DR 0004, CT TPG-211, PA EIT, NYC, OH RITA/CCA, MD county surtax)
│   └── Tax/
│       ├── Federal/           # IRS 15-T percentage calculator
│       │   └── Annual/        # Form 1040 orchestrator, Federal 1040 brackets, Schedule 1,
│       │                      #   CTC, Form 8863/8880/8960, Form 1040-ES, Withholding suggestion
│       ├── Fica/              # Social Security & Medicare calculator
│       ├── SelfEmployment/    # Schedule SE tax & Form 8995/8995-A QBI deduction
│       ├── State/             # State tax interfaces, registry, generic percentage-method adapter,
│       │                      #   and annual state-tax calculator
│       ├── Local/             # Local (city/county/school district) plugin model + registry
│       │   ├── Maryland/      # County surtax (JSON-backed)
│       │   ├── NewYork/       # NYC withholding (JSON-backed)
│       │   ├── Ohio/          # RITA + CCA (JSON-backed)
│       │   └── Pennsylvania/  # Act 32 EIT (JSON-backed) + LST flat head tax
│       ├── Alabama/           # Alabama-specific formula calculator
│       ├── Arkansas/          # Arkansas DFA formula calculator
│       ├── California/        # California Method B calculator (+ SDI)
│       ├── Colorado/          # Colorado flat-rate + DR 0004 calculator (+ FMLI)
│       ├── Connecticut/       # Connecticut TPG-211 table-driven calculator (+ PFMLI)
│       ├── Delaware/          # Delaware DE W-4 graduated calculator (+ personal credit)
│       ├── Georgia/           # Georgia flat 5.19% + G-4 allowances (HB 111, 2026)
│       ├── Illinois/          # Illinois flat-rate + IL-W-4 allowance calculator
│       ├── Oklahoma/          # Oklahoma OW-2 percentage calculator
│       └── Pennsylvania/      # Pennsylvania flat-rate calculator
├── PaycheckCalc.Tests/        # xUnit test suite
└── docs/                      # Class diagrams and documentation
```

## Technology Stack

| Component | Technology |
|---|---|
| **Frameworks** | .NET 11 Preview — MAUI (PaycheckCalc.App) and Blazor Web App / Server rendering (PaycheckCalc.Blazor) |
| **Target Platforms** | Android, Windows 10+ (MAUI); modern browsers via server-rendered Blazor |
| **UI Pattern** | MVVM with [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) on MAUI; Razor components on Blazor |
| **Test Framework** | xUnit 2.9.3 |
| **PDF Export** | [QuestPDF](https://www.questpdf.com/) 2025.12.4 |
| **Tax Data** | JSON-based IRS 15-T, Federal 1040, and state / local tax bracket tables (2026) |

## Prerequisites

- [.NET 11 SDK](https://dotnet.microsoft.com/) (preview) — pinned in `global.json`
- .NET MAUI workload (only required for the MAUI App project):
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

### Run the MAUI App

```bash
dotnet build PaycheckCalc.App
dotnet run --project PaycheckCalc.App
```

> **Note:** The MAUI app requires the `maui` workload and a supported target platform (Android emulator/device or Windows).

### Run the Blazor Server Web App

The Blazor head does **not** require the MAUI workload and can run on any supported OS:

```bash
dotnet run --project PaycheckCalc.Blazor
```

The web app loads tax JSON at startup from its build output (linked from `PaycheckCalc.Core/Data/`) and uses an interactive Server rendering mode with a per-circuit `CalculatorSessionState` shared between the inputs and results pages.

## How It Works

The **PayCalculator** orchestrates the full paycheck calculation pipeline:

1. **Gross Pay** — `(Regular Hours × Hourly Rate) + (Overtime Hours × Hourly Rate × OT Multiplier)`
2. **Deductions** — Pre-tax deductions reduce taxable wages; post-tax deductions are subtracted after taxes.
3. **FICA** — Social Security and Medicare are calculated on gross wages minus applicable pre-tax deductions, with annual wage-base caps.
4. **Federal Withholding** — Wages are annualized, the standard deduction and W-4 adjustments are applied, the tax is computed using graduated brackets from IRS Publication 15-T, and the result is de-annualized back to the pay period.
5. **State Withholding** — The `StateCalculatorRegistry` looks up the registered `IStateWithholdingCalculator` for the selected state and delegates calculation using the state's specific rules and inputs.
6. **Local Withholding** — When a local jurisdiction is selected, the `LocalCalculatorRegistry` delegates to a registered `ILocalWithholdingCalculator` (e.g., PA Act 32 EIT + LST, NYC, Ohio RITA/CCA, MD county surtax). Local taxes are additive — they reduce net pay but do not reduce federal or state taxable wages.
7. **Net Pay** — `Gross Pay − Pre-Tax Deductions − Post-Tax Deductions − Federal Tax − State Tax − State Disability − Social Security − Medicare − Additional Medicare − Local Withholding − Local Head Tax`

Gross pay, taxes, and deductions are rounded individually to two decimal places using `MidpointRounding.AwayFromZero` (round half away from zero). Net pay is computed from the unrounded components and then rounded so the displayed net equals `gross − taxes − deductions` to the cent.

## State Tax Coverage

All 50 states and the District of Columbia are supported. The architecture uses a plugin-based registry where each state implements `IStateWithholdingCalculator`:

| Category | States |
|---|---|
| **No Income Tax** | AK, FL, NV, NH, SD, TN, TX, WA, WY |
| **Flat Rate** | PA (3.07%) |
| **Custom Formula** | AL (graduated + dependents + federal deduction), AR (DFA formula method), CA (Method B, EDD DE 44 + SDI), CO (flat 4.4% + DR 0004 Table 1 allowance + FMLI), CT (TPG-211 table-driven + PFMLI), DE (DE W-4, 7 graduated brackets + $110 credit), GA (flat 5.19% + G-4 allowances, HB 111 / 2026 Employer's Tax Guide), IL (flat 4.95% + IL-W-4 allowances), OK (OW-2 percentage tables) |
| **Annualized Percentage Method** | AZ, DC, HI, IA, ID, IN, KS, KY, LA, MA, MD, ME, MI, MN, MO, MS, MT, NC, ND, NE, NJ, NM, NY, OH, OR, RI, SC, UT, VA, VT, WI, WV |

## Local (Sub-State) Tax Coverage

PaycheckCalc also models a growing set of local / sub-state payroll taxes via an `ILocalWithholdingCalculator` plugin model and `LocalCalculatorRegistry`. Current coverage:

| Jurisdiction | Calculator | Notes |
|---|---|---|
| Pennsylvania (Act 32) | `PaEitCalculator` | Earned Income Tax by municipality, JSON-backed rate table |
| Pennsylvania LST | `PaLstCalculator` | Flat per-pay-period Local Services Tax (head tax) |
| New York City | `NycWithholdingCalculator` | NYC resident personal income tax, JSON-backed |
| Ohio (RITA) | `OhRitaCalculator` | Regional Income Tax Agency municipal rates, JSON-backed |
| Ohio (CCA) | `OhCcaCalculator` | Central Collection Agency municipal rates, JSON-backed |
| Maryland county surtax | `MdCountyCalculator` | County-level surtax paired with the MD state calculator, JSON-backed |

Local taxes are additive: they subtract from net pay but do **not** reduce federal or state taxable wages.

## UI Overview

The MAUI app uses flyout navigation with the following sections:

- **Inputs** — A four-tab form for Pay & Hours, Federal W-4, State, and Deductions.
- **Results** — Two sub-tabs: **Period** (per-paycheck itemized taxes, deductions, net pay, doughnut chart, Save to Compare, Save as New Paycheck, and CSV/PDF export) and **Annual** (annualized projections, current paycheck number, and estimated year-end over/under withholding).
- **Compare** — Side-by-side comparison. Falls back to a 1-vs-1 saved-vs-current view, or renders a multi-scenario layout when scenarios are pushed from Saved Paychecks via `ComparisonSession`.
- **Saved Paychecks** — Lists previously saved paycheck calculations with options to reload inputs into the calculator, rename, or delete entries, and push one or more into the multi-scenario comparison.
- **Self-Employment** — Three-tab input form (business income, expenses, QBI) for Schedule SE / Form 8995 calculations.
- **SE Results** — Two-tab results view showing self-employment tax breakdown and QBI deduction.
- **Annual Projection / Jobs & YTD / Other Income & Adjustments / Credits / Quarterly Estimates / What-If** — Phase 8 flyout pages that share a single `AnnualTaxSession`. Together they collect Form 1040 inputs, credits, estimated-tax and what-if scenarios, and persist saved annual scenarios via `JsonAnnualScenarioRepository`.
- **Annual Results** — Year-end estimate combining withholding, credits, NIIT, and balance due / refund.

The Blazor Server head exposes a streamlined two-page flow — **Inputs** and **Results** — backed by a scoped `CalculatorSessionState` that stores the in-flight `PaycheckInput` and `PaycheckResult` between page navigations within a circuit.

## Documentation

- [Wiki](docs/wiki/Home.md) — Full project wiki covering architecture, tax engine, state coverage, self-employment module, UI guide, and contributing guidelines.
- [UML Class Diagram](docs/class-diagram.md) — Mermaid-based class diagram of the full architecture.
