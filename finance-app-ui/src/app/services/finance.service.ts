import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class FinanceService {
  private apiUrl = 'http://localhost:5078/api';
  private http = inject(HttpClient);

  getCategories(): Observable<any[]> { return this.http.get<any[]>(`${this.apiUrl}/categories`); }
  getPaymentMethods(): Observable<any[]> { return this.http.get<any[]>(`${this.apiUrl}/paymentmethods`); }
  getPersons(): Observable<any[]> { return this.http.get<any[]>(`${this.apiUrl}/persons`); }
  
  getTransactions(year?: number, month?: number): Observable<any[]> { 
    let url = `${this.apiUrl}/transactions`;
    if(year && month) url += `?year=${year}&month=${month}`;
    return this.http.get<any[]>(url); 
  }
  
  getSavingsBalance(): Observable<any> { return this.http.get<any>(`${this.apiUrl}/savings/balance`); }
  
  createTransaction(t: any): Observable<any> { return this.http.post(`${this.apiUrl}/transactions`, t); }
  deleteTransaction(id: number): Observable<any> { return this.http.delete(`${this.apiUrl}/transactions/${id}`); }
  deleteCategory(id: number): Observable<any> { return this.http.delete(`${this.apiUrl}/categories/${id}`); }
  deletePerson(id: number): Observable<any> { return this.http.delete(`${this.apiUrl}/persons/${id}`); }
  deletePaymentMethod(id: number): Observable<any> { return this.http.delete(`${this.apiUrl}/paymentmethods/${id}`); }
}
