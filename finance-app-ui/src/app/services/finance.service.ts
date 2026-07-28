import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, throwError } from 'rxjs';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class FinanceService {
  private apiUrl = environment.apiUrl;
  private http = inject(HttpClient);

  private handleError(error: any) {
    console.error('Erro na chamada à API:', error);
    return throwError(() => error);
  }

  getCategories(): Observable<any[]> {
    return this.http.get<any[]>(`${this.apiUrl}/categories`).pipe(catchError(this.handleError));
  }

  getPaymentMethods(): Observable<any[]> {
    return this.http.get<any[]>(`${this.apiUrl}/paymentmethods`).pipe(catchError(this.handleError));
  }

  getPersons(): Observable<any[]> {
    return this.http.get<any[]>(`${this.apiUrl}/persons`).pipe(catchError(this.handleError));
  }

  getTransactions(year?: number, month?: number): Observable<any[]> {
    let url = `${this.apiUrl}/transactions`;
    if (year && month) url += `?year=${year}&month=${month}`;
    return this.http.get<any[]>(url).pipe(catchError(this.handleError));
  }

  getSummaryByCategory(year: number, month: number): Observable<any[]> {
    return this.http.get<any[]>(`${this.apiUrl}/summary/by-category?year=${year}&month=${month}`).pipe(catchError(this.handleError));
  }

  getSavingsBalance(): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}/savings/balance`).pipe(catchError(this.handleError));
  }

  createTransaction(t: any): Observable<any> {
    return this.http.post(`${this.apiUrl}/transactions`, t).pipe(catchError(this.handleError));
  }

  updateTransaction(id: number, t: any): Observable<any> {
    return this.http.put(`${this.apiUrl}/transactions/${id}`, t).pipe(catchError(this.handleError));
  }

  deleteTransaction(id: number): Observable<any> {
    return this.http.delete(`${this.apiUrl}/transactions/${id}`).pipe(catchError(this.handleError));
  }

  deleteCategory(id: number): Observable<any> {
    return this.http.delete(`${this.apiUrl}/categories/${id}`).pipe(catchError(this.handleError));
  }

  deletePerson(id: number): Observable<any> {
    return this.http.delete(`${this.apiUrl}/persons/${id}`).pipe(catchError(this.handleError));
  }

  deletePaymentMethod(id: number): Observable<any> {
    return this.http.delete(`${this.apiUrl}/paymentmethods/${id}`).pipe(catchError(this.handleError));
  }
}
