import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
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
  private route = inject(ActivatedRoute);
  private cdr = inject(ChangeDetectorRef);
  editingId: number | null = null;
  applyToSeries = false;

  isInstallment: boolean = false;

  transaction: any = {
    description: '',
    amount: null,
    type: 1, // Default to Expense
    date: new Date().toISOString().split('T')[0],
    referenceMonth: `${new Date().getFullYear()}-${String(new Date().getMonth() + 1).padStart(2, '0')}`,
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
    this.financeService.getCategories().subscribe(res => { this.categories = res; this.cdr.markForCheck(); });
    this.financeService.getPersons().subscribe(res => { this.persons = res; this.cdr.markForCheck(); });
    this.financeService.getPaymentMethods().subscribe(res => { this.paymentMethods = res; this.cdr.markForCheck(); });

    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (Number.isFinite(id) && id > 0) {
      this.editingId = id;
      this.financeService.getTransaction(id).subscribe(transaction => {
        this.transaction = {
          ...transaction,
          date: transaction.date?.slice(0, 10),
          referenceMonth: transaction.referenceMonth?.slice(0, 7),
          installmentCurrent: transaction.installmentCurrent ?? 1,
          installmentTotal: transaction.installmentTotal ?? 1
        };
        this.isInstallment = this.transaction.installmentTotal > 1;
        this.cdr.markForCheck();
      });
    }
  }

  onInstallmentChange() {
    if (this.isInstallment) this.transaction.isFixed = false;
  }

  onFixedChange() {
    if (this.transaction.isFixed) {
      this.isInstallment = false;
      this.transaction.installmentCurrent = 1;
      this.transaction.installmentTotal = 1;
    }
  }

  get canApplyToSeries() {
    return !!this.editingId && !!this.transaction.installmentGroupId
      && (this.transaction.isFixed || this.transaction.installmentTotal > 1);
  }

  save() {
    if (!this.isInstallment) {
      this.transaction.installmentTotal = 1;
    }

    const transactionPayload = {
      ...this.transaction,
      referenceMonth: this.transaction.referenceMonth && this.transaction.referenceMonth.length === 7
        ? `${this.transaction.referenceMonth}-01`
        : this.transaction.referenceMonth,
      amount: Number(this.transaction.amount)
    };

    const request = this.editingId
      ? this.financeService.updateTransaction(this.editingId, transactionPayload, this.applyToSeries ? 'series' : 'current')
      : this.financeService.createTransaction(transactionPayload);

    request.subscribe({
      next: () => this.router.navigate(['/']),
      error: (e) => console.error(e)
    });
  }
}
