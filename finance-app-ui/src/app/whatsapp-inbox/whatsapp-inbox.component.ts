import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { FinanceService } from '../services/finance.service';
import { AuthService } from '../services/auth.service';
import { environment } from '../../environments/environment';

@Component({
  selector: 'app-whatsapp-inbox',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './whatsapp-inbox.component.html',
  styleUrl: './whatsapp-inbox.component.css'
})
export class WhatsAppInboxComponent implements OnInit {
  private readonly finance = inject(FinanceService);
  private readonly http = inject(HttpClient);
  readonly auth = inject(AuthService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly apiUrl = environment.apiUrl;

  items: any[] = [];
  senders: any[] = [];
  users: any[] = [];
  persons: any[] = [];
  status: any = { webhookConfigured: false, appSecretConfigured: false };
  loading = true;
  newSender = { phoneNumber: '', displayName: '', ownerId: '', personId: null as number | null };

  ngOnInit() {
    this.loadInbox();
    if (this.auth.hasRole('Admin')) this.loadAdminConfiguration();
  }

  loadInbox() {
    this.loading = true;
    this.finance.getWhatsAppInbox().subscribe({
      next: items => { this.items = items; this.loading = false; this.cdr.markForCheck(); },
      error: () => { this.loading = false; this.cdr.markForCheck(); }
    });
  }

  confirm(item: any) { this.finance.confirmWhatsAppInbox(item.id).subscribe(() => this.loadInbox()); }

  ignore(item: any) {
    if (confirm('Ignorar esta mensagem sem criar lançamento?')) this.finance.ignoreWhatsAppInbox(item.id).subscribe(() => this.loadInbox());
  }

  private loadAdminConfiguration() {
    this.http.get<any>(`${this.apiUrl}/whatsapp/status`).subscribe(result => { this.status = result; this.cdr.markForCheck(); });
    this.http.get<any[]>(`${this.apiUrl}/whatsapp/senders`).subscribe(result => { this.senders = result; this.cdr.markForCheck(); });
    this.http.get<any[]>(`${this.apiUrl}/admin/users`).subscribe(result => { this.users = result; this.cdr.markForCheck(); });
    this.finance.getPersons().subscribe(result => { this.persons = result; this.cdr.markForCheck(); });
  }

  addSender() {
    if (!this.newSender.phoneNumber || !this.newSender.ownerId) return;
    this.http.post(`${this.apiUrl}/whatsapp/senders`, this.newSender).subscribe(() => {
      this.newSender = { phoneNumber: '', displayName: '', ownerId: '', personId: null };
      this.loadAdminConfiguration();
    });
  }

  deleteSender(id: number) {
    if (confirm('Remover este número autorizado?')) this.http.delete(`${this.apiUrl}/whatsapp/senders/${id}`).subscribe(() => this.loadAdminConfiguration());
  }

  formatCurrency(value: number | null) {
    return value == null ? 'Ajuste necessário' : new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(value);
  }
}
