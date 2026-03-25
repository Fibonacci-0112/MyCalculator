# UML Class Diagram

> High-level Mermaid class diagram for the **PaycheckCalc** solution.
> Render with any Mermaid-compatible viewer (GitHub markdown, VS Code extension, etc.).

```mermaid
classDiagram
    direction TB

    %% ═══════════════════════════════════════
    %% Core Models – Enums
    %% ═══════════════════════════════════════

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

    class DeductionAmountType {
        <<enumeration>>
        Dollar
        Percentage
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

    class CaliforniaFilingStatus {
        <<enumeration>>
        Single
        Married
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

    %% ═══════════════════════════════════════
    %% Core Models – Data Classes
    %% ═══════════════════════════════════════

    class PaycheckInput {
        <<sealed>>
        +PayFrequency Frequency
        +FilingStatus FilingStatus
        +decimal HourlyRate
        +decimal RegularHours
        +decimal OvertimeHours
        +decimal OvertimeMultiplier
        +UsState State
        +StateInputValues StateInputs
        +int StateAllowances
        +decimal StateAdditionalWithholding
        +FederalW4Input FederalW4
        +IReadOnlyList~Deduction~ Deductions
        +decimal YtdSocialSecurityWages
        +decimal YtdMedicareWages
        +int PaycheckNumber
    }

    class PaycheckResult {
        <<sealed>>
        +decimal GrossPay
        +decimal PreTaxDeductions
        +decimal PostTaxDeductions
        +UsState State
        +decimal StateTaxableWages
        +decimal StateWithholding
        +decimal StateDisabilityInsurance
        +string StateDisabilityInsuranceLabel
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
        +DeductionAmountType AmountType
        +decimal Amount
        +bool ReducesStateTaxableWages
        +EffectiveAmount(decimal grossPay) decimal
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

    class CalculationScenario {
        <<sealed>>
        +PaycheckInput Input
        +PaycheckResult Result
    }

    class AnnualProjection {
        <<sealed>>
        +int PayPeriodsPerYear
        +int CurrentPaycheckNumber
        +int RemainingPaychecks
        +decimal AnnualizedGrossPay
        +decimal AnnualizedFederalWithholding
        +decimal AnnualizedStateWithholding
        +decimal AnnualizedFica
        +decimal AnnualizedNetPay
        +decimal ProjectedYtdGrossPay
        +decimal ProjectedYtdNetPay
        +decimal EstimatedAnnualFederalLiability
        +decimal EstimatedAnnualFicaLiability
        +decimal AnnualizedTotalWithholding
        +decimal EstimatedTotalLiability
        +decimal OverUnderWithholding
    }

    PaycheckInput *-- FederalW4Input
    PaycheckInput *-- "0..*" Deduction
    PaycheckInput *-- StateInputValues
    Deduction --> DeductionType
    Deduction --> DeductionAmountType
    PaycheckInput --> PayFrequency
    PaycheckInput --> FilingStatus
    PaycheckInput --> UsState
    PaycheckResult --> UsState
    FederalW4Input --> FederalFilingStatus
    CalculationScenario *-- PaycheckInput
    CalculationScenario *-- PaycheckResult

    %% ═══════════════════════════════════════
    %% State Tax – Interface & Core Types
    %% ═══════════════════════════════════════

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
        +decimal FederalWithholdingPerPeriod
    }

    class StateInputValues {
        <<sealed>>
        +StateInputValues()
        +StateInputValues(IDictionary source)
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
        +decimal DisabilityInsurance
        +string DisabilityInsuranceLabel
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

    IStateWithholdingCalculator --> CommonWithholdingContext
    IStateWithholdingCalculator --> StateInputValues
    IStateWithholdingCalculator --> StateWithholdingResult
    IStateWithholdingCalculator --> StateFieldDefinition
    StateFieldDefinition --> StateFieldType
    CommonWithholdingContext --> PayFrequency

    %% ═══════════════════════════════════════
    %% State Tax – Registry & Generic Adapters
    %% ═══════════════════════════════════════

    class StateCalculatorRegistry {
        <<sealed>>
        -Dictionary~UsState, IStateWithholdingCalculator~ _calculators
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
        +Calculate(CommonWithholdingContext ctx, StateInputValues vals) StateWithholdingResult
    }

    class PercentageMethodWithholdingAdapter {
        <<sealed>>
        -PercentageMethodStateTaxCalculator _inner
        +UsState State
        +GetInputSchema() IReadOnlyList~StateFieldDefinition~
        +Validate(StateInputValues values) IReadOnlyList~string~
        +Calculate(CommonWithholdingContext ctx, StateInputValues vals) StateWithholdingResult
    }

    class PercentageMethodStateTaxCalculator {
        <<sealed>>
        -PercentageMethodConfig _config
        +UsState State
        +CalculateWithholding(StateTaxInput input) StateTaxResult
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

    NoIncomeTaxWithholdingAdapter ..|> IStateWithholdingCalculator
    PercentageMethodWithholdingAdapter ..|> IStateWithholdingCalculator
    PercentageMethodWithholdingAdapter *-- PercentageMethodStateTaxCalculator
    PercentageMethodStateTaxCalculator *-- PercentageMethodConfig
    PercentageMethodStateTaxCalculator --> StateTaxInput
    PercentageMethodStateTaxCalculator --> StateTaxResult
    PercentageMethodConfig *-- "0..*" TaxBracket
    StateCalculatorRegistry o-- "0..*" IStateWithholdingCalculator
    StateTaxConfigs2026 ..> PercentageMethodConfig : provides

    %% ═══════════════════════════════════════
    %% State Tax – Alabama
    %% ═══════════════════════════════════════

    class AlabamaWithholdingCalculator {
        <<sealed>>
        +UsState State
        +GetInputSchema() IReadOnlyList~StateFieldDefinition~
        +Validate(StateInputValues values) IReadOnlyList~string~
        +Calculate(CommonWithholdingContext ctx, StateInputValues vals) StateWithholdingResult
    }

    class AlabamaFormulaCalculator {
        +CalculateWithholding(decimal grossWages, int payPeriods, decimal federalWithholding, AlabamaFilingStatus status, int dependents)$ decimal
    }

    AlabamaWithholdingCalculator ..|> IStateWithholdingCalculator
    AlabamaWithholdingCalculator ..> AlabamaFormulaCalculator
    AlabamaFormulaCalculator --> AlabamaFilingStatus

    %% ═══════════════════════════════════════
    %% State Tax – Arkansas
    %% ═══════════════════════════════════════

    class ArkansasWithholdingCalculator {
        <<sealed>>
        -ArkansasFormulaCalculator _inner
        +UsState State
        +ArkansasWithholdingCalculator(ArkansasFormulaCalculator inner)
        +GetInputSchema() IReadOnlyList~StateFieldDefinition~
        +Validate(StateInputValues values) IReadOnlyList~string~
        +Calculate(CommonWithholdingContext ctx, StateInputValues vals) StateWithholdingResult
    }

    class ArkansasFormulaCalculator {
        <<sealed>>
        +ArkansasFormulaCalculator(string json)
        +CalculateWithholding(decimal grossWages, int payPeriods, int exemptions) decimal
        +RoundToNearest50(decimal amount)$ decimal
    }

    ArkansasWithholdingCalculator ..|> IStateWithholdingCalculator
    ArkansasWithholdingCalculator *-- ArkansasFormulaCalculator

    %% ═══════════════════════════════════════
    %% State Tax – California
    %% ═══════════════════════════════════════

    class CaliforniaWithholdingCalculator {
        <<sealed>>
        -CaliforniaPercentageCalculator _inner
        -decimal SdiRate$
        +UsState State
        +CaliforniaWithholdingCalculator(CaliforniaPercentageCalculator inner)
        +GetInputSchema() IReadOnlyList~StateFieldDefinition~
        +Validate(StateInputValues values) IReadOnlyList~string~
        +Calculate(CommonWithholdingContext ctx, StateInputValues vals) StateWithholdingResult
    }

    class CaliforniaPercentageCalculator {
        <<sealed>>
        +CaliforniaPercentageCalculator(string json)
        +CalculateWithholding(decimal grossPay, PayFrequency freq, CaliforniaFilingStatus status, int regularAllowances, int estimatedDeductionAllowances) decimal
    }

    CaliforniaWithholdingCalculator ..|> IStateWithholdingCalculator
    CaliforniaWithholdingCalculator *-- CaliforniaPercentageCalculator
    CaliforniaPercentageCalculator --> CaliforniaFilingStatus

    %% ═══════════════════════════════════════
    %% State Tax – Colorado
    %% ═══════════════════════════════════════

    class ColoradoWithholdingCalculator {
        <<sealed>>
        -decimal FlatRate$
        -decimal FmliRate$
        +UsState State
        +ColoradoWithholdingCalculator(string json)
        +GetInputSchema() IReadOnlyList~StateFieldDefinition~
        +Validate(StateInputValues values) IReadOnlyList~string~
        +Calculate(CommonWithholdingContext ctx, StateInputValues vals) StateWithholdingResult
    }

    ColoradoWithholdingCalculator ..|> IStateWithholdingCalculator

    %% ═══════════════════════════════════════
    %% State Tax – Connecticut
    %% ═══════════════════════════════════════

    class ConnecticutWithholdingCalculator {
        <<sealed>>
        -decimal NoFormFlatRate$
        -decimal PfmliRate$
        +UsState State
        +ConnecticutWithholdingCalculator(string json)
        +GetInputSchema() IReadOnlyList~StateFieldDefinition~
        +Validate(StateInputValues values) IReadOnlyList~string~
        +Calculate(CommonWithholdingContext ctx, StateInputValues vals) StateWithholdingResult
    }

    ConnecticutWithholdingCalculator ..|> IStateWithholdingCalculator

    %% ═══════════════════════════════════════
    %% State Tax – Oklahoma
    %% ═══════════════════════════════════════

    class OklahomaWithholdingCalculator {
        <<sealed>>
        -OklahomaOw2PercentageCalculator _inner
        +UsState State
        +OklahomaWithholdingCalculator(OklahomaOw2PercentageCalculator inner)
        +GetInputSchema() IReadOnlyList~StateFieldDefinition~
        +Validate(StateInputValues values) IReadOnlyList~string~
        +Calculate(CommonWithholdingContext ctx, StateInputValues vals) StateWithholdingResult
    }

    class OklahomaOw2PercentageCalculator {
        <<sealed>>
        -Ow2Root _data
        +OklahomaOw2PercentageCalculator(string json)
        +GetAllowanceAmount(PayFrequency frequency) decimal
        +CalculateWithholding(decimal wages, PayFrequency freq, FilingStatus status) decimal
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

    %% ═══════════════════════════════════════
    %% State Tax – Pennsylvania
    %% ═══════════════════════════════════════

    class PennsylvaniaWithholdingCalculator {
        <<sealed>>
        -decimal FlatRate$
        +UsState State
        +GetInputSchema() IReadOnlyList~StateFieldDefinition~
        +Validate(StateInputValues values) IReadOnlyList~string~
        +Calculate(CommonWithholdingContext ctx, StateInputValues vals) StateWithholdingResult
    }

    PennsylvaniaWithholdingCalculator ..|> IStateWithholdingCalculator

    %% ═══════════════════════════════════════
    %% Federal Tax
    %% ═══════════════════════════════════════

    class Irs15TPercentageCalculator {
        <<sealed>>
        -Irs15TRoot _data
        +Irs15TPercentageCalculator(string json)
        +CalculateWithholding(decimal taxableWages, PayFrequency freq, FederalW4Input w4) decimal
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

    %% ═══════════════════════════════════════
    %% FICA Tax
    %% ═══════════════════════════════════════

    class FicaCalculator {
        <<sealed>>
        +decimal SocialSecurityRate$
        +decimal MedicareRate$
        +decimal AdditionalMedicareRate$
        +decimal SocialSecurityWageBase
        +decimal AdditionalMedicareEmployerThreshold
        +Calculate(decimal wagesThisPeriod, decimal ytdSsWages, decimal ytdMedicareWages) tuple
    }

    %% ═══════════════════════════════════════
    %% Pay Orchestrator & Annual Projection
    %% ═══════════════════════════════════════

    class PayCalculator {
        <<sealed>>
        -StateCalculatorRegistry _stateRegistry
        -FicaCalculator _fica
        -Irs15TPercentageCalculator _fed
        +PayCalculator(StateCalculatorRegistry stateRegistry, FicaCalculator fica, Irs15TPercentageCalculator fed)
        +Calculate(PaycheckInput input) PaycheckResult
    }

    class AnnualProjectionCalculator {
        <<sealed>>
        -Irs15TPercentageCalculator _fed
        -FicaCalculator _fica
        +AnnualProjectionCalculator(Irs15TPercentageCalculator fed, FicaCalculator fica)
        +Calculate(PaycheckInput input, PaycheckResult result) AnnualProjection
    }

    PayCalculator *-- StateCalculatorRegistry
    PayCalculator *-- FicaCalculator
    PayCalculator *-- Irs15TPercentageCalculator
    PayCalculator ..> PaycheckInput : uses
    PayCalculator ..> PaycheckResult : creates

    AnnualProjectionCalculator *-- Irs15TPercentageCalculator
    AnnualProjectionCalculator *-- FicaCalculator
    AnnualProjectionCalculator ..> PaycheckInput : uses
    AnnualProjectionCalculator ..> PaycheckResult : uses
    AnnualProjectionCalculator ..> AnnualProjection : creates

    %% ═══════════════════════════════════════
    %% Export
    %% ═══════════════════════════════════════

    class CsvPaycheckExporter {
        <<static>>
        +Generate(PaycheckResult result)$ string
    }

    class PdfPaycheckExporter {
        <<static>>
        +Generate(PaycheckResult result)$ byte[]
    }

    CsvPaycheckExporter ..> PaycheckResult : reads
    PdfPaycheckExporter ..> PaycheckResult : reads

    %% ═══════════════════════════════════════
    %% App – ViewModels
    %% ═══════════════════════════════════════

    class ObservableObject {
        <<abstract>>
    }

    class PickerItem~T~ {
        <<record>>
        +T Value
        +string Text
        +ToString() string
    }

    class CalculatorViewModel {
        <<partial>>
        -PayCalculator _calc
        -AnnualProjectionCalculator _projectionCalc
        -StateCalculatorRegistry _stateRegistry
        +int SelectedInputTab
        +int SelectedResultTab
        +PickerItem~FederalFilingStatus~ SelectedFederalPickerItem
        +PickerItem~PayFrequency~ SelectedFrequencyPickerItem
        +PickerItem~UsState~ SelectedStatePickerItem
        +PayFrequency Frequency
        +decimal HourlyRate
        +decimal RegularHours
        +decimal OvertimeHours
        +decimal OvertimeMultiplier
        +int PaycheckNumber
        +UsState SelectedState
        +FederalFilingStatus FederalFilingStatus
        +bool FederalStep2Checked
        +decimal FederalStep3Credits
        +decimal FederalStep4aOtherIncome
        +decimal FederalStep4bDeductions
        +decimal FederalStep4cExtraWithholding
        +ObservableCollection~StateFieldViewModel~ StateFields
        +ObservableCollection~DeductionItemViewModel~ Deductions
        +ObservableCollection~string~ StateValidationErrors
        +ResultCardModel ResultCard
        +AnnualProjectionModel Projection
        +ScenarioSnapshot SavedScenario
        +bool CanExport
        +bool HasSavedComparison
        +decimal NetPayDifference
        +IReadOnlyList~PickerItem~ Frequencies
        +IReadOnlyList~PickerItem~ StatePickerItems
        +SelectTabCommand() void
        +SelectResultTabCommand() void
        +CalculateCommand() void
        +SaveForCompareCommand() void
        +AddDeductionCommand() void
        +RemoveDeductionCommand() void
        +ExportCsvCommand() Task
        +ExportPdfCommand() Task
    }

    class StateFieldViewModel {
        <<partial>>
        +StateFieldDefinition Definition
        +string Key
        +string Label
        +IReadOnlyList~string~ Options
        +bool IsPicker
        +bool IsText
        +bool IsNumeric
        +bool IsToggle
        +bool IsCurrency
        +string SelectedOption
        +string StringValue
        +bool BoolValue
        +string ErrorMessage
        +bool HasError
        +Validate() void
        +GetResolvedValue() object
    }

    class DeductionItemViewModel {
        <<partial>>
        +string Name
        +decimal Amount
        +DeductionAmountType AmountType
        +bool ReducesStateTaxableWages
        +bool IsPercentageAmount
        +bool IsDollarAmount
        +DeductionType Type
        +PickerItem~DeductionType~ SelectedDeductionTypePickerItem
        +IReadOnlyList~PickerItem~ DeductionTypeItems
        +IReadOnlyList~DeductionAmountType~ AmountTypes
        +ToDeduction() Deduction
    }

    CalculatorViewModel --|> ObservableObject
    StateFieldViewModel --|> ObservableObject
    DeductionItemViewModel --|> ObservableObject

    CalculatorViewModel *-- PayCalculator
    CalculatorViewModel *-- AnnualProjectionCalculator
    CalculatorViewModel *-- StateCalculatorRegistry
    CalculatorViewModel *-- "0..*" StateFieldViewModel
    CalculatorViewModel *-- "0..*" DeductionItemViewModel
    CalculatorViewModel --> ResultCardModel
    CalculatorViewModel --> AnnualProjectionModel
    CalculatorViewModel --> ScenarioSnapshot

    StateFieldViewModel --> StateFieldDefinition
    DeductionItemViewModel --> DeductionType
    DeductionItemViewModel --> DeductionAmountType
    DeductionItemViewModel ..> Deduction : creates

    %% ═══════════════════════════════════════
    %% App – Presentation Models
    %% ═══════════════════════════════════════

    class ResultCardModel {
        <<sealed>>
        +decimal GrossPay
        +decimal FederalTaxableIncome
        +decimal StateTaxableWages
        +decimal FederalWithholding
        +decimal SocialSecurityWithholding
        +decimal MedicareWithholding
        +decimal AdditionalMedicareWithholding
        +decimal StateWithholding
        +decimal StateDisabilityInsurance
        +string StateDisabilityInsuranceLabel
        +string StateName
        +decimal PreTaxDeductions
        +decimal PostTaxDeductions
        +decimal TotalTaxes
        +decimal NetPay
        +bool ShowStateDisabilityInsurance
    }

    class AnnualProjectionModel {
        <<sealed>>
        +int PayPeriodsPerYear
        +int CurrentPaycheckNumber
        +int RemainingPaychecks
        +decimal AnnualizedGrossPay
        +decimal AnnualizedFederalWithholding
        +decimal AnnualizedStateWithholding
        +decimal AnnualizedFica
        +decimal AnnualizedNetPay
        +decimal ProjectedYtdGrossPay
        +decimal ProjectedYtdNetPay
        +decimal EstimatedAnnualFederalLiability
        +decimal EstimatedAnnualFicaLiability
        +decimal AnnualizedTotalWithholding
        +decimal EstimatedTotalLiability
        +decimal OverUnderWithholding
        +bool IsOverWithholding
        +bool IsUnderWithholding
        +decimal OverUnderAmount
        +string OverUnderLabel
    }

    class ScenarioSnapshot {
        <<sealed>>
        +PayFrequency Frequency
        +decimal HourlyRate
        +decimal RegularHours
        +decimal OvertimeHours
        +decimal OvertimeMultiplier
        +UsState State
        +decimal PretaxDeductions
        +decimal PosttaxDeductions
        +ResultCardModel ResultCard
    }

    ScenarioSnapshot --> ResultCardModel

    %% ═══════════════════════════════════════
    %% App – Mappers
    %% ═══════════════════════════════════════

    class PaycheckInputMapper {
        <<static>>
        +Map(CalculatorViewModel vm, StateInputValues stateValues)$ PaycheckInput
    }

    class ResultCardMapper {
        <<static>>
        +Map(PaycheckResult result)$ ResultCardModel
    }

    class AnnualProjectionMapper {
        <<static>>
        +Map(AnnualProjection projection)$ AnnualProjectionModel
    }

    class ScenarioMapper {
        <<static>>
        +Capture(CalculatorViewModel vm)$ ScenarioSnapshot
    }

    PaycheckInputMapper ..> CalculatorViewModel : reads
    PaycheckInputMapper ..> PaycheckInput : creates
    ResultCardMapper ..> PaycheckResult : reads
    ResultCardMapper ..> ResultCardModel : creates
    AnnualProjectionMapper ..> AnnualProjection : reads
    AnnualProjectionMapper ..> AnnualProjectionModel : creates
    ScenarioMapper ..> CalculatorViewModel : reads
    ScenarioMapper ..> ScenarioSnapshot : creates

    %% ═══════════════════════════════════════
    %% App – Views
    %% ═══════════════════════════════════════

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
    }

    class ComparePage {
        +ComparePage(CalculatorViewModel vm)
    }

    InputsPage --|> ContentPage
    ResultsPage --|> ContentPage
    ComparePage --|> ContentPage

    InputsPage ..> CalculatorViewModel : binds to
    ResultsPage *-- DoughnutChartDrawable
    ResultsPage ..> CalculatorViewModel : binds to
    ComparePage ..> CalculatorViewModel : binds to

    %% ═══════════════════════════════════════
    %% App – Controls & Behaviors
    %% ═══════════════════════════════════════

    class IDrawable {
        <<interface>>
        +Draw(ICanvas canvas, RectF dirtyRect) void
    }

    class DoughnutChartDrawable {
        <<sealed>>
        -Color[] SliceColors$
        +ResultCardModel Result
        +Draw(ICanvas canvas, RectF dirtyRect) void
    }

    DoughnutChartDrawable ..|> IDrawable
    DoughnutChartDrawable --> ResultCardModel

    class Behavior~Entry~ {
        <<abstract>>
    }

    class DecimalFormatBehavior {
        +bool IsCurrency
        +bool IsPercentage
        #OnAttachedTo(Entry entry) void
        #OnDetachingFrom(Entry entry) void
    }

    DecimalFormatBehavior --|> Behavior~Entry~

    %% ═══════════════════════════════════════
    %% App – Helpers & Shell
    %% ═══════════════════════════════════════

    class EnumDisplay {
        <<static>>
        +DeductionType(string name)$ string
        +PayFrequency(string name)$ string
        +FederalFilingStatus(string name)$ string
        +UsStateName(string abbreviation)$ string
    }

    class GreaterThanZeroConverter {
        <<sealed>>
        +Convert(object value, Type targetType, object parameter, CultureInfo culture) object
        +ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) object
    }

    class IValueConverter {
        <<interface>>
    }

    GreaterThanZeroConverter ..|> IValueConverter

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

    %% ═══════════════════════════════════════
    %% App – DI Composition Root
    %% ═══════════════════════════════════════

    class MauiProgram {
        <<static>>
        +CreateMauiApp()$ MauiApp
    }

    MauiProgram ..> PayCalculator : registers
    MauiProgram ..> AnnualProjectionCalculator : registers
    MauiProgram ..> CalculatorViewModel : registers
    MauiProgram ..> StateCalculatorRegistry : registers
    MauiProgram ..> FicaCalculator : registers
    MauiProgram ..> Irs15TPercentageCalculator : registers
    MauiProgram ..> ArkansasFormulaCalculator : registers
    MauiProgram ..> CaliforniaPercentageCalculator : registers
    MauiProgram ..> ColoradoWithholdingCalculator : registers
    MauiProgram ..> ConnecticutWithholdingCalculator : registers
    MauiProgram ..> OklahomaOw2PercentageCalculator : registers
    MauiProgram ..> InputsPage : registers
    MauiProgram ..> ResultsPage : registers
    MauiProgram ..> ComparePage : registers
```
