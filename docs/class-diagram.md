# UML Class Diagram

> High-level Mermaid class diagram for the **PaycheckCalc** solution.
> Render with any Mermaid-compatible viewer (GitHub markdown, VS Code extension, etc.).
>
> The diagram below is intentionally architectural rather than exhaustive: each
> per-state withholding calculator (50 states + DC) and each per-locality
> calculator implements the registry-driven interfaces shown here, so they are
> elided in favor of the contracts and registries that wire them together.

## Package overview

```mermaid
classDiagram
    direction TB

    class Core["PaycheckCalc.Core"] {
        <<library>>
        UI-agnostic tax engine
    }
    class App["PaycheckCalc.App (MAUI)"] {
        <<head>>
        Android & Windows MVVM
    }
    class Blazor["PaycheckCalc.Blazor"] {
        <<head>>
        Blazor Server web app
    }
    class Tests["PaycheckCalc.Tests"] {
        <<xUnit>>
    }

    App ..> Core : ProjectReference
    Blazor ..> Core : ProjectReference
    Tests ..> Core : ProjectReference
```

## Core — paycheck pipeline

```mermaid
classDiagram
    direction TB

    %% ── Domain models ───────────────────────────────────────
    class PaycheckInput {
        <<sealed>>
        +PayFrequency Frequency
        +decimal HourlyRate
        +decimal RegularHours
        +decimal OvertimeHours
        +decimal OvertimeMultiplier
        +UsState State
        +StateInputValues? StateInputValues
        +string? HomeLocalityCode
        +string? WorkLocalityCode
        +LocalInputValues? LocalInputValues
        +FederalW4Input FederalW4
        +IReadOnlyList~Deduction~ Deductions
        +int PaycheckNumber
    }

    class PaycheckResult {
        <<sealed>>
        +decimal GrossPay
        +decimal PreTaxDeductions
        +decimal PostTaxDeductions
        +decimal FederalTaxableIncome
        +decimal FederalWithholding
        +decimal SocialSecurityWithholding
        +decimal MedicareWithholding
        +decimal AdditionalMedicareWithholding
        +UsState State
        +decimal StateTaxableWages
        +decimal StateWithholding
        +decimal StateDisabilityInsurance
        +decimal LocalWithholding
        +decimal LocalHeadTax
        +IReadOnlyList~LocalBreakdownLine~ LocalBreakdown
        +decimal NetPay
        +decimal TotalTaxes
    }

    class FederalW4Input {
        +FederalFilingStatus FilingStatus
        +bool Step2Checked
        +decimal Step3TaxCredits
        +decimal Step4aOtherIncome
        +decimal Step4bDeductions
        +decimal Step4cExtraWithholding
    }

    class Deduction {
        +string Name
        +DeductionType Type
        +DeductionAmountType AmountType
        +decimal Amount
        +bool ReducesStateTaxableWages
        +bool ReducesFicaWages
    }

    %% ── Pay orchestrator ────────────────────────────────────
    class PayCalculator {
        <<sealed>>
        +PayCalculator(StateCalculatorRegistry, FicaCalculator, Irs15TPercentageCalculator, LocalCalculatorRegistry)
        +Calculate(PaycheckInput) PaycheckResult
    }

    class AnnualProjectionCalculator {
        +AnnualProjectionCalculator(Irs15TPercentageCalculator, FicaCalculator)
        +Project(PaycheckInput, PaycheckResult) AnnualProjection
    }

    %% ── Federal & FICA ──────────────────────────────────────
    class Irs15TPercentageCalculator {
        +Irs15TPercentageCalculator(string json)
        +CalculateWithholding(decimal taxableWages, PayFrequency, FederalW4Input) decimal
    }

    class FicaCalculator {
        +SocialSecurityWageBase: decimal
        +SocialSecurityRate: 0.062
        +MedicareRate: 0.0145
        +AdditionalMedicareRate: 0.009
        +Calculate(...) FicaResult
    }

    %% ── State plugin model ─────────────────────────────────
    class IStateWithholdingCalculator {
        <<interface>>
        +UsState State
        +GetInputSchema() IReadOnlyList~StateFieldDefinition~
        +Validate(StateInputValues) IReadOnlyList~string~
        +Calculate(CommonWithholdingContext, StateInputValues) StateWithholdingResult
    }

    class StateCalculatorRegistry {
        <<sealed>>
        +Register(IStateWithholdingCalculator) void
        +IsSupported(UsState) bool
        +GetCalculator(UsState) IStateWithholdingCalculator
        +SupportedStates: IReadOnlyList~UsState~
    }

    class StateImpls["Per-state calculators (50 + DC)"] {
        <<implementations>>
        AlabamaWithholdingCalculator
        ArkansasWithholdingCalculator
        CaliforniaWithholdingCalculator
        ...
        WyomingWithholdingCalculator
        NoIncomeTaxWithholdingAdapter
        PercentageMethodWithholdingAdapter
    }

    %% ── Local plugin model ─────────────────────────────────
    class ILocalWithholdingCalculator {
        <<interface>>
        +LocalityId Locality
        +GetInputSchema() IReadOnlyList~StateFieldDefinition~
        +Validate(LocalInputValues) IReadOnlyList~string~
        +Calculate(CommonLocalWithholdingContext, LocalInputValues) LocalWithholdingResult
    }

    class LocalCalculatorRegistry {
        <<sealed>>
        +Register(ILocalWithholdingCalculator) void
        +IsSupported(UsState) bool
        +TryGetCalculator(string?, out ILocalWithholdingCalculator?) bool
        +GetCalculatorsForState(UsState) IReadOnlyList~ILocalWithholdingCalculator~
    }

    class LocalImpls["Local calculators"] {
        <<implementations>>
        PaEitCalculator
        PaLstCalculator
        NycWithholdingCalculator
        OhRitaCalculator (OhioMunicipalCalculator)
        OhCcaCalculator (OhioMunicipalCalculator)
        MdCountyCalculator
    }

    PaycheckInput *-- FederalW4Input
    PaycheckInput *-- "0..*" Deduction
    PayCalculator ..> PaycheckInput
    PayCalculator ..> PaycheckResult
    PayCalculator --> StateCalculatorRegistry
    PayCalculator --> LocalCalculatorRegistry
    PayCalculator --> FicaCalculator
    PayCalculator --> Irs15TPercentageCalculator
    AnnualProjectionCalculator --> Irs15TPercentageCalculator
    AnnualProjectionCalculator --> FicaCalculator
    StateCalculatorRegistry o-- "0..*" IStateWithholdingCalculator
    StateImpls ..|> IStateWithholdingCalculator
    LocalCalculatorRegistry o-- "0..*" ILocalWithholdingCalculator
    LocalImpls ..|> ILocalWithholdingCalculator
```

