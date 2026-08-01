import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  let router: { navigateByUrl: ReturnType<typeof vi.fn> };

  const tokenWithRoles = (roles: string | string[], exp = Math.floor(Date.now() / 1000) + 900) => {
    const payload = btoa(JSON.stringify({ role: roles, exp })).replace(/=/g, '');
    return `header.${payload}.signature`;
  };

  beforeEach(() => {
    localStorage.clear();
    router = { navigateByUrl: vi.fn() };
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Router, useValue: router }
      ]
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('stores the access token, reads roles and navigates after login', () => {
    const token = tokenWithRoles(['User', 'Admin']);

    service.login('ana@example.com', 'Senha123');

    const request = httpMock.expectOne('/api/auth/login');
    expect(request.request.method).toBe('POST');
    expect(request.request.withCredentials).toBe(true);
    request.flush({ accessToken: token, expiresIn: 900 });

    expect(service.accessToken()).toBe(token);
    expect(service.hasRole('Admin')).toBe(true);
    expect(localStorage.getItem('accessToken')).toBe(token);
    expect(router.navigateByUrl).toHaveBeenCalledWith('/');
  });

  it('reads the .NET role claim used by older tokens', () => {
    const payload = btoa(JSON.stringify({
      exp: Math.floor(Date.now() / 1000) + 900,
      'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'Admin'
    })).replace(/=/g, '');
    const token = `header.${payload}.signature`;

    service.login('admin@example.com', 'Senha123');
    httpMock.expectOne('/api/auth/login').flush({ accessToken: token, expiresIn: 900 });

    expect(service.hasRole('Admin')).toBe(true);
  });

  it('shows a clear message when credentials are rejected', () => {
    service.login('ana@example.com', 'senha-incorreta');

    const request = httpMock.expectOne('/api/auth/login');
    request.flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(service.error()).toBe('E-mail ou senha inválidos. Tente novamente.');
  });

  it('does not restore an expired token saved by a previous session', () => {
    const expiredToken = tokenWithRoles('User', Math.floor(Date.now() / 1000) - 1);
    localStorage.setItem('accessToken', expiredToken);
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Router, useValue: router }
      ]
    });

    service = TestBed.inject(AuthService);

    expect(service.accessToken()).toBeNull();
    expect(service.isAuthenticated()).toBe(false);
    expect(localStorage.getItem('accessToken')).toBeNull();
  });

  it('sends the CSRF token when refreshing a session', () => {
    document.cookie = 'csrfToken=csrf-value; path=/';

    service.refresh().subscribe();

    const request = httpMock.expectOne('/api/auth/refresh');
    expect(request.request.withCredentials).toBe(true);
    expect(request.request.headers.get('X-CSRF-Token')).toBe('csrf-value');
    request.flush({ accessToken: 'new-token', expiresIn: 900 });
  });

  it('clears local state and requests a CSRF-protected logout', () => {
    localStorage.setItem('accessToken', tokenWithRoles('User'));
    service = TestBed.inject(AuthService);
    document.cookie = 'csrfToken=logout-csrf; path=/';

    service.clear();

    const request = httpMock.expectOne('/api/auth/logout');
    expect(request.request.headers.get('X-CSRF-Token')).toBe('logout-csrf');
    request.flush({});
    expect(service.accessToken()).toBeNull();
    expect(localStorage.getItem('accessToken')).toBeNull();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/login');
  });
});
