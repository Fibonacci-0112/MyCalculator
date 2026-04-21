import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  PaycheckInput,
  PaycheckResult,
  StateFieldDefinition,
} from './paycheck.models';

/**
 * Thin, typed wrapper around the PaycheckCalc.Api backend. The base URL is
 * configurable via an environment file or overridden at build time; the
 * default matches the API's dev `launchSettings.json` (http://localhost:5200).
 */
@Injectable({ providedIn: 'root' })
export class PaycheckApiService {
  private readonly http = inject(HttpClient);

  // Default points at the API dev server. For production the same origin
  // would typically serve both the SPA and the API; the URL can be replaced
  // with a relative path ('') in that case.
  private readonly baseUrl = 'http://localhost:5200';

  getStates(): Observable<string[]> {
    return this.http.get<string[]>(`${this.baseUrl}/api/reference/states`);
  }

  getPayFrequencies(): Observable<string[]> {
    return this.http.get<string[]>(
      `${this.baseUrl}/api/reference/pay-frequencies`,
    );
  }

  getFederalFilingStatuses(): Observable<string[]> {
    return this.http.get<string[]>(
      `${this.baseUrl}/api/reference/federal-filing-statuses`,
    );
  }

  getStateSchema(state: string): Observable<StateFieldDefinition[]> {
    return this.http.get<StateFieldDefinition[]>(
      `${this.baseUrl}/api/reference/states/${state}/schema`,
    );
  }

  calculate(input: PaycheckInput): Observable<PaycheckResult> {
    return this.http.post<PaycheckResult>(
      `${this.baseUrl}/api/paycheck/calculate`,
      input,
    );
  }
}
