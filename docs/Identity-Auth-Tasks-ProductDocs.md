# Tarefas Detalhadas e Documentação do Produto

## Resumo das tarefas (alto nível)

1. Add Identity & EF packages
2. Create `ApplicationUser` and roles
3. Configure Identity `DbContext` and services
4. Add EF migrations & apply DB
5. Seed default roles & admin user
6. Implement JWT + refresh tokens
7. Create auth endpoints (register/login/refresh/logout)
8. Protect API endpoints with policies
9. Frontend: login UI, token flow, guards
10. Frontend: route guards & attach token
11. Secure token storage (httpOnly cookie)
12. CORS, CSRF and cookie settings
13. Enforce HTTPS, HSTS & security headers
14. Secrets management (env/Key Vault)
15. CI/CD: migrations & secret handling
16. Logging, monitoring & rate limiting
17. Automated auth/integration tests
18. Security audit & pentest checklist
19. Docs: runbook + developer guide

---

## Tarefas com passos concretos (fase 1 - 3)

### 1) Add Identity & EF packages
- Abrir `FinanceApp.Api` e executar:

```powershell
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
```

Aceitação: pacotes instalados e `dotnet build` limpa.

### 2) Create `ApplicationUser` and roles
- Criar `Models/ApplicationUser.cs`:

```csharp
using Microsoft.AspNetCore.Identity;

public class ApplicationUser : IdentityUser
{
    // Campos adicionais se necessário (ex: DisplayName)
}
```

Aceitação: classe adicionada e compilação ok.

### 3) Configure Identity DbContext
- Criar `AppIdentityDbContext : IdentityDbContext<ApplicationUser>` e registrar em `Program.cs`.
- Exemplo de configuração em `Program.cs`:

```csharp
builder.Services.AddDbContext<AppIdentityDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<AppIdentityDbContext>()
    .AddDefaultTokenProviders();
```

Aceitação: app inicia e Identity está registrado.

### 4) Add EF migrations & apply DB
- `dotnet ef migrations add IdentityInit -p FinanceApp.Api -s FinanceApp.Api`
- `dotnet ef database update -p FinanceApp.Api -s FinanceApp.Api`

Aceitação: tabelas Identity criadas no banco.

### 5) Seed default roles & admin user
- Implementar um serviço de seed que cria roles e um admin usando credenciais via env vars.

Aceitação: roles `Admin` e `User` existem; admin pode logar.

---

## Auth endpoints — contrato mínimo

- `POST /api/auth/register`
  - body: `{ "email":"","password":"","displayName":"" }`
  - success: `201 Created`

- `POST /api/auth/login`
  - body: `{ "email":"","password":"" }`
  - success: `200` with JSON `{ "accessToken":"...","expiresIn":3600 }` and `Set-Cookie` for refresh token (HttpOnly)

- `POST /api/auth/refresh`
  - body: none (cookie sent automatically)
  - success: `200` with new access token

- `POST /api/auth/logout`
  - invalidates refresh token server-side and clears cookie

---

## Regras de negócio e modelo de permissão (documentação do produto)

Visão geral
- Usuário: conta individual, pode criar/editar/excluir suas próprias transações.
- Admin: pode listar, gerenciar e atribuir roles/permissions para usuários — *não* tem acesso intrínseco a todas as transações (exceto quando explicitamente adicionado como colaborador).
- Colaboração/Shared Accounts: recurso que permite que múltiplos usuários acessem o mesmo conjunto de transações (ex.: casal que compartilha despesas).

Principal regras
- Por padrão, um recurso financeiro pertence a um `Owner` (usuário ou entidade compartilhada).
- `Owner` pode adicionar `Collaborator`s com permissões: `Owner`, `Editor`, `Viewer`.
- `Editor` pode criar/editar/excluir transações daquele recurso.
- `Viewer` apenas visualiza.
- Admin global: gerencia `Users`, `Roles` e `Permissions`, mas não visualiza automaticamente dados de usuários sem permissão explícita.

Exemplos
- Cenário casal: usuário `Jose` e `Esposa` são adicionados como `Editors` em `SharedAccount` chamado `Casa`. Ambos podem ver/editar a mesma lista de transações.
- Cenário admin: `admin@example.com` adiciona `Esposa` ao `SharedAccount` — após isso `Esposa` ganha acesso conforme o papel definido.

Privacidade
- Usuários sem associação não devem conseguir enumerar ou visualizar transações de terceiros.

Auditoria
- Todas as ações sensíveis (login, alteração de roles, criação/exclusão de transação por outros) devem gerar entradas de auditoria com `who/what/when`.

Atualização de documentação
- Regra: a cada alteração em qualquer endpoint, modelo ou política de autorização, atualize este documento e registre um resumo no changelog (`docs/CHANGELOG.md`).

---

## Observações operacionais

- Tokens: access token curto (ex.: 15 min), refresh token longo (7–30 dias) revogável.
- Para maior segurança, use refresh tokens armazenados como `HttpOnly` cookies e access tokens em memória no frontend.
- Em produção, use DB robusto e Key Vault para segredos.

## Próximos passos sugeridos

1. Implementar itens 1–5 (Identity + seed).
2. Implementar endpoints de auth e testes de integração.
3. Implementar modelo de `SharedAccount` e políticas de autorização.
