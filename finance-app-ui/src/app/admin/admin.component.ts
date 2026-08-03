import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { AuthService } from '../services/auth.service';
import { environment } from '../../environments/environment';

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './admin.component.html',
  styleUrls: ['./admin.component.css']
})
export class AdminComponent implements OnInit {
  http = inject(HttpClient);
  auth = inject(AuthService);
  private cdr = inject(ChangeDetectorRef);

  readonly apiUrl = `${environment.apiUrl}/admin`;

  invites: any[] = [];
  tenants: any[] = [];
  users: any[] = [];

  newInvite = { email: '', tenantName: '' };
  roleAssignment = { userId: '', role: 'User' };
  tenantAssignment = { userId: '', tenantId: '' };
  createdInviteUrl = '';
  copiedInviteUrl = false;

  ngOnInit() {
    this.loadAdmin();
  }

  loadAdmin() {
    this.http.get<any[]>(`${this.apiUrl}/invites`).subscribe(res => { this.invites = res; this.cdr.markForCheck(); });
    this.http.get<any[]>(`${this.apiUrl}/tenants`).subscribe(res => { this.tenants = res; this.cdr.markForCheck(); });
    this.http.get<any[]>(`${this.apiUrl}/users`).subscribe(res => { this.users = res; this.cdr.markForCheck(); });
  }

  createInvite() {
    if (!this.newInvite.email || !this.newInvite.tenantName) return;
    this.http.post<any>(`${this.apiUrl}/invites`, this.newInvite).subscribe(response => {
      this.createdInviteUrl = response.inviteUrl;
      this.copiedInviteUrl = false;
      this.newInvite = { email: '', tenantName: '' };
      this.loadAdmin();
    });
  }

  inviteUrl(token: string) {
    return `${window.location.origin}/login?invite=${encodeURIComponent(token)}`;
  }

  async copyInviteUrl(url: string) {
    await navigator.clipboard.writeText(url);
    this.createdInviteUrl = url;
    this.copiedInviteUrl = true;
    this.cdr.markForCheck();
  }

  assignRole() {
    if (!this.roleAssignment.userId || !this.roleAssignment.role) return;
    this.http.post(`${this.apiUrl}/users/${this.roleAssignment.userId}/roles`, this.roleAssignment).subscribe(() => {
      this.loadAdmin();
    });
  }

  assignTenant() {
    if (!this.tenantAssignment.userId || !this.tenantAssignment.tenantId) return;
    this.http.post(`${this.apiUrl}/users/${this.tenantAssignment.userId}/assign-tenant`, {
      tenantId: this.tenantAssignment.tenantId
    }).subscribe(() => this.loadAdmin());
  }
}
