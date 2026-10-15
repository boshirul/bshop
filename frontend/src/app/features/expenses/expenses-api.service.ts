import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map } from 'rxjs';
import { API_BASE_URL } from '../../core/http/api-base-url.token';
import { ApiResponse } from '../../core/auth/auth.models';

export interface ExpenseCategory { id:string; code:string; name:string; isActive:boolean; }
export interface ExpenseLine { id?:string; categoryId:string; categoryName?:string; description:string; amount:number; reference?:string|null; }
export interface ExpenseVoucher { id:string; voucherNumber:string; expenseDate:string; payee:string; paymentMethodId?:string|null; paymentMethodName?:string|null; generalRemarks?:string|null; status:string; totalAmount:number; lines:ExpenseLine[]; }
export interface Paged<T>{items:T[];page:number;pageSize:number;totalCount:number;}
@Injectable({providedIn:'root'}) export class ExpensesApiService { private http=inject(HttpClient);private base=inject(API_BASE_URL);
 categories(){return this.http.get<ApiResponse<ExpenseCategory[]>>(`${this.base}/expenses/categories`).pipe(map(r=>r.data??[]));}
 saveCategory(v:{code:string;name:string;isActive:boolean},id?:string){return (id?this.http.put<ApiResponse<ExpenseCategory>>(`${this.base}/expenses/categories/${id}`,v):this.http.post<ApiResponse<ExpenseCategory>>(`${this.base}/expenses/categories`,v)).pipe(map(r=>this.data(r)));}
 list(search='',from='',to=''){let p=new HttpParams().set('page','1').set('pageSize','50');if(search)p=p.set('search',search);if(from)p=p.set('from',from);if(to)p=p.set('to',to);return this.http.get<ApiResponse<Paged<ExpenseVoucher>>>(`${this.base}/expenses`,{params:p}).pipe(map(r=>r.data!));}
 get(id:string){return this.http.get<ApiResponse<ExpenseVoucher>>(`${this.base}/expenses/${id}`).pipe(map(r=>this.data(r)));}
 save(v:unknown,id?:string){return (id?this.http.put<ApiResponse<ExpenseVoucher>>(`${this.base}/expenses/${id}`,v):this.http.post<ApiResponse<ExpenseVoucher>>(`${this.base}/expenses`,v)).pipe(map(r=>this.data(r)));}
 post(id:string){return this.http.post<ApiResponse<ExpenseVoucher>>(`${this.base}/expenses/${id}/post`,{}).pipe(map(r=>this.data(r)));}
 void(id:string,reason:string){return this.http.post<ApiResponse<ExpenseVoucher>>(`${this.base}/expenses/${id}/void`,{reason}).pipe(map(r=>this.data(r)));}
 private data<T>(r:ApiResponse<T>):T{if(!r.succeeded||!r.data)throw new Error(r.errors[0]??'Request failed.');return r.data;}}
