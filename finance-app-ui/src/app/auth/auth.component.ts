import { Component, signal } from '@angular/core';
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
export class AuthComponent {
  view = signal<'login' | 'register' | 'forgot'>('login');
  email = signal('');
  password = signal('');
  displayName = signal('');

  constructor(public auth: AuthService) {}

  submitLogin() {
    this.auth.login(this.email(), this.password());
  }

  submitRegister() {
    this.auth.register(this.email(), this.password(), this.displayName());
  }

  submitForgot() {
    this.auth.clearError();
    alert('Recuperação de senha ainda não implementada.');
  }

  switchView(view: 'login' | 'register' | 'forgot') {
    this.view.set(view);
    this.password.set('');
    this.auth.clearError();
  }
}
