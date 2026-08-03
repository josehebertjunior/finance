import { Injectable, signal } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Router } from '@angular/router';
import { catchError, of } from 'rxjs';
import { ErrorMessageService } from './error-message.service';

interface LoginResult { accessToken: string; expiresIn: number }
interface GroupAccess { activeTenantId: string | null; groups: { tenantId: string; name: string }[] }

interface JwtClaims {
  role?: string | string[];
  'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'?: string | string[];
  exp?: number;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly refreshLeadMs = 2 * 60 * 1000;
  private readonly idleLimitMs = 2 * 60 * 1000;
  private readonly sessionPromptMs = 15 * 1000;
  private refreshTimer?: ReturnType<typeof setTimeout>;
  private promptTimer?: ReturnType<typeof setTimeout>;
  private lastActivity = Date.now();
  private _accessToken = signal<string | null>(null);
  public accessToken = this._accessToken.asReadonly();
  private _roles = signal<string[]>([]);
  public roles = this._roles.asReadonly();
  private _groups = signal<{ tenantId: string; name: string }[]>([]);
  public groups = this._groups.asReadonly();
  private _activeTenantId = signal<string | null>(null);
  public activeTenantId = this._activeTenantId.asReadonly();
  private _error = signal<string | null>(null);
  public error = this._error.asReadonly();
  private _sessionPrompt = signal(false);
  public sessionPrompt = this._sessionPrompt.asReadonly();

  constructor(private http: HttpClient, private router: Router, private errorMessages: ErrorMessageService) {
    if (typeof window !== 'undefined') {
      const markActivity = () => this.lastActivity = Date.now();
      ['pointerdown', 'keydown', 'touchstart', 'scroll'].forEach(event =>
        window.addEventListener(event, markActivity, { passive: true }));
    }
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
    const roleClaim = claims?.role ?? claims?.['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
    if (roleClaim) {
      if (Array.isArray(roleClaim)) {
        this._roles.set(roleClaim);
      } else {
        this._roles.set([roleClaim]);
      }
    } else {
      this._roles.set([]);
    }
    this.lastActivity = Date.now();
    this.scheduleRefresh(token);
  }

  private getCsrfToken() {
    const cookies = document.cookie.split(';').map(c => c.trim());
    const csrfCookie = cookies.find(c => c.startsWith('csrfToken='));
    if (!csrfCookie) return null;
    try { return decodeURIComponent(csrfCookie.split('=').slice(1).join('=')); } catch { return null; }
  }

  private scheduleRefresh(token: string) {
    this.stopSessionTimers();
    const expiration = this.parseJwt(token)?.exp;
    if (!expiration) return;
    const delay = Math.max(0, expiration * 1000 - Date.now() - this.refreshLeadMs);
    this.refreshTimer = setTimeout(() => {
      if (!this.isAuthenticated()) return this.handleUnauthorized();
      if (Date.now() - this.lastActivity < this.idleLimitMs) {
        this.renewSession();
      } else {
        this.askToContinueSession();
      }
    }, delay);
  }

  private stopSessionTimers() {
    if (this.refreshTimer) clearTimeout(this.refreshTimer);
    if (this.promptTimer) clearTimeout(this.promptTimer);
    this.refreshTimer = undefined;
    this.promptTimer = undefined;
    this._sessionPrompt.set(false);
  }

  private askToContinueSession() {
    this._sessionPrompt.set(true);
    this.promptTimer = setTimeout(() => this.clear(), this.sessionPromptMs);
  }

  private renewSession() {
    this.stopSessionTimers();
    this.refresh().subscribe({
      next: result => result?.accessToken ? this.setAccessToken(result.accessToken) : this.handleUnauthorized(),
      error: () => this.handleUnauthorized()
    });
  }

  continueSession() {
    this.lastActivity = Date.now();
    this.renewSession();
  }

  hasRole(role: string) {
    return this._roles().includes(role);
  }

  loadGroups() {
    if (!this.isAuthenticated()) return;
    this.http.get<GroupAccess>(`${this.apiUrl}/auth/groups`).subscribe({
      next: result => {
        this._groups.set(result.groups);
        this._activeTenantId.set(result.activeTenantId);
      }
    });
  }

  changeActiveGroup(tenantId: string) {
    if (!tenantId || tenantId === this._activeTenantId()) return;
    this.http.post(`${this.apiUrl}/auth/active-group`, { tenantId }).subscribe({
      next: () => window.location.reload()
    });
  }

  isAuthenticated() {
    const token = this._accessToken();
    return !!token && !this.isTokenExpired(token);
  }

  handleUnauthorized() {
    this.stopSessionTimers();
    this._accessToken.set(null);
    this._roles.set([]);
    this._groups.set([]);
    this._activeTenantId.set(null);
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
    this.stopSessionTimers();
    this._accessToken.set(null);
    this._roles.set([]);
    this._groups.set([]);
    this._activeTenantId.set(null);
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