## Core — annual Form 1040 module

```mermaid
classDiagram
    direction TB

    class Form1040Calculator {
        <<sealed>>
        +Form1040Calculator(Federal1040TaxCalculator, Schedule1Calculator, SelfEmploymentTaxCalculator, QbiDeductionCalculator, FicaCalculator, AnnualStateTaxCalculator)
        +Calculate(TaxYearProfile) AnnualTaxResult
    }

    class Federal1040TaxCalculator {
        +Federal1040TaxCalculator(string json)
        +TaxYear: int
        +GetStandardDeduction(FederalFilingStatus) decimal
        +CalculateTax(decimal taxableIncome, FederalFilingStatus) decimal
        +GetMarginalRate(decimal taxableIncome, FederalFilingStatus) decimal
    }

    class Schedule1Calculator {
        +Calculate(OtherIncomeInput, AdjustmentsInput) Schedule1Result
    }

    class ChildTaxCreditCalculator {
        +Calculate(ChildTaxCreditInput, FederalFilingStatus, decimal agi, decimal taxBeforeCtc) ChildTaxCreditResult
    }
    class Form8863EducationCreditsCalculator {
        +Calculate(EducationCreditsInput, FederalFilingStatus, decimal agi) EducationCreditsResult
    }
    class Form8880SaversCreditCalculator {
        +Calculate(SaversCreditInput, FederalFilingStatus, decimal agi) SaversCreditResult
    }
    class Form8960NiitCalculator {
        +Calculate(NetInvestmentIncomeInput, FederalFilingStatus, decimal agi) decimal
    }

    class AnnualStateTaxCalculator {
        +AnnualStateTaxCalculator(StateCalculatorRegistry)
        +Calculate(TaxYearProfile, decimal federalTaxAnnual) AnnualStateTaxResult
    }

    class Form1040ESCalculator {
        +Calculate(int taxYear, FederalFilingStatus, decimal currentYearProjectedTax, decimal expectedWithholding, PriorYearSafeHarborInput?) QuarterlyEstimatesResult
    }

    class WithholdingSuggestionCalculator {
        +Calculate(WithholdingSuggestionInput) WithholdingSuggestionResult
    }

    class TaxYearProfile {
        +int TaxYear
        +FederalFilingStatus FilingStatus
        +int QualifyingChildren
        +UsState ResidenceState
        +IReadOnlyList~W2JobInput~ W2Jobs
        +SelfEmploymentInput? SelfEmployment
        +OtherIncomeInput OtherIncome
        +AdjustmentsInput Adjustments
        +decimal ItemizedDeductionsOverStandard
        +CreditsInput Credits
        +OtherTaxesInput OtherTaxes
        +decimal EstimatedTaxPayments
        +PriorYearSafeHarborInput? PriorYearSafeHarbor
        +decimal AdditionalExpectedWithholding
        +StateInputValues? StateInputValues
    }

    class AnnualTaxResult {
        <<35+ init properties>>
        income build-up · deductions
        federal tax · credits
        state tax · payments
        refund or balance due
    }

    Form1040Calculator --> Federal1040TaxCalculator
    Form1040Calculator --> Schedule1Calculator
    Form1040Calculator --> ChildTaxCreditCalculator
    Form1040Calculator --> Form8863EducationCreditsCalculator
    Form1040Calculator --> Form8880SaversCreditCalculator
    Form1040Calculator --> Form8960NiitCalculator
    Form1040Calculator --> AnnualStateTaxCalculator
    Form1040Calculator ..> TaxYearProfile
    Form1040Calculator ..> AnnualTaxResult
    AnnualStateTaxCalculator --> StateCalculatorRegistry
```

