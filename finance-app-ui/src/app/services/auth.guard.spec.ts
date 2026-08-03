import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { authGuard } from './auth.guard';
import { adminGuard } from './admin.guard';
import { AuthService } from './auth.service';

describe('authentication guards', () => {
  let router: { navigate: ReturnType<typeof vi.fn>; createUrlTree: ReturnType<typeof vi.fn> };
  let auth: {
    isAuthenticated: ReturnType<typeof vi.fn>;
    hasRole: ReturnType<typeof vi.fn>;
  };

  beforeEach(() => {
    router = { navigate: vi.fn(), createUrlTree: vi.fn(() => 'login-url-tree') };
    auth = { isAuthenticated: vi.fn(), hasRole: vi.fn() };
    TestBed.configureTestingModule({
      providers: [
        { provide: Router, useValue: router },
        { provide: AuthService, useValue: auth }
      ]
    });
  });

  it('redirects anonymous users to login', () => {
    auth.isAuthenticated.mockReturnValue(false);

    const allowed = TestBed.runInInjectionContext(() => authGuard({} as any, {} as any));

    expect(allowed).toBe('login-url-tree');
    expect(router.createUrlTree).toHaveBeenCalledWith(['/login']);
  });

  it('allows signed-in users through the auth guard', () => {
    auth.isAuthenticated.mockReturnValue(true);

    const allowed = TestBed.runInInjectionContext(() => authGuard({} as any, {} as any));

    expect(allowed).toBe(true);
  });

  it('redirects non-admin users away from the admin page', () => {
    auth.isAuthenticated.mockReturnValue(true);
    auth.hasRole.mockReturnValue(false);

    const allowed = TestBed.runInInjectionContext(() => adminGuard({} as any, {} as any));

    expect(allowed).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/']);
  });
});
