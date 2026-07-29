## Plano de Implementação — Autenticação, Autorização e Segurança

**Objetivo**

Adicionar controle de login (autenticação) e gerenciamento de usuários (autorização) usando ASP.NET Identity, com suporte a roles e claims, e preparação para publicação em produção (segurança, secrets, monitoramento).

**Escopo**

- Backend (.NET 8): ASP.NET Identity + EF Core (SQLite em dev, SQL Server ou outro em produção)
- JWT para access tokens + refresh tokens armazenados de forma segura
- Authorization por Roles e Claims; policies configuráveis
- Frontend (Angular): telas de login/registro, guards, interceptors
- Segurança: HTTPS, HSTS, CORS restrito, cookies `HttpOnly`, proteção CSRF quando aplicável

### Fases

1) Preparação
  - Adicionar pacotes NuGet: `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.AspNetCore.Authentication.JwtBearer`.
  - Criar `ApplicationUser : IdentityUser` para estender dados do usuário.

2) Persistência e IdentityDbContext
  - Implementar `AppIdentityDbContext : IdentityDbContext<ApplicationUser>`.
  - Configurar serviços Identity em `Program.cs`.
  - Criar e aplicar migrations.

3) Roles e Seed
  - Criar seed para roles (por exemplo: `Admin`, `User`).
  - Criar admin inicial (credenciais via env vars).

4) Endpoints de Autenticação
  - `POST /api/auth/register` — registrar usuário.
  - `POST /api/auth/login` — retornar JWT (access) e cookie `HttpOnly` com refresh.
  - `POST /api/auth/refresh` — renovar tokens.
  - `POST /api/auth/logout` — invalidar refresh token.

5) Autorização e Políticas
  - Proteger endpoints com `[Authorize]` e policies específicas.
  - Implementar política de colaboração: verificar que usuário pertence a um `Group`/`SharedAccount` ou tem claim apropriada.

6) Frontend
  - Implementar `AuthService`, `AuthInterceptor` e `AuthGuard`.
  - Fluxo: armazenar access token em memória; refresh token em `HttpOnly` cookie.

7) Segurança adicional
  - Lockout, validação de senha forte, possibilidade de MFA futura.
  - Rate limiting e monitoramento.

8) Deploy e Ops
  - Chaves/segredos via Key Vault / env vars.
  - CI/CD aplica migrations e injeta segredos de runtime.

**Critérios de aceitação**

- Registro e login de usuário funcionam.
- Admin visualiza e gerencia usuários e roles.
- Usuário vê apenas seus dados por padrão; colaborador vê recursos compartilhados.
- Tokens expirados são renovados via endpoint de refresh.

**Documentação e manutenção**

Todo ajuste nas regras de negócio ou no fluxo de autenticação deve ser refletido nos docs em `docs/` e no changelog.
