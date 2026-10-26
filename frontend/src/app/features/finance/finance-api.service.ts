import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { API_BASE_URL } from '../../core/http/api-base-url.token';
export interface Api<T>{data:T;succeeded:boolean;errors:string[]}
export interface Setup{automaticPostingEnabled:boolean;cutoverDate:string|null;mandatoryMappingsApproved:boolean;openingBalancesApproved:boolean;reconciliationReady:boolean;missingRequiredMappings:number;mode:string}
export interface Account{id:string;code:string;name:string;type:number;normalBalance:number;kind:number;parentId:string|null;isActive:boolean;isSystemControlled:boolean;hasPostings:boolean}
export interface FinancialAccount{id:string;code:string;name:string;type:number;accountCode:string;maskedReference:string|null;bookBalance:number;isActive:boolean}
export interface Mapping{id:string;type:number;key:string;accountCode:string;paymentMethodId:string|null;expenseCategoryId:string|null;isActive:boolean}
export interface Journal{id:string;number:string;businessDate:string;sourceType:string;postingKind:string;description:string;debit:number;credit:number;reversesJournalId:string|null}
export interface Dashboard{cash:number;bank:number;wallet:number;cardClearing:number;receivables:number;payables:number;inventory:number;revenue:number;cogs:number;grossProfit:number;expenses:number;netProfit:number;provisional:boolean}
export interface OpeningPosition{inventoryQuantity:number;inventoryValue:number;customerReceivables:number;supplierPayables:number;journalDebits:number;journalCredits:number;zeroOpeningValid:boolean}
export interface Readiness{accountsReady:boolean;systemMappingsReady:boolean;paymentMappingsReady:boolean;expenseMappingsReady:boolean;financialAccountsReady:boolean;fiscalPeriodReady:boolean;openingBalancesReady:boolean;reconciliationReady:boolean;journalsBalanced:boolean;canActivate:boolean;blockers:string[];positions:OpeningPosition}
export interface FiscalPeriod{id:string;name:string;startDate:string;endDate:string;status:number}
@Injectable({providedIn:'root'}) export class FinanceApiService{
 private http=inject(HttpClient);private base=`${inject(API_BASE_URL)}/finance`;
 setup(){return this.http.get<Api<Setup>>(`${this.base}/setup`)} accounts(search=''){return this.http.get<Api<Account[]>>(`${this.base}/accounts`,{params:{search}})}
 openingAssessment(){return this.http.get<Api<OpeningPosition>>(`${this.base}/setup/opening-assessment`)} readiness(cutover:string){return this.http.get<Api<Readiness>>(`${this.base}/setup/readiness`,{params:{cutover}})}
 saveAccount(value:unknown,id?:string){return id?this.http.put<Api<Account>>(`${this.base}/accounts/${id}`,value):this.http.post<Api<Account>>(`${this.base}/accounts`,value)}
 financial(){return this.http.get<Api<FinancialAccount[]>>(`${this.base}/financial-accounts`)} mappings(){return this.http.get<Api<Mapping[]>>(`${this.base}/mappings`)}
 saveMapping(value:unknown){return this.http.post<Api<Mapping>>(`${this.base}/mappings`,value)}
 journals(search=''){return this.http.get<Api<{items:Journal[]}>>(`${this.base}/journals`,{params:new HttpParams().set('search',search)})}
 periods(){return this.http.get<Api<FiscalPeriod[]>>(`${this.base}/periods`)} createPeriod(v:unknown){return this.http.post<Api<FiscalPeriod>>(`${this.base}/periods`,v)}
 saveDraft(v:unknown){return this.http.post<Api<string>>(`${this.base}/manual-journals`,v)} postDraft(id:string){return this.http.post<Api<any>>(`${this.base}/manual-journals/${id}/post`,{})}
 transfer(v:unknown){return this.http.post<Api<string>>(`${this.base}/transfers`,v)} reconcile(v:unknown){return this.http.post<Api<string>>(`${this.base}/reconciliations`,v)}
 report<T>(name:string,params:Record<string,string>){return this.http.get<Api<T>>(`${this.base}/reports/${name}`,{params})} downloadTrialBalance(from:string,to:string){return this.http.get(`${this.base}/reports/trial-balance/export`,{params:{from,to},responseType:'blob'})}
}
