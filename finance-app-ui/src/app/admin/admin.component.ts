import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { AuthService } from '../services/auth.service';

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

  apiUrl = 'http://localhost:5078/api/admin';

  invites: any[] = [];
  tenants: any[] = [];
  users: any[] = [];

  newInvite = { email: '', tenantName: '' };
  roleAssignment = { userId: '', role: 'User' };

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
    this.http.post(`${this.apiUrl}/invites`, this.newInvite).subscribe(() => {
      this.newInvite = { email: '', tenantName: '' };
      this.loadAdmin();
    });
  }

  assignRole() {
    if (!this.roleAssignment.userId || !this.roleAssignment.role) return;
    this.http.post(`${this.apiUrl}/users/${this.roleAssignment.userId}/roles`, this.roleAssignment).subscribe(() => {
      this.loadAdmin();
    });
  }
}
