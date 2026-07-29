import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { catchError, of } from 'rxjs';

interface LoginResult { accessToken: string; expiresIn: number }

@Injectable({ providedIn: 'root' })
export class AuthService {
  private _accessToken = signal<string | null>(null);
  public accessToken = this._accessToken.asReadonly();
  private _error = signal<string | null>(null);
  public error = this._error.asReadonly();

  constructor(private http: HttpClient, private router: Router) {}

  login(email: string, password: string) {
    this._error.set(null);
    this.http.post<LoginResult>('/api/auth/login', { email, password }, { withCredentials: true }).pipe(
      catchError((error) => {
        const message = error?.error?.title || error?.message || 'Credenciais inválidas. Tente novamente.';
        this._error.set(message);
        return of(null);
      })
    ).subscribe((res) => {
      if (res?.accessToken) {
        this._accessToken.set(res.accessToken);
        this.router.navigateByUrl('/');
      }
    });
  }

  register(email: string, password: string, displayName: string) {
    this._error.set(null);
    this.http.post('/api/auth/register', { email, password, displayName }, { withCredentials: true }).pipe(
      catchError((error) => {
        const message = error?.error?.title || error?.message || 'Não foi possível registrar. Verifique os dados.';
        this._error.set(message);
        return of(null);
      })
    ).subscribe((res) => {
      if (res) {
        this._error.set('Conta criada com sucesso. Faça login.');
        this.router.navigateByUrl('/login');
      }
    });
  }

  clear() { this._accessToken.set(null); this.http.post('/api/auth/logout', {}, { withCredentials: true }).subscribe(); this.router.navigateByUrl('/login'); }

  refresh() {
    return this.http.post<LoginResult>('/api/auth/refresh', {}, { withCredentials: true });
  }

  clearError() { this._error.set(null); }
}
