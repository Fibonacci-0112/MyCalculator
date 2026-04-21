// Types mirroring the PaycheckCalc.Api DTOs. Kept in sync manually with
// PaycheckCalc.Api/Dtos/*. Keeping the shape identical to the server DTOs
// lets us send/receive with no per-field mapping on the client.

export type StateFieldType = 'Text' | 'Integer' | 'Decimal' | 'Toggle' | 'Picker';

export interface StateFieldDefinition {
  key: string;
  label: string;
  fieldType: StateFieldType;
  isRequired: boolean;
  defaultValue: unknown;
  options: string[] | null;
}

export type PayFrequency =
  | 'Weekly'
  | 'Biweekly'
  | 'Semimonthly'
  | 'Monthly'
  | 'Quarterly'
  | 'Semiannual'
  | 'Annual'
  | 'Daily';

export type FederalFilingStatus =
  | 'SingleOrMarriedSeparately'
  | 'MarriedFilingJointly'
  | 'HeadOfHousehold';

export type DeductionType = 'PreTax' | 'PostTax';
export type DeductionAmountType = 'Dollar' | 'Percentage';

export interface Deduction {
  name: string;
  type: DeductionType;
  amount: number;
  amountType: DeductionAmountType;
  reducesStateTaxableWages: boolean;
}

export interface FederalW4 {
  filingStatus: FederalFilingStatus;
  step2Checked: boolean;
  step3TaxCredits: number;
  step4aOtherIncome: number;
  step4bDeductions: number;
  step4cExtraWithholding: number;
}

export interface PaycheckInput {
  frequency: PayFrequency;
  hourlyRate: number;
  regularHours: number;
  overtimeHours: number;
  overtimeMultiplier: number;
  state: string;
  stateInputValues: Record<string, unknown>;
  federalW4: FederalW4;
  deductions: Deduction[];
  ytdSocialSecurityWages: number;
  ytdMedicareWages: number;
  paycheckNumber: number;
}

export interface LocalBreakdownLine {
  localityCode: string;
  localityName: string;
  taxableWages: number;
  withholding: number;
  headTax: number;
  headTaxLabel: string;
  description: string | null;
}

export interface PaycheckResult {
  grossPay: number;
  preTaxDeductions: number;
  postTaxDeductions: number;
  state: string;
  stateTaxableWages: number;
  stateWithholding: number;
  stateDisabilityInsurance: number;
  stateDisabilityInsuranceLabel: string;
  socialSecurityWithholding: number;
  medicareWithholding: number;
  additionalMedicareWithholding: number;
  federalTaxableIncome: number;
  federalWithholding: number;
  localTaxableWages: number;
  localWithholding: number;
  localHeadTax: number;
  localHeadTaxLabel: string;
  localityLabel: string;
  localBreakdown: LocalBreakdownLine[];
  totalTaxes: number;
  netPay: number;
}
