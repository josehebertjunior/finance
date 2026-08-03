using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Npgsql;
using FinanceApp.Api.Models;
using FinanceApp.Api.Endpoints;
using FinanceApp.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("A conexão DefaultConnection não foi configurada.");
connectionString = NormalizePostgresConnectionString(connectionString);
if (connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
    || connectionString.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("SQLite is not supported. Use a PostgreSQL connection string.");

// SQLite é mantido no desenvolvimento. Em produção, use a connection string PostgreSQL do Neon.
builder.Services.AddDbContext<FinanceDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

builder.Services.AddDbContext<AppIdentityDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

builder.Services.AddIdentity<ApplicationUser, Microsoft.AspNetCore.Identity.IdentityRole>()
    .AddEntityFrameworkStores<AppIdentityDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<MailSettings>(builder.Configuration.GetSection("MailSettings"));
builder.Services.Configure<ResendSettings>(builder.Configuration.GetSection("Resend"));
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("App"));
if (!string.IsNullOrWhiteSpace(builder.Configuration["Resend:ApiKey"]))
    builder.Services.AddHttpClient<IAppEmailSender, ResendEmailSender>();
else
    builder.Services.AddSingleton<IAppEmailSender, SmtpEmailSender>();

builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?.Where(origin => Uri.TryCreate(origin, UriKind.Absolute, out _))
        .ToArray() ?? ["http://localhost:4200"];
    options.AddPolicy("AppCors",
        policyBuilder =>
        {
            policyBuilder.WithOrigins(allowedOrigins)
                   .AllowAnyHeader()
                   .AllowAnyMethod()
                   .AllowCredentials();
        });
});

// Identity options: password strength and lockout
builder.Services.Configure<IdentityOptions>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;

    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
});

// Prefer environment variables for secrets (Jwt__Key, Admin__Email, Admin__Password)

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "dev-key-please-change";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "finance-api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "finance-client";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = true;
        options.SaveToken = true;
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(jwtKey);
        if (keyBytes.Length < 32)
        {
            keyBytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(jwtKey));
        }
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(keyBytes),
            RoleClaimType = ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("OwnerOrAdmin", policy => policy.Requirements.Add(new FinanceApp.Api.Authorization.IsOwnerOrAdminRequirement()));
});

builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, FinanceApp.Api.Authorization.IsOwnerOrAdminHandler>();

// Rate limiting per remote IP
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });
    // Named limiter for login attempts (per IP)
    options.AddPolicy<string>("Login", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });
    options.RejectionStatusCode = 429;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
    await EnsureSchemaAsync(db, "Transactions");
}

// Ensure Identity DB migrated and seed roles/admin
using (var scope = app.Services.CreateScope())
{
    var idDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
    await EnsureSchemaAsync(idDb, "AspNetUsers");
    await EnsureTenantMembershipSchemaAsync(idDb);

    var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    string[] roles = new[] { "Admin", "User" };
    foreach (var r in roles)
    {
        if (!roleMgr.RoleExistsAsync(r).Result)
        {
            roleMgr.CreateAsync(new IdentityRole(r)).Wait();
        }
    }

    var adminEmail = builder.Configuration["Admin:Email"] ?? "josehebertjr@gmail.com";
    var adminPass = builder.Configuration["Admin:Password"] ?? "Admin123!";
    if (app.Environment.IsProduction() && (string.IsNullOrWhiteSpace(builder.Configuration["Admin:Email"]) || string.IsNullOrWhiteSpace(builder.Configuration["Admin:Password"])))
        throw new InvalidOperationException("Defina Admin__Email e Admin__Password antes de iniciar a API em produção.");
    var admin = userMgr.FindByEmailAsync(adminEmail).Result;
    if (admin == null)
    {
        admin = new ApplicationUser { UserName = adminEmail, Email = adminEmail, DisplayName = "Administrator" };
        var res = userMgr.CreateAsync(admin, adminPass).Result;
        if (res.Succeeded) userMgr.AddToRoleAsync(admin, "Admin").Wait();
    }
    else
    {
        if (!userMgr.IsInRoleAsync(admin, "Admin").Result)
        {
            userMgr.AddToRoleAsync(admin, "Admin").Wait();
        }
    }
}

using (var scope = app.Services.CreateScope())
{
    var financeDb = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
    await EnsureTenantScopedRegistrationsSchemaAsync(financeDb);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
// Security: HSTS in non-development
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Basic security headers
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] = "geolocation=()";
    // Content Security Policy (adjust as needed for external assets)
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; object-src 'none'; frame-ancestors 'none'; base-uri 'self'";
    await next();
});

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    KnownNetworks = { },
    KnownProxies = { }
});
app.UseHttpsRedirection();
app.UseCors("AppCors");

app.UseAuthentication();
app.UseAuthorization();

app.MapApiEndpoints();
app.MapAuthEndpoints();
app.MapTenantEndpoints();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.Run();

static async Task EnsureSchemaAsync(DbContext context, string markerTable)
{
    if (!context.Database.IsNpgsql())
    {
        await context.Database.MigrateAsync();
        return;
    }

    var connection = context.Database.GetDbConnection();
    await connection.OpenAsync();
    try
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = @tableName)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "tableName";
        parameter.Value = markerTable;
        command.Parameters.Add(parameter);
        var exists = (bool)(await command.ExecuteScalarAsync() ?? false);
        if (!exists)
            await context.Database.ExecuteSqlRawAsync(context.Database.GenerateCreateScript());
    }
    finally
    {
        await connection.CloseAsync();
    }
}

