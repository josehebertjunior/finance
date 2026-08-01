import { Component, signal, inject } from '@angular/core';
import { Router, RouterOutlet, RouterModule, NavigationEnd } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService } from './services/auth.service';
import { ErrorMessageService } from './services/error-message.service';

@Component({
  selector: 'app-root',
  imports: [CommonModule, RouterOutlet, RouterModule],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  protected readonly showNav = signal(true);
  protected readonly auth = inject(AuthService);
  protected readonly errorMessages = inject(ErrorMessageService);

  constructor(router: Router) {
    router.events.subscribe((event) => {
      if (event instanceof NavigationEnd) {
        this.showNav.set(!event.urlAfterRedirects.startsWith('/login'));
      }
    });
  }

  logout() {
    this.auth.clear();
  }
}
