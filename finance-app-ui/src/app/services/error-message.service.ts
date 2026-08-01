import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ErrorMessageService {
  private _message = signal<string | null>(null);
  readonly message = this._message.asReadonly();

  show(error: unknown, fallback = 'Não foi possível concluir esta ação. Tente novamente.') {
    this._message.set(this.forRequest(error, fallback));
  }

  clear() {
    this._message.set(null);
  }

  forRequest(error: unknown, fallback = 'Não foi possível concluir esta ação. Tente novamente.') {
    if (!(error instanceof HttpErrorResponse)) return fallback;

    switch (error.status) {
      case 0:
        return 'Não foi possível conectar ao servidor. Verifique sua conexão e tente novamente.';
      case 400:
        return this.fromApi(error) ?? 'Revise os dados informados e tente novamente.';
      case 401:
        return 'Sua sessão expirou. Entre novamente para continuar.';
      case 403:
        return 'Você não tem permissão para realizar esta ação.';
      case 404:
        return 'Não encontramos a informação solicitada.';
      case 409:
        return 'Já existe um cadastro com estas informações.';
      case 429:
        return 'Muitas tentativas em pouco tempo. Aguarde um minuto e tente novamente.';
      default:
        return fallback;
    }
  }

  private fromApi(error: HttpErrorResponse) {
    const message = error.error?.error;
    if (typeof message !== 'string') return null;

    const translations: Record<string, string> = {
      'Name is required.': 'Informe um nome.',
      'Description is required.': 'Informe a descrição do lançamento.',
      'Amount must be greater than zero.': 'Informe um valor maior que zero.',
      'Email and tenant name are required.': 'Informe o e-mail e o nome do grupo.',
      'Tenant not found.': 'Grupo não encontrado.',
      'Role is required.': 'Selecione uma função.',
      'Role must be Admin or User.': 'Selecione uma função válida.'
    };

    return translations[message] ?? message;
  }
}