## Core — self-employment module

```mermaid
classDiagram
    direction LR

    class SelfEmploymentCalculator {
        <<sealed>>
        +SelfEmploymentCalculator(SelfEmploymentTaxCalculator, QbiDeductionCalculator, Irs15TPercentageCalculator, StateCalculatorRegistry)
        +Calculate(SelfEmploymentInput) SelfEmploymentResult
    }
    class SelfEmploymentTaxCalculator {
        +SelfEmploymentTaxableRate: 0.9235
        +Calculate(decimal netSeEarnings, decimal w2SsWages, decimal w2MedicareWages) SelfEmploymentTaxResult
    }
    class QbiDeductionCalculator {
        +QbiRate: 0.20
        +Calculate(decimal qbi, decimal taxableBeforeQbi, FederalFilingStatus, bool isSstb, decimal w2Wages, decimal ubia) decimal
    }

    class SelfEmploymentInput {
        <<sealed>>
        Schedule C: GrossRevenue, COGS, TotalBusinessExpenses
        OtherIncome
        W-2 coordination: W2SocialSecurityWages, W2MedicareWages
        FilingStatus, State, StateInputValues?
        ItemizedDeductionsOverStandard
        QBI: IsSpecifiedServiceBusiness, QualifiedBusinessW2Wages, QualifiedPropertyUbia
        EstimatedTaxPayments
    }
    class SelfEmploymentResult {
        <<sealed>>
        Schedule C net profit
        SE tax breakdown (SS / Medicare / Add'l Medicare)
        AGI, deductions, QBI, taxable income
        Federal & state income tax
        EffectiveTaxRate, EstimatedQuarterlyPayment, OverUnderPayment
    }

    SelfEmploymentCalculator --> SelfEmploymentTaxCalculator
    SelfEmploymentCalculator --> QbiDeductionCalculator
    SelfEmploymentCalculator --> Irs15TPercentageCalculator
    SelfEmploymentCalculator --> StateCalculatorRegistry
    SelfEmploymentCalculator ..> SelfEmploymentInput
    SelfEmploymentCalculator ..> SelfEmploymentResult
```

## Core — storage, geocoding, exports

