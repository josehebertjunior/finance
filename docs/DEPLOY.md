# Publicação gratuita

Este repositório está preparado para usar **Vercel** no Angular, **Render** na API .NET e **Neon PostgreSQL** no banco. O SQLite é exclusivamente local: o disco de serviços gratuitos é temporário.

## 1. Banco no Neon

1. Crie um projeto gratuito no Neon.
2. Em **Connection details**, selecione a string de conexão para .NET/Npgsql.
3. Guarde a string completa. Ela será usada como `ConnectionStrings__DefaultConnection` no Render.

Na primeira inicialização com PostgreSQL, a API cria as tabelas da aplicação e do Identity a partir dos modelos atuais. Não reutilize o arquivo SQLite de desenvolvimento no servidor.

## 2. API no Render

1. Envie este repositório ao GitHub.
2. No Render, escolha **New > Blueprint** e selecione o repositório. O arquivo `render.yaml` criará o serviço Docker.
3. Preencha os valores marcados como secretos:

| Variável | Valor |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | string do Neon em formato Npgsql |
| `Admin__Email` / `Admin__Password` | administrador inicial, com senha forte |
| `Resend__ApiKey` | chave da API do Resend |
| `Resend__FromEmail` | remetente validado no Resend |
| `App__FrontendUrl` | URL final do Vercel, sem barra ao final |
| `Cors__AllowedOrigins__0` | mesma URL final do Vercel |

Após publicar, abra `https://SUA-API.onrender.com/health`. A resposta esperada é `{ "status": "ok" }`.

> No plano gratuito, o Render pausa a API após inatividade. A primeira requisição pode levar cerca de um minuto. Ele também bloqueia SMTP; por isso a API usa Resend quando `Resend__ApiKey` está definida.

## 3. Front-end no Vercel

1. No Vercel, importe o mesmo repositório GitHub.
2. Mantenha a raiz do repositório como base do projeto; o arquivo `vercel.json` já aponta para o Angular em `finance-app-ui`.
3. Crie a variável de ambiente `API_URL` com `https://SUA-API.onrender.com/api`.
4. Faça o deploy. O build `build:vercel` injeta essa URL no ambiente de produção sem versionar a URL da sua API.

Copie a URL `*.vercel.app` gerada e use-a nos campos `App__FrontendUrl` e `Cors__AllowedOrigins__0` da API. Depois, faça um novo deploy/restart no Render.

## Checklist final

- Faça login e crie um lançamento.
- Recarregue uma rota interna, como `/new`, para confirmar o fallback de SPA do Vercel.
- Teste convite e recuperação de senha com o Resend.
- Nunca envie chaves, connection strings ou senhas para o Git.
