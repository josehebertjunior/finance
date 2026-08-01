import { HttpErrorResponse, HttpRequest, HttpResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { AuthInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';
import { ErrorMessageService } from './error-message.service';

describe('AuthInterceptor', () => {
  it('adds a bearer token to authenticated requests', () => {
    const auth = { isAuthenticated: () => true, accessToken: () => 'access-token' } as AuthService;
    const interceptor = new AuthInterceptor(auth, new ErrorMessageService());
    let interceptedRequest: HttpRequest<unknown> | undefined;
    const next = { handle: (request: HttpRequest<unknown>) => {
      interceptedRequest = request;
      return of(new HttpResponse({ status: 200 }));
    }};

    interceptor.intercept(new HttpRequest('GET', '/api/transactions'), next).subscribe();

    expect(interceptedRequest?.headers.get('Authorization')).toBe('Bearer access-token');
  });

  it('does not add an authorization header without a session', () => {
    const auth = { isAuthenticated: () => false, accessToken: () => null } as AuthService;
    const interceptor = new AuthInterceptor(auth, new ErrorMessageService());
    let interceptedRequest: HttpRequest<unknown> | undefined;
    const next = { handle: (request: HttpRequest<unknown>) => {
      interceptedRequest = request;
      return of(new HttpResponse({ status: 200 }));
    }};

    interceptor.intercept(new HttpRequest('GET', '/api/transactions'), next).subscribe();

    expect(interceptedRequest?.headers.has('Authorization')).toBe(false);
  });

  it('clears the client session after an unauthorized protected request', () => {
    const auth = {
      isAuthenticated: () => true,
      accessToken: () => 'access-token',
      handleUnauthorized: vi.fn()
    } as unknown as AuthService;
    const errorMessages = new ErrorMessageService();
    const interceptor = new AuthInterceptor(auth, errorMessages);
    const next = { handle: () => throwError(() => new HttpErrorResponse({ status: 401 })) };

    interceptor.intercept(new HttpRequest('POST', '/api/transactions', {}), next).subscribe({ error: () => undefined });

    expect(auth.handleUnauthorized).toHaveBeenCalledOnce();
  });

  it('turns an API validation error into a readable message', () => {
    const auth = { isAuthenticated: () => true, accessToken: () => 'access-token' } as AuthService;
    const errorMessages = new ErrorMessageService();
    const interceptor = new AuthInterceptor(auth, errorMessages);
    const next = {
      handle: () => throwError(() => new HttpErrorResponse({
        status: 400,
        error: { error: 'Amount must be greater than zero.' }
      }))
    };

    interceptor.intercept(new HttpRequest('POST', '/api/transactions', {}), next).subscribe({ error: () => undefined });

    expect(errorMessages.message()).toBe('Informe um valor maior que zero.');
  });
});
