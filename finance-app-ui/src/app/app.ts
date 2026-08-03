import { Component, signal, inject } from '@angular/core';
import { Router, RouterOutlet, RouterModule, NavigationEnd } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from './services/auth.service';
import { ErrorMessageService } from './services/error-message.service';

@Component({
  selector: 'app-root',
  imports: [CommonModule, FormsModule, RouterOutlet, RouterModule],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  protected readonly showShell = signal(true);
  protected readonly sidebarOpen = signal(false);
  protected readonly auth = inject(AuthService);
  protected readonly errorMessages = inject(ErrorMessageService);

  constructor(router: Router) {
    router.events.subscribe((event) => {
      if (event instanceof NavigationEnd) {
        this.showShell.set(!event.urlAfterRedirects.startsWith('/login') && this.auth.isAuthenticated());
        if (this.showShell()) this.auth.loadGroups();
        this.sidebarOpen.set(false);
      }
    });
  }

  logout() {
    this.auth.clear();
  }

  toggleSidebar() {
    this.sidebarOpen.set(!this.sidebarOpen());
  }
}
