# UML Class Diagram

```mermaid
classDiagram
    direction TB

    %% ───────────────────────────────────────
    %% Core Models – Enums
    %% ───────────────────────────────────────

    class PayFrequency {
        <<enumeration>>
        Weekly
        Biweekly
        Semimonthly
        Monthly
        Quarterly
        Semiannual
        Annual
        Daily
    }

    class FilingStatus {
        <<enumeration>>
        Single
        Married
    }

    class DeductionType {
        <<enumeration>>
        PreTax
        PostTax
    }

    class UsState {
        <<enumeration>>
        AK
        AL
        AR
        ...
        WV
        WY
    }

    class FederalFilingStatus {
        <<enumeration>>
        SingleOrMarriedSeparately
        MarriedFilingJointly
        HeadOfHousehold
    }

    class AlabamaFilingStatus {
        <<enumeration>>
        Zero
        Single
        MarriedFilingJointly
        MarriedFilingSeparately
        HeadOfFamily
    }

    %% ───────────────────────────────────────
    %% Core Models – Data Classes
    %% ───────────────────────────────────────

    class PaycheckInput {
        <<sealed>>
        +PayFrequency Frequency
        +FilingStatus FilingStatus
        +decimal HourlyRate
        +decimal RegularHours
        +decimal OvertimeHours
        +decimal OvertimeMultiplier
        +UsState State
        +int StateAllowances
        +decimal StateAdditionalWithholding
        +FederalW4Input FederalW4
        +IReadOnlyList~Deduction~ Deductions
        +decimal YtdSocialSecurityWages
        +decimal YtdMedicareWages
    }

    class PaycheckResult {
        <<sealed>>
        +decimal GrossPay
        +decimal PreTaxDeductions
        +decimal PostTaxDeductions
        +UsState State
        +decimal StateTaxableWages
        +decimal StateWithholding
        +decimal SocialSecurityWithholding
        +decimal MedicareWithholding
        +decimal AdditionalMedicareWithholding
        +decimal FederalTaxableIncome
        +decimal FederalWithholding
        +decimal NetPay
        +decimal TotalTaxes
    }

    class Deduction {
        <<sealed>>
        +string Name
        +DeductionType Type
        +decimal Amount
        +bool ReducesStateTaxableWages
    }

    class FederalW4Input {
        <<sealed>>
        +FederalFilingStatus FilingStatus
        +bool Step2Checked
        +decimal Step3TaxCredits
        +decimal Step4aOtherIncome
        +decimal Step4bDeductions
        +decimal Step4cExtraWithholding
    }

    PaycheckInput *-- FederalW4Input
    PaycheckInput *-- "0..*" Deduction
    Deduction --> DeductionType
    PaycheckInput --> PayFrequency
    PaycheckInput --> FilingStatus
    PaycheckInput --> UsState
    PaycheckResult --> UsState
    FederalW4Input --> FederalFilingStatus

    %% ───────────────────────────────────────
    %% State Tax – Interface & Implementations
    %% ───────────────────────────────────────

    class IStateWithholdingCalculator {
        <<interface>>
        +UsState State
        +GetInputSchema() IReadOnlyList~StateFieldDefinition~
        +Validate(StateInputValues values) IReadOnlyList~string~
        +Calculate(CommonWithholdingContext context, StateInputValues values) StateWithholdingResult
    }

    class CommonWithholdingContext {
        <<sealed record>>
        +UsState State
        +decimal GrossWages
        +PayFrequency PayPeriod
        +int Year
        +decimal PreTaxDeductionsReducingStateWages
    }

    class StateInputValues {
        <<sealed>>
        +GetValueOrDefault~T~(string key, T fallback) T
    }

    class StateFieldDefinition {
        <<sealed>>
        +string Key
        +string Label
        +StateFieldType FieldType
        +bool IsRequired
        +object DefaultValue
        +IReadOnlyList~string~ Options
    }

    class StateFieldType {
        <<enumeration>>
        Text
        Integer
        Decimal
        Toggle
        Picker
    }

    class StateWithholdingResult {
        <<sealed>>
        +decimal TaxableWages
        +decimal Withholding
        +string Description
    }

    class StateTaxInput {
        <<sealed>>
        +decimal GrossWages
        +PayFrequency Frequency
        +FilingStatus FilingStatus
        +int Allowances
        +decimal AdditionalWithholding
        +decimal PreTaxDeductionsReducingStateWages
    }

    class StateTaxResult {
        <<sealed>>
        +decimal TaxableWages
        +decimal Withholding
    }

    class StateCalculatorRegistry {
        <<sealed>>
        -Dictionary~UsState, IStateWithholdingCalculator~ _calculators
        -IReadOnlyList~UsState~ _sortedStates
        +IReadOnlyList~UsState~ SupportedStates
        +Register(IStateWithholdingCalculator calculator) void
        +IsSupported(UsState state) bool
        +GetCalculator(UsState state) IStateWithholdingCalculator
    }

    class NoIncomeTaxWithholdingAdapter {
        <<sealed>>
        +UsState State
        +NoIncomeTaxWithholdingAdapter(UsState state)
        +GetInputSchema() IReadOnlyList~StateFieldDefinition~
        +Validate(StateInputValues values) IReadOnlyList~string~
        +Calculate(CommonWithholdingContext context, StateInputValues values) StateWithholdingResult
    }

    class PercentageMethodStateTaxCalculator {
        <<sealed>>
        -PercentageMethodConfig _config
        +UsState State
        +PercentageMethodStateTaxCalculator(UsState state, PercentageMethodConfig config)
        +CalculateWithholding(StateTaxInput input) StateTaxResult
        -CalculateFromBrackets(decimal income, TaxBracket[] brackets)$ decimal
        -GetPayPeriods(PayFrequency frequency)$ int
    }

    class PercentageMethodWithholdingAdapter {
        <<sealed>>
        -PercentageMethodStateTaxCalculator _inner
        +UsState State
        +GetInputSchema() IReadOnlyList~StateFieldDefinition~
        +Validate(StateInputValues values) IReadOnlyList~string~
        +Calculate(CommonWithholdingContext context, StateInputValues values) StateWithholdingResult
    }

    class PercentageMethodConfig {
        <<sealed>>
        +decimal StandardDeductionSingle
        +decimal StandardDeductionMarried
        +decimal AllowanceAmount
        +decimal AllowanceCreditAmount
        +TaxBracket[] BracketsSingle
        +TaxBracket[] BracketsMarried
    }

    class TaxBracket {
        <<sealed>>
        +decimal Floor
        +decimal? Ceiling
        +decimal Rate
    }

    class StateTaxConfigs2026 {
        <<static>>
        +IReadOnlyDictionary~UsState, PercentageMethodConfig~ Configs$
    }

    IStateWithholdingCalculator --> CommonWithholdingContext
    IStateWithholdingCalculator --> StateInputValues
    IStateWithholdingCalculator --> StateWithholdingResult
    IStateWithholdingCalculator --> StateFieldDefinition
    StateFieldDefinition --> StateFieldType
    CommonWithholdingContext --> PayFrequency

    NoIncomeTaxWithholdingAdapter ..|> IStateWithholdingCalculator
    PercentageMethodWithholdingAdapter ..|> IStateWithholdingCalculator
    PercentageMethodWithholdingAdapter *-- PercentageMethodStateTaxCalculator
    PercentageMethodStateTaxCalculator *-- PercentageMethodConfig
    PercentageMethodStateTaxCalculator --> StateTaxInput
    PercentageMethodStateTaxCalculator --> StateTaxResult
    PercentageMethodConfig *-- "0..*" TaxBracket

    StateCalculatorRegistry o-- "0..*" IStateWithholdingCalculator
    StateTaxConfigs2026 ..> PercentageMethodConfig : provides

    %% ───────────────────────────────────────
    %% Pennsylvania State Tax
    %% ───────────────────────────────────────

    class PennsylvaniaWithholdingCalculator {
        <<sealed>>
        -decimal FlatRate$
        +UsState State
        +GetInputSchema() IReadOnlyList~StateFieldDefinition~
        +Validate(StateInputValues values) IReadOnlyList~string~
        +Calculate(CommonWithholdingContext context, StateInputValues values) StateWithholdingResult
    }

    PennsylvaniaWithholdingCalculator ..|> IStateWithholdingCalculator

    %% ───────────────────────────────────────
    %% Oklahoma State Tax
    %% ───────────────────────────────────────

    class OklahomaWithholdingCalculator {
        <<sealed>>
        -OklahomaOw2PercentageCalculator _inner
        +UsState State
        +OklahomaWithholdingCalculator(OklahomaOw2PercentageCalculator inner)
        +GetInputSchema() IReadOnlyList~StateFieldDefinition~
        +Validate(StateInputValues values) IReadOnlyList~string~
        +Calculate(CommonWithholdingContext context, StateInputValues values) StateWithholdingResult
    }

    class OklahomaOw2PercentageCalculator {
        <<sealed>>
        -Ow2Root _data
        +OklahomaOw2PercentageCalculator(string json)
        +GetAllowanceAmount(PayFrequency frequency) decimal
        +CalculateWithholding(decimal wages, PayFrequency frequency, FilingStatus status) decimal
        -FindBracket(List~Ow2Bracket~ brackets, decimal wages)$ Ow2Bracket
        -RoundToNearestWholeDollar(decimal amount)$ decimal
        -FrequencyKey(PayFrequency frequency)$ string
    }

    class Ow2Root {
        <<sealed>>
        +Dictionary~string, decimal~ AllowanceAmounts
        +List~Ow2Table~ Tables
    }

    class Ow2Table {
        <<sealed>>
        +string Frequency
        +List~Ow2Bracket~ Single
        +List~Ow2Bracket~ Married
    }

    class Ow2Bracket {
        <<sealed>>
        +decimal Over
        +decimal? Under
        +decimal Base
        +decimal Rate
        +decimal ExcessOver
    }

    OklahomaWithholdingCalculator ..|> IStateWithholdingCalculator
    OklahomaWithholdingCalculator *-- OklahomaOw2PercentageCalculator
    OklahomaOw2PercentageCalculator *-- Ow2Root
    Ow2Root *-- "0..*" Ow2Table
    Ow2Table *-- "0..*" Ow2Bracket

    %% ───────────────────────────────────────
    %% Alabama State Tax
    %% ───────────────────────────────────────

    class AlabamaWithholdingCalculator {
        <<sealed>>
        +UsState State
        +GetInputSchema() IReadOnlyList~StateFieldDefinition~
        +Validate(StateInputValues values) IReadOnlyList~string~
        +Calculate(CommonWithholdingContext context, StateInputValues values) StateWithholdingResult
    }

    class AlabamaFormulaCalculator {
        +CalculateWithholding(decimal grossWagesPerPeriod, int payPeriodsPerYear, decimal federalWithholdingPerPeriod, AlabamaFilingStatus filingStatus, int dependents)$ decimal
        -GetStandardDeduction(decimal gi, AlabamaFilingStatus status)$ decimal
        -GetPersonalExemption(AlabamaFilingStatus filingStatus)$ decimal
        -CalculateAnnualTax(decimal taxableIncome, AlabamaFilingStatus filingStatus)$ decimal
    }

    AlabamaWithholdingCalculator ..|> IStateWithholdingCalculator
    AlabamaWithholdingCalculator ..> AlabamaFormulaCalculator
    AlabamaFormulaCalculator --> AlabamaFilingStatus

    %% ───────────────────────────────────────
    %% Federal Tax
    %% ───────────────────────────────────────

    class Irs15TPercentageCalculator {
        <<sealed>>
        -Irs15TRoot _data
        +Irs15TPercentageCalculator(string json)
        +CalculateWithholding(decimal taxableWagesThisPeriod, PayFrequency frequency, FederalW4Input w4) decimal
        -GetBrackets(AnnualSchedule schedule, FederalFilingStatus status)$ List~AnnualBracket~
        -FindBracket(List~AnnualBracket~ brackets, decimal wages)$ AnnualBracket
        -RoundMoney(decimal amount)$ decimal
        -PayPeriodsPerYear(PayFrequency frequency)$ decimal
    }

    class Irs15TRoot {
        <<sealed>>
        +int Year
        +AnnualTables AnnualTables
        +WorksheetConstants WorksheetConstants
    }

    class AnnualTables {
        <<sealed>>
        +AnnualSchedule Standard
        +AnnualSchedule Step2Checked
    }

    class AnnualSchedule {
        <<sealed>>
        +List~AnnualBracket~ MarriedFilingJointly
        +List~AnnualBracket~ SingleOrMfs
        +List~AnnualBracket~ HeadOfHousehold
    }

    class AnnualBracket {
        <<sealed>>
        +decimal Over
        +decimal? Under
        +decimal Base
        +decimal Rate
        +decimal ExcessOver
    }

    class WorksheetConstants {
        <<sealed>>
        +Line1GConstants Line1G
    }

    class Line1GConstants {
        <<sealed>>
        +decimal Mfj
        +decimal Other
    }

    Irs15TPercentageCalculator *-- Irs15TRoot
    Irs15TRoot *-- AnnualTables
    Irs15TRoot *-- WorksheetConstants
    AnnualTables *-- AnnualSchedule : Standard
    AnnualTables *-- AnnualSchedule : Step2Checked
    AnnualSchedule *-- "0..*" AnnualBracket
    WorksheetConstants *-- Line1GConstants

    %% ───────────────────────────────────────
    %% FICA Tax
    %% ───────────────────────────────────────

    class FicaCalculator {
        <<sealed>>
        +decimal SocialSecurityRate$
        +decimal MedicareRate$
        +decimal AdditionalMedicareRate$
        +decimal SocialSecurityWageBase
        +decimal AdditionalMedicareEmployerThreshold
        +Calculate(decimal medicareWagesThisPeriod, decimal ytdSsWages, decimal ytdMedicareWages) tuple
    }

    %% ───────────────────────────────────────
    %% Pay Orchestrator
    %% ───────────────────────────────────────

    class PayCalculator {
        <<sealed>>
        -StateCalculatorRegistry _stateRegistry
        -FicaCalculator _fica
        -Irs15TPercentageCalculator _fed
        +PayCalculator(StateCalculatorRegistry stateRegistry, FicaCalculator fica, Irs15TPercentageCalculator fed)
        +Calculate(PaycheckInput input) PaycheckResult
        -RoundMoney(decimal v)$ decimal
    }

    PayCalculator *-- StateCalculatorRegistry
    PayCalculator *-- FicaCalculator
    PayCalculator *-- Irs15TPercentageCalculator
    PayCalculator ..> PaycheckInput : uses
    PayCalculator ..> PaycheckResult : creates

    %% ───────────────────────────────────────
    %% App – ViewModel
    %% ───────────────────────────────────────

    class PickerItem~T~ {
        <<record>>
        +T Value
        +string Text
        +ToString() string
    }

    class CalculatorViewModel {
        -PayCalculator _calc
        -StateCalculatorRegistry _stateRegistry
        +int SelectedInputTab
        +PickerItem~FederalFilingStatus~ SelectedFederalPickerItem
        +PayFrequency Frequency
        +decimal HourlyRate
        +decimal RegularHours
        +decimal OvertimeHours
        +decimal OvertimeMultiplier
        +UsState SelectedState
        +ObservableCollection~StateFieldViewModel~ StateFields
        +decimal PretaxDeductions
        +decimal PosttaxDeductions
        +FederalFilingStatus FederalFilingStatus
        +bool FederalStep2Checked
        +decimal FederalStep3Credits
        +decimal FederalStep4aOtherIncome
        +decimal FederalStep4bDeductions
        +decimal FederalStep4cExtraWithholding
        +PaycheckResult Result
        +ComparisonSnapshot SavedComparison
        +bool IsTab0Visible
        +bool IsTab1Visible
        +bool IsTab2Visible
        +bool IsTab3Visible
        +bool HasSavedComparison
        +bool HasNoSavedComparison
        +decimal NetPayDifference
        +ObservableCollection~PickerItem~ FederalStatuses
        +IReadOnlyList~PayFrequency~ Frequencies
        +IReadOnlyList~UsState~ SupportedStates
        +IRelayCommand SelectTabCommand
        +IRelayCommand CalculateCommand
        +IRelayCommand SaveForCompareCommand
    }

    class ObservableObject {
        <<abstract>>
    }

    CalculatorViewModel --|> ObservableObject
    CalculatorViewModel *-- PayCalculator
    CalculatorViewModel *-- StateCalculatorRegistry
    CalculatorViewModel --> PaycheckResult
    CalculatorViewModel --> ComparisonSnapshot

    class ComparisonSnapshot {
        <<sealed>>
        +PayFrequency Frequency
        +decimal HourlyRate
        +decimal RegularHours
        +decimal OvertimeHours
        +decimal OvertimeMultiplier
        +UsState State
        +decimal PretaxDeductions
        +decimal PosttaxDeductions
        +PaycheckResult Result
    }

    ComparisonSnapshot --> PaycheckResult

    %% ───────────────────────────────────────
    %% App – Views
    %% ───────────────────────────────────────

    class ContentPage {
        <<abstract>>
    }

    class InputsPage {
        +InputsPage(CalculatorViewModel vm)
    }

    class ResultsPage {
        -CalculatorViewModel _vm
        -DoughnutChartDrawable _chartDrawable
        +ResultsPage(CalculatorViewModel vm)
        #OnAppearing() void
        #OnDisappearing() void
        -OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e) void
        -UpdateChart() void
    }

    class ComparePage {
        +ComparePage(CalculatorViewModel vm)
    }

    InputsPage --|> ContentPage
    ResultsPage --|> ContentPage
    ComparePage --|> ContentPage

    InputsPage ..> CalculatorViewModel : binds to
    ResultsPage *-- CalculatorViewModel
    ResultsPage *-- DoughnutChartDrawable
    ComparePage ..> CalculatorViewModel : binds to

    %% ───────────────────────────────────────
    %% App – Controls & Behaviors
    %% ───────────────────────────────────────

    class IDrawable {
        <<interface>>
        +Draw(ICanvas canvas, RectF dirtyRect) void
    }

    class DoughnutChartDrawable {
        <<sealed>>
        -Color[] SliceColors$
        +PaycheckResult Result
        +Draw(ICanvas canvas, RectF dirtyRect) void
        -DrawSlice(ICanvas canvas, float cx, float cy, float outerR, float innerR, float startAngle, float sweepAngle, Color color)$ void
    }

    DoughnutChartDrawable ..|> IDrawable
    DoughnutChartDrawable --> PaycheckResult

    class Behavior~Entry~ {
        <<abstract>>
    }

    class DecimalFormatBehavior {
        +bool IsCurrency
        #OnAttachedTo(Entry entry) void
        #OnDetachingFrom(Entry entry) void
        -OnFocused(object sender, FocusEventArgs e) void
        -OnUnfocused(object sender, FocusEventArgs e) void
    }

    DecimalFormatBehavior --|> Behavior~Entry~

    %% ───────────────────────────────────────
    %% App – Helpers & Shell
    %% ───────────────────────────────────────

    class EnumDisplay {
        <<static>>
        +PayFrequency(string name)$ string
        +FederalFilingStatus(string name)$ string
        -SplitPascalCase(string s)$ string
    }

    class Shell {
        <<abstract>>
    }

    class AppShell {
        +AppShell()
    }

    AppShell --|> Shell

    class Application {
        <<abstract>>
    }

    class App {
        -AppShell _shell
        +App(AppShell shell)
        #CreateWindow(IActivationState activationState) Window
    }

    App --|> Application
    App *-- AppShell

    %% ───────────────────────────────────────
    %% App – DI Composition Root
    %% ───────────────────────────────────────

    class MauiProgram {
        <<static>>
        +CreateMauiApp()$ MauiApp
    }

    MauiProgram ..> PayCalculator : registers
    MauiProgram ..> CalculatorViewModel : registers
    MauiProgram ..> StateCalculatorRegistry : registers
    MauiProgram ..> FicaCalculator : registers
    MauiProgram ..> Irs15TPercentageCalculator : registers
    MauiProgram ..> OklahomaOw2PercentageCalculator : registers
    MauiProgram ..> InputsPage : registers
    MauiProgram ..> ResultsPage : registers
    MauiProgram ..> ComparePage : registers
```
