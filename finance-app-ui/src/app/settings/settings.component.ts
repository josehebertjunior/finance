import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { FinanceService } from '../services/finance.service';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './settings.component.html'
})
export class SettingsComponent implements OnInit {
  financeService = inject(FinanceService);
  http = inject(HttpClient);
  apiUrl = 'http://localhost:5078/api';

  activeTab = 'persons'; // 'persons', 'methods', 'categories'

  categories: any[] = [];
  persons: any[] = [];
  paymentMethods: any[] = [];

  newCategory = { name: '', colorHex: '#ffffff' };
  newPerson = { name: '' };
  newPaymentMethod = { name: '', isCreditCard: false };

  ngOnInit() {
    this.loadData();
  }

  loadData() {
    this.financeService.getCategories().subscribe(res => this.categories = res);
    this.financeService.getPersons().subscribe(res => this.persons = res);
    this.financeService.getPaymentMethods().subscribe(res => this.paymentMethods = res);
  }

  addCategory() {
    if (!this.newCategory.name) return;
    this.http.post(`${this.apiUrl}/categories`, this.newCategory).subscribe(() => {
      this.newCategory = { name: '', colorHex: '#ffffff' };
      this.loadData();
    });
  }

  deleteCategory(id: number) {
    if(confirm('Excluir categoria?')) this.financeService.deleteCategory(id).subscribe(() => this.loadData());
  }

  addPerson() {
    if (!this.newPerson.name) return;
    this.http.post(`${this.apiUrl}/persons`, this.newPerson).subscribe(() => {
      this.newPerson = { name: '' };
      this.loadData();
    });
  }

  deletePerson(id: number) {
    if(confirm('Excluir pessoa?')) this.financeService.deletePerson(id).subscribe(() => this.loadData());
  }

  addPaymentMethod() {
    if (!this.newPaymentMethod.name) return;
    this.http.post(`${this.apiUrl}/paymentmethods`, this.newPaymentMethod).subscribe(() => {
      this.newPaymentMethod = { name: '', isCreditCard: false };
      this.loadData();
    });
  }

  deletePaymentMethod(id: number) {
    if(confirm('Excluir método?')) this.financeService.deletePaymentMethod(id).subscribe(() => this.loadData());
  }
}