```mermaid
classDiagram
    direction TB

    class IPaycheckRepository {
        <<interface>>
        +GetAllAsync() Task~IReadOnlyList~SavedPaycheck~~
        +GetByIdAsync(Guid) Task~SavedPaycheck?~
        +SaveAsync(SavedPaycheck) Task
        +DeleteAsync(Guid) Task
    }
    class IAnnualScenarioRepository {
        <<interface>>
        +GetAllAsync() Task~IReadOnlyList~SavedAnnualScenario~~
        +GetByIdAsync(Guid) Task~SavedAnnualScenario?~
        +SaveAsync(SavedAnnualScenario) Task
        +DeleteAsync(Guid) Task
    }
    class SavedPaycheck {
        +Guid Id
        +string Name
        +DateTimeOffset CreatedAt
        +DateTimeOffset UpdatedAt
        +PaycheckInput Input
        +PaycheckResult Result
    }
    class SavedAnnualScenario {
        +Guid Id
        +string Name
        +DateTimeOffset CreatedAt
        +DateTimeOffset UpdatedAt
        +TaxYearProfile Profile
        +AnnualTaxResult? Result
    }

    class IGeocodingService {
        <<interface>>
        +GeocodeAsync(AddressInput, CancellationToken) Task~GeocodeResult?~
    }
    class IJurisdictionService {
        <<interface>>
        +ResolveAsync(GeocodeResult, CancellationToken) Task~JurisdictionResult~
    }
    class IAddressService {
        <<interface>>
        +Normalize(AddressInput, out IReadOnlyList~string~ errors) AddressInput
    }
    class IGeocodingCache {
        <<interface>>
        +TryGet(string, out GeocodeResult?) bool
        +Set(string, GeocodeResult) void
    }

    class CsvPaycheckExporter {
        +Generate(PaycheckResult) string
    }
    class PdfPaycheckExporter {
        +Generate(PaycheckResult) byte[]
    }
    class CsvSelfEmploymentExporter {
        +Generate(SelfEmploymentResult) string
    }
    class PdfSelfEmploymentExporter {
        +Generate(SelfEmploymentResult) byte[]
    }

    IPaycheckRepository ..> SavedPaycheck
    IAnnualScenarioRepository ..> SavedAnnualScenario
```

## MAUI head

```mermaid
classDiagram
    direction TB

    class AppShell {
        <<XAML Shell>>
        FlyoutItem Paycheck Calculator
        FlyoutItem Self-Employment
        FlyoutItem Annual Tax Planner
    }

    class Pages["Pages (17 .xaml)"] {
        <<grouped by hub>>
        Paycheck: Inputs, PayHours, Federal, State, Deductions, Results, Saved, Compare
        SE: SelfEmployment, SelfEmploymentResults
        Annual: AnnualProjection, JobsAndYtd, OtherIncomeAdjustments, Credits, QuarterlyEstimates, WhatIf, AnnualTaxResults
    }

    class ViewModels["ViewModels (15)"] {
        CalculatorViewModel
        SavedPaychecksViewModel
        CompareViewModel
        SelfEmploymentViewModel
        AnnualTaxViewModel
        AnnualProjectionViewModel
        JobsAndYtdViewModel
        OtherIncomeAdjustmentsViewModel
        CreditsViewModel
        QuarterlyEstimatesViewModel
        WhatIfViewModel
        DeductionItemViewModel
        StateFieldViewModel
        SavedPaycheckViewModel
        W2JobItemViewModel
    }

    class AnnualTaxSession {
        <<singleton, ObservableObject>>
        ~60 ObservableProperty fields → TaxYearProfile + CreditsInput
        +ObservableCollection~W2JobItemViewModel~ W2Jobs
        +AnnualTaxResultModel? ResultModel
        +Guid? LoadedScenarioId
    }
    class ComparisonSession {
        <<singleton>>
        +ObservableCollection~ScenarioSnapshot~ Scenarios
        +Clear() void
        +SetScenarios(IEnumerable~ScenarioSnapshot~) void
    }

    class JsonPaycheckRepository {
        <<sealed>>
        +JsonPaycheckRepository(string dataDirectory)
        ~File-backed JSON in FileSystem.AppDataDirectory~
    }
    class JsonAnnualScenarioRepository {
        <<sealed>>
        ~File-backed JSON in FileSystem.AppDataDirectory~
    }

    class GoogleMapsGeocodingService {
        +GeocodeAsync(AddressInput, CancellationToken) Task~GeocodeResult?~
    }
    class JurisdictionResolver {
        +ResolveAsync(GeocodeResult, CancellationToken) Task~JurisdictionResult~
    }

    class MauiProgram {
        <<DI composition root>>
        Registers all Core calculators + registries
        Registers ViewModels + Pages + Sessions
        Wires JsonPaycheckRepository as IPaycheckRepository
        Wires JsonAnnualScenarioRepository as IAnnualScenarioRepository
    }

    AppShell o-- Pages
    Pages --> ViewModels
    ViewModels --> AnnualTaxSession
    ViewModels --> ComparisonSession
    JsonPaycheckRepository ..|> IPaycheckRepository
    JsonAnnualScenarioRepository ..|> IAnnualScenarioRepository
    GoogleMapsGeocodingService ..|> IGeocodingService
    JurisdictionResolver ..|> IJurisdictionService
    MauiProgram ..> PayCalculator : registers
    MauiProgram ..> Form1040Calculator : registers
    MauiProgram ..> SelfEmploymentCalculator : registers
```

