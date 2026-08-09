# Lançamentos pelo WhatsApp

## O que já está preparado

O Finance recebe mensagens do WhatsApp em uma caixa de entrada. Nenhuma mensagem cria uma transação diretamente: ela aparece como sugestão no menu **Lançamentos recebidos** e precisa ser confirmada por uma pessoa do grupo.

Cada número autorizado é vinculado a:

- um grupo (tenant);
- o usuário proprietário do lançamento;
- opcionalmente, uma pessoa cadastrada no grupo.

Mensagens de números não autorizados e mensagens repetidas são ignoradas. O identificador da mensagem do WhatsApp é único para evitar lançamentos duplicados.

## Formatos iniciais reconhecidos

O valor deve usar vírgula decimal. O sistema busca um método de pagamento que já esteja cadastrado no grupo.

```text
89,90 almoço nubank
120,00 luz débito fixa
995,00 parcela civic débito 17/48
entrada 6200,00 salário
```

O primeiro formato cria uma saída sugerida. `entrada` ou `receita` cria uma entrada. A palavra `fixa` agenda a conta fixa; `17/48` identifica uma parcela.

## Configuração após adquirir o número

1. Crie um app em [Meta for Developers](https://developers.facebook.com/) e adicione o produto **WhatsApp**.
2. No Render, adicione as variáveis secretas:

   ```text
   WhatsApp__WebhookVerifyToken=<um-token-aleatório-longo>
   WhatsApp__AppSecret=<App Secret do app Meta>
   ```

3. Em **Webhook** na Meta, informe:

   ```text
   https://finance-4vj8.onrender.com/api/integrations/whatsapp/webhook
   ```

4. Use o mesmo `WebhookVerifyToken` escolhido no Render para validar o webhook na Meta.
5. Assine o evento `messages` no painel da Meta.
6. No Finance, entre no grupo correto, abra **Lançamentos recebidos** e autorize os números pessoais que podem enviar compras.
7. Faça primeiro um teste com o número de teste fornecido pela Meta, confirme a mensagem na caixa de entrada e só então cadastre o número definitivo.

## Segurança

- O webhook exige a assinatura `X-Hub-Signature-256` gerada com o `AppSecret`.
- Os segredos ficam somente nas variáveis do Render; não devem ser enviados ao GitHub.
- O webhook não aceita números que não tenham sido explicitamente autorizados por um administrador.
- A confirmação valida novamente se categoria, pessoa e método pertencem ao grupo ativo.
