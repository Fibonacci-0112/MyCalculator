import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { PaycheckApiService } from './paycheck-api.service';
import {
  Deduction,
  PaycheckInput,
  PaycheckResult,
  StateFieldDefinition,
} from './paycheck.models';

/**
 * Single-page calculator that talks to the PaycheckCalc.Api backend, which
 * in turn delegates all payroll math to PaycheckCalc.Core (the same engine
 * used by the MAUI and Blazor heads).
 */
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css',
})
export class AppComponent implements OnInit {
  private readonly api = inject(PaycheckApiService);

  readonly title = 'PaycheckCalc — Angular';

  // Reference data populated from the API.
  readonly states = signal<string[]>([]);
  readonly frequencies = signal<string[]>([]);
  readonly filingStatuses = signal<string[]>([]);

  // Schema for the currently selected state.
  readonly stateSchema = signal<StateFieldDefinition[]>([]);

  // Calculation result + status flags.
  readonly result = signal<PaycheckResult | null>(null);
  readonly errorMessage = signal<string | null>(null);
  readonly isCalculating = signal(false);

  // Form state.
  readonly input: PaycheckInput = {
    frequency: 'Biweekly',
    hourlyRate: 50,
    regularHours: 80,
    overtimeHours: 0,
    overtimeMultiplier: 1.5,
    state: 'OK',
    stateInputValues: {},
    federalW4: {
      filingStatus: 'SingleOrMarriedSeparately',
      step2Checked: false,
      step3TaxCredits: 0,
      step4aOtherIncome: 0,
      step4bDeductions: 0,
      step4cExtraWithholding: 0,
    },
    deductions: [],
    ytdSocialSecurityWages: 0,
    ytdMedicareWages: 0,
    paycheckNumber: 1,
  };

  async ngOnInit(): Promise<void> {
    try {
      const [states, frequencies, filingStatuses] = await Promise.all([
        firstValueFrom(this.api.getStates()),
        firstValueFrom(this.api.getPayFrequencies()),
        firstValueFrom(this.api.getFederalFilingStatuses()),
      ]);
      this.states.set(states);
      this.frequencies.set(frequencies);
      this.filingStatuses.set(filingStatuses);
      await this.loadStateSchema(this.input.state);
    } catch (err) {
      this.errorMessage.set(this.describeError(err));
    }
  }

  /**
   * Loads the dynamic field schema for the selected state and seeds
   * `stateInputValues` with default values so the bound inputs render
   * without `undefined`.
   */
  async onStateChange(state: string): Promise<void> {
    this.input.state = state;
    await this.loadStateSchema(state);
  }

  private async loadStateSchema(state: string): Promise<void> {
    try {
      const schema = await firstValueFrom(this.api.getStateSchema(state));
      this.stateSchema.set(schema);
      const values: Record<string, unknown> = {};
      for (const field of schema) {
        values[field.key] = this.coerceDefault(field);
      }
      this.input.stateInputValues = values;
    } catch (err) {
      this.errorMessage.set(this.describeError(err));
    }
  }

  private coerceDefault(field: StateFieldDefinition): unknown {
    if (field.defaultValue !== null && field.defaultValue !== undefined) {
      return field.defaultValue;
    }
    switch (field.fieldType) {
      case 'Integer':
      case 'Decimal':
        return 0;
      case 'Toggle':
        return false;
      case 'Picker':
        return field.options?.[0] ?? '';
      default:
        return '';
    }
  }

  addDeduction(): void {
    const deduction: Deduction = {
      name: '',
      type: 'PreTax',
      amount: 0,
      amountType: 'Dollar',
      reducesStateTaxableWages: true,
    };
    this.input.deductions = [...this.input.deductions, deduction];
  }

  removeDeduction(index: number): void {
    this.input.deductions = this.input.deductions.filter((_, i) => i !== index);
  }

  trackByIndex(index: number): number {
    return index;
  }

  async calculate(): Promise<void> {
    this.errorMessage.set(null);
    this.isCalculating.set(true);
    try {
      const result = await firstValueFrom(this.api.calculate(this.input));
      this.result.set(result);
    } catch (err) {
      this.result.set(null);
      this.errorMessage.set(this.describeError(err));
    } finally {
      this.isCalculating.set(false);
    }
  }

  private describeError(err: unknown): string {
    if (err && typeof err === 'object' && 'message' in err) {
      return String((err as { message: unknown }).message);
    }
    return 'Unknown error contacting the API.';
  }
}