## Blazor head

```mermaid
classDiagram
    direction TB

    class BlazorPages["Components/Pages (6 .razor)"] {
        Home
        Inputs
        Results
        SavedPaychecks
        SelfEmployment
        SelfEmploymentResults
    }

    class CalculatorSessionState {
        <<scoped per circuit>>
        +decimal HourlyRate
        +decimal RegularHours
        +PayFrequency Frequency
        +FederalFilingStatus FederalFilingStatus
        +UsState State
        +StateInputValues StateInputValues
        +List~DeductionEntry~ Deductions
        +PaycheckResult? LastResult
        +LoadFromInput(PaycheckInput) void
        +BuildInput() PaycheckInput
    }

    class SelfEmploymentSessionState {
        <<scoped per circuit>>
        Schedule C, W-2 coord, QBI fields
        +SelfEmploymentResult? LastResult
        +BuildInput() SelfEmploymentInput
    }

    class LocalStoragePaycheckRepository {
        <<scoped per circuit>>
        +LocalStoragePaycheckRepository(IJSRuntime)
        ~Single JSON-array key in browser localStorage~
    }

    class paycheckStorage_js["wwwroot/js/paycheckStorage.js"] {
        <<JS interop shim>>
        get(key)
        set(key, value)
        remove(key)
    }

    class BlazorProgram {
        <<DI composition root>>
        Registers Core calculators + registries (parity with MauiProgram)
        Scoped: CalculatorSessionState, SelfEmploymentSessionState
        Scoped: IPaycheckRepository → LocalStoragePaycheckRepository
    }

    BlazorPages --> CalculatorSessionState
    BlazorPages --> SelfEmploymentSessionState
    BlazorPages --> LocalStoragePaycheckRepository
    LocalStoragePaycheckRepository ..|> IPaycheckRepository
    LocalStoragePaycheckRepository ..> paycheckStorage_js : invokes via IJSRuntime
    BlazorProgram ..> PayCalculator : registers
    BlazorProgram ..> SelfEmploymentCalculator : registers
    BlazorProgram ..> Form1040Calculator : registers
```

## Reading the diagrams together

- **Both heads share the same Core engine**: every calculator, registry, and
  domain model under `PaycheckCalc.Core/Tax`, `Pay`, `Models`, and `Storage` is
  consumed identically by `MauiProgram` and `Program.cs` (Blazor). The two heads
  only diverge on *presentation* (XAML vs. Razor) and *persistence implementations*
  (JSON files vs. browser `localStorage`).
- **Plugin registries are the extension points**: adding a new state means
  shipping one `IStateWithholdingCalculator` and registering it in both program
  composition roots; adding a new locality means one `ILocalWithholdingCalculator`.
  Neither change touches `PayCalculator` or any UI page.
- **Sessions are the bridge between pages**: in MAUI, `AnnualTaxSession` and
  `ComparisonSession` are singleton view-models that the 17 pages share to keep
  multi-page flows coherent. In Blazor, the equivalent role is filled by scoped
  per-circuit services (`CalculatorSessionState`, `SelfEmploymentSessionState`).