static async Task EnsureTenantMembershipSchemaAsync(AppIdentityDbContext context)
{
    var membershipTable = context.Database.IsNpgsql()
        ? """
          CREATE TABLE IF NOT EXISTS "TenantMemberships" (
              "UserId" text NOT NULL,
              "TenantId" text NOT NULL,
              "JoinedAt" timestamp with time zone NOT NULL,
              CONSTRAINT "PK_TenantMemberships" PRIMARY KEY ("UserId", "TenantId"),
              CONSTRAINT "FK_TenantMemberships_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
              CONSTRAINT "FK_TenantMemberships_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE CASCADE
          )
          """
        : """
          CREATE TABLE IF NOT EXISTS "TenantMemberships" (
              "UserId" TEXT NOT NULL,
              "TenantId" TEXT NOT NULL,
              "JoinedAt" TEXT NOT NULL,
              CONSTRAINT "PK_TenantMemberships" PRIMARY KEY ("UserId", "TenantId"),
              CONSTRAINT "FK_TenantMemberships_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
              CONSTRAINT "FK_TenantMemberships_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE CASCADE
          )
          """;

    await context.Database.ExecuteSqlRawAsync(membershipTable);
    var insertExistingMemberships = context.Database.IsNpgsql()
        ? """
          INSERT INTO "TenantMemberships" ("UserId", "TenantId", "JoinedAt")
          SELECT "Id", "TenantId", CURRENT_TIMESTAMP FROM "AspNetUsers" WHERE "TenantId" IS NOT NULL
          ON CONFLICT ("UserId", "TenantId") DO NOTHING;
          """
        : """
          INSERT OR IGNORE INTO "TenantMemberships" ("UserId", "TenantId", "JoinedAt")
          SELECT "Id", "TenantId", CURRENT_TIMESTAMP FROM "AspNetUsers" WHERE "TenantId" IS NOT NULL;
          """;
    var insertCreatorMemberships = context.Database.IsNpgsql()
        ? """
          INSERT INTO "TenantMemberships" ("UserId", "TenantId", "JoinedAt")
          SELECT "CreatedById", "Id", CURRENT_TIMESTAMP FROM "Tenants"
          ON CONFLICT ("UserId", "TenantId") DO NOTHING;
          """
        : """
          INSERT OR IGNORE INTO "TenantMemberships" ("UserId", "TenantId", "JoinedAt")
          SELECT "CreatedById", "Id", CURRENT_TIMESTAMP FROM "Tenants";
          """;
    await context.Database.ExecuteSqlRawAsync(insertExistingMemberships);
    await context.Database.ExecuteSqlRawAsync(insertCreatorMemberships);
    await context.Database.ExecuteSqlRawAsync("""
        UPDATE "AspNetUsers"
        SET "TenantId" = (
            SELECT membership."TenantId"
            FROM "TenantMemberships" AS membership
            INNER JOIN "Tenants" AS tenant ON tenant."Id" = membership."TenantId"
            WHERE membership."UserId" = "AspNetUsers"."Id"
            ORDER BY tenant."CreatedAt", membership."TenantId"
            LIMIT 1
        )
        WHERE "TenantId" IS NULL
          AND EXISTS (
            SELECT 1 FROM "TenantMemberships"
            WHERE "UserId" = "AspNetUsers"."Id"
          );
        """);
}

static async Task EnsureTenantScopedRegistrationsSchemaAsync(FinanceDbContext context)
{
    foreach (var table in new[] { "Categories", "Persons", "PaymentMethods", "Transactions" })
    {
        var sql = context.Database.IsNpgsql()
            ? $"ALTER TABLE \"{table}\" ADD COLUMN IF NOT EXISTS \"TenantId\" text NULL"
            : $"ALTER TABLE \"{table}\" ADD COLUMN \"TenantId\" TEXT NULL";
        try
        {
            await context.Database.ExecuteSqlRawAsync(sql);
        }
        catch (Exception) when (!context.Database.IsNpgsql())
        {
            // SQLite reports an error when the column already exists.
        }

        await context.Database.ExecuteSqlRawAsync($"""
            UPDATE "{table}"
            SET "TenantId" = (SELECT "Id" FROM "Tenants" ORDER BY "CreatedAt", "Id" LIMIT 1)
            WHERE "TenantId" IS NULL;
            """);
    }
}

static string NormalizePostgresConnectionString(string connectionString)
{
    if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri)
        || (uri.Scheme != "postgres" && uri.Scheme != "postgresql"))
    {
        return connectionString;
    }

    var credentials = uri.UserInfo.Split(':', 2);
    if (credentials.Length != 2)
        throw new InvalidOperationException("A URL de conexao PostgreSQL precisa conter usuario e senha.");

    return new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.IsDefaultPort ? 5432 : uri.Port,
        Database = uri.AbsolutePath.Trim('/'),
        Username = Uri.UnescapeDataString(credentials[0]),
        Password = Uri.UnescapeDataString(credentials[1]),
        SslMode = SslMode.Require
    }.ConnectionString;
}

public partial class Program { }
