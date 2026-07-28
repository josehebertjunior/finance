# 💰 Finance App

![Build](https://github.com/josehebertjunior/finance/actions/workflows/build.yml/badge.svg)

Aplicação full stack para controle de receitas e despesas pessoais, construída com **.NET** no back-end e **Angular** no front-end.

🚧 **Status:** em desenvolvimento

## Sobre o projeto

O Finance App nasceu da necessidade de ter um controle simples e centralizado de entradas e saídas financeiras — uma alternativa a planilhas manuais, com uma API própria e uma interface web responsiva.

## Tecnologias

**Backend** — `FinanceApp.Api`
- C# / .NET
- ASP.NET Core Web API
- Entity Framework

**Frontend** — `finance-app-ui`
- Angular
- TypeScript

## Funcionalidades

- [x] Cadastro de receitas e despesas, incluindo lançamentos parcelados
- [x] Edição e exclusão de lançamentos
- [x] Dashboard com filtro por mês e por pessoa
- [x] Cálculo de saldo por período (entradas − saídas)
- [x] Controle de saldo de reserva/poupança (depósitos e retiradas)
- [x] Cadastro de categorias, pessoas e formas de pagamento
- [x] Resumo de gastos agrupado por categoria (`/api/summary/by-category`)
- [ ] Gráficos visuais no dashboard
- [ ] Autenticação de usuário
- [ ] Deploy público

## Principais endpoints

| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/transactions?year=&month=` | Lista lançamentos, com filtro opcional por mês |
| POST | `/api/transactions` | Cria um lançamento (suporta parcelamento) |
| PUT | `/api/transactions/{id}` | Edita um lançamento |
| DELETE | `/api/transactions/{id}` | Remove um lançamento |
| GET | `/api/summary/by-category?year=&month=` | Total de despesas agrupado por categoria |
| GET | `/api/savings/balance` | Saldo acumulado de reserva |
| GET/POST/PUT/DELETE | `/api/categories`, `/api/persons`, `/api/paymentmethods` | CRUD dos cadastros de apoio |

Documentação interativa via Swagger em `/swagger` (ambiente de desenvolvimento).

## Como executar localmente

### Backend
```bash
cd FinanceApp.Api
dotnet restore
dotnet run
```

### Frontend
```bash
cd finance-app-ui
npm install
ng serve
```
Acesse em `http://localhost:4200`.

## Roadmap

- [ ] Gráficos no dashboard consumindo o endpoint de resumo por categoria
- [ ] Autenticação simples (JWT)
- [ ] Deploy de uma versão demo pública (API + front-end)

## Autor

**José Hebert Júnior**
Full Stack Developer — C# · .NET · Angular
[LinkedIn](https://www.linkedin.com/in/josehebertjunior/)
