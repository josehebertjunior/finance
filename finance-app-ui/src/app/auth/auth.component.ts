import { Component, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../services/auth.service';

@Component({
  selector: 'app-auth',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './auth.component.html',
  styleUrls: ['./auth.component.css']
})
export class AuthComponent implements OnInit {
  view = signal<'login' | 'register' | 'forgot' | 'reset'>('login');
  email = signal('');
  password = signal('');
  showPassword = signal(false);
  showNewPassword = signal(false);
  displayName = signal('');
  inviteCode = signal('');
  resetToken = signal('');
  resetCode = signal('');
  newPassword = signal('');

  constructor(public auth: AuthService) {}

  ngOnInit() {
    const params = new URLSearchParams(window.location.search);
    const token = params.get('resetToken');
    if (token) {
      this.resetToken.set(token);
      this.view.set('reset');
      this.auth.clearError();
    }
    const invite = params.get('invite');
    if (invite) {
      this.inviteCode.set(invite);
      this.view.set('register');
      this.auth.clearError();
    }
  }

  submitLogin() {
    this.auth.login(this.email(), this.password());
  }

  submitRegister() {
    this.auth.register(this.email(), this.password(), this.displayName(), this.inviteCode());
  }

  submitForgot() {
    this.auth.clearError();
    this.auth.forgotPassword(this.email()).subscribe((res) => {
      if (res) {
        this.auth.setMessage('Se o email existir, você receberá instruções para redefinir a senha.');
      }
    });
  }

  submitReset() {
    this.auth.clearError();
    this.auth.resetPassword(this.resetToken(), this.resetCode(), this.newPassword()).subscribe((res) => {
      if (res) {
        this.auth.setMessage('Senha redefinida com sucesso. Faça login.');
        this.switchView('login');
      }
    });
  }

  switchView(view: 'login' | 'register' | 'forgot' | 'reset') {
    this.view.set(view);
    this.password.set('');
    this.showPassword.set(false);
    this.showNewPassword.set(false);
    this.inviteCode.set('');
    this.displayName.set('');
    this.resetCode.set('');
    this.newPassword.set('');
    if (view !== 'reset') {
      this.resetToken.set('');
    }
    this.auth.clearError();
  }
}
