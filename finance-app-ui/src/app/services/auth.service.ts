import { Injectable, signal } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Router } from '@angular/router';
import { catchError, of } from 'rxjs';
import { ErrorMessageService } from './error-message.service';

interface LoginResult { accessToken: string; expiresIn: number }

interface JwtClaims {
  role?: string | string[];
  exp?: number;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private _accessToken = signal<string | null>(null);
  public accessToken = this._accessToken.asReadonly();
  private _roles = signal<string[]>([]);
  public roles = this._roles.asReadonly();
  private _error = signal<string | null>(null);
  public error = this._error.asReadonly();

  constructor(private http: HttpClient, private router: Router, private errorMessages: ErrorMessageService) {
    const savedToken = localStorage.getItem('accessToken');
    if (savedToken && !this.isTokenExpired(savedToken)) {
      this.setAccessToken(savedToken);
    } else if (savedToken) {
      localStorage.removeItem('accessToken');
    }
  }

  private parseJwt(token: string): JwtClaims | null {
    try {
      const payload = token.split('.')[1];
      const decoded = atob(payload.replace(/-/g, '+').replace(/_/g, '/'));
      return JSON.parse(decodeURIComponent(escape(decoded)));
    } catch {
      return null;
    }
  }

  private isTokenExpired(token: string) {
    const expiration = this.parseJwt(token)?.exp;
    return !expiration || expiration * 1000 <= Date.now();
  }

  private setAccessToken(token: string) {
    this._accessToken.set(token);
    localStorage.setItem('accessToken', token);
    const claims = this.parseJwt(token);
    if (claims && claims.role) {
      const roleClaim = claims.role;
      if (Array.isArray(roleClaim)) {
        this._roles.set(roleClaim);
      } else {
        this._roles.set([roleClaim]);
      }
    } else {
      this._roles.set([]);
    }
  }

  private getCsrfToken() {
    const cookies = document.cookie.split(';').map(c => c.trim());
    const csrfCookie = cookies.find(c => c.startsWith('csrfToken='));
    return csrfCookie ? csrfCookie.split('=')[1] : null;
  }

  hasRole(role: string) {
    return this._roles().includes(role);
  }

  isAuthenticated() {
    const token = this._accessToken();
    return !!token && !this.isTokenExpired(token);
  }

  handleUnauthorized() {
    this._accessToken.set(null);
    this._roles.set([]);
    localStorage.removeItem('accessToken');
    this._error.set('Sua sessão expirou. Entre novamente para continuar.');
    this.router.navigateByUrl('/login');
  }

  login(email: string, password: string) {
    this._error.set(null);
    this.http.post<LoginResult>('/api/auth/login', { email, password }, { withCredentials: true }).pipe(
      catchError((error) => {
        const message = error?.status === 401
          ? 'E-mail ou senha inválidos. Tente novamente.'
          : this.errorMessages.forRequest(error, 'Não foi possível entrar agora. Tente novamente.');
        this._error.set(message);
        return of(null);
      })
    ).subscribe((res) => {
      if (res?.accessToken) {
        this.setAccessToken(res.accessToken);
        this.router.navigateByUrl('/');
      }
    });
  }

  register(email: string, password: string, displayName: string, inviteCode: string) {
    this._error.set(null);
    this.http.post('/api/auth/register', { email, password, displayName, inviteCode }, { withCredentials: true }).pipe(
      catchError((error) => {
        const message = this.errorMessages.forRequest(error, 'Não foi possível criar sua conta. Verifique os dados e tente novamente.');
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

  forgotPassword(email: string) {
    this._error.set(null);
    return this.http.post('/api/auth/forgot-password', { email }, { withCredentials: true }).pipe(
      catchError((error) => {
        const message = this.errorMessages.forRequest(error, 'Não foi possível enviar as instruções. Tente novamente.');
        this._error.set(message);
        return of(null);
      })
    );
  }

  resetPassword(token: string, code: string, newPassword: string) {
    this._error.set(null);
    return this.http.post('/api/auth/reset-password', { token, code, newPassword }, { withCredentials: true }).pipe(
      catchError((error) => {
        const message = this.errorMessages.forRequest(error, 'Não foi possível redefinir a senha. Tente novamente.');
        this._error.set(message);
        return of(null);
      })
    );
  }

  setMessage(message: string) {
    this._error.set(message);
  }

  clear() {
    this._accessToken.set(null);
    this._roles.set([]);
    localStorage.removeItem('accessToken');
    const csrf = this.getCsrfToken();
    const headers = csrf ? new HttpHeaders({ 'X-CSRF-Token': csrf }) : undefined;
    this.http.post('/api/auth/logout', {}, { withCredentials: true, headers }).subscribe();
    this.router.navigateByUrl('/login');
  }

  refresh() {
    const csrf = this.getCsrfToken();
    const headers = csrf ? new HttpHeaders({ 'X-CSRF-Token': csrf }) : undefined;
    return this.http.post<LoginResult>('/api/auth/refresh', {}, { withCredentials: true, headers });
  }

  clearError() { this._error.set(null); }
}
