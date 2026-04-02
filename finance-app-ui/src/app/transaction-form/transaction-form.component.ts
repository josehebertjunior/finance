import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { FinanceService } from '../services/finance.service';

@Component({
  selector: 'app-transaction-form',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './transaction-form.component.html',
  styleUrl: './transaction-form.component.css'
})
export class TransactionFormComponent implements OnInit {
  financeService = inject(FinanceService);
  router = inject(Router);

  isInstallment: boolean = false;

  transaction: any = {
    description: '',
    amount: null,
    type: 1, // Default to Expense
    date: new Date().toISOString().split('T')[0],
    referenceMonth: new Date().toISOString().substring(0, 7), // YYYY-MM
    categoryId: null,
    personId: null,
    paymentMethodId: null,
    isFixed: false,
    installmentCurrent: 1,
    installmentTotal: 1
  };

  categories: any[] = [];
  persons: any[] = [];
  paymentMethods: any[] = [];

  ngOnInit() {
    this.financeService.getCategories().subscribe(res => this.categories = res);
    this.financeService.getPersons().subscribe(res => this.persons = res);
    this.financeService.getPaymentMethods().subscribe(res => this.paymentMethods = res);
  }

  save() {
    if (!this.isInstallment) {
      this.transaction.installmentTotal = 1;
    }
    
    // Convert YYYY-MM back to full date string for backend
    if (this.transaction.referenceMonth && this.transaction.referenceMonth.length === 7) {
      this.transaction.referenceMonth = `${this.transaction.referenceMonth}-01`;
    }

    this.financeService.createTransaction(this.transaction).subscribe({
      next: () => this.router.navigate(['/']),
      error: (e) => console.error(e)
    });
  }
}
