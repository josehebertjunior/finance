import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FinanceService } from '../services/finance.service';

type DashboardMonth = {
  year: number;
  month: number;
  id: string;
  label: string;
  transactions: any[];
  totalIncome: number;
  totalExpense: number;
  total: number;
};

type CreditCardGroup = {
  id: number | string;
  name: string;
  total: number;
  transactions: any[];
};

type FixedExpenseGroup = {
  total: number;
  transactions: any[];
};

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css'
})
export class DashboardComponent implements OnInit {
  private financeService = inject(FinanceService);
  private cdr = inject(ChangeDetectorRef);

  savingsBalance = 0;
  availableMonths: { id: string; label: string }[] = [];
  selectedMonths: string[] = [];
  availablePersons: any[] = [];
  selectedPersons: number[] = [];
  months: DashboardMonth[] = [];
  pendingDeletion: any | null = null;
  // Grouped bills start compact; the user expands only what they want to inspect.
  private expandedGroups = new Set<string>();

  get totalIncome() { return this.months.reduce((total, month) => total + month.totalIncome, 0); }
  get totalExpense() { return this.months.reduce((total, month) => total + month.totalExpense, 0); }
  get balance() { return this.totalIncome - this.totalExpense; }
  get allTransactions() { return this.months.flatMap(month => month.transactions); }

  ngOnInit() {
    this.financeService.getPersons().subscribe(res => {
      this.availablePersons = res;
      this.cdr.markForCheck();
    });

    const now = new Date();
    for (let offset = -3; offset <= 12; offset++) {
      const date = new Date(now.getFullYear(), now.getMonth() + offset, 1);
      this.availableMonths.push({
        id: this.monthId(date.getFullYear(), date.getMonth() + 1),
        label: date.toLocaleDateString('pt-BR', { month: 'short', year: 'numeric' })
      });
    }

    this.selectedMonths = [this.monthId(now.getFullYear(), now.getMonth() + 1)];
    this.loadSelectedMonths();
    this.loadSavings();
  }

  toggleMonth(monthId: string) {
    this.selectedMonths = this.selectedMonths.includes(monthId)
      ? this.selectedMonths.filter(id => id !== monthId)
      : [...this.selectedMonths, monthId];
    this.loadSelectedMonths();
  }

  togglePerson(personId: number) {
    this.selectedPersons = this.selectedPersons.includes(personId)
      ? this.selectedPersons.filter(id => id !== personId)
      : [...this.selectedPersons, personId];
    this.loadTransactions();
  }

  clearPersonFilter() {
    this.selectedPersons = [];
    this.loadTransactions();
  }

  isGroupExpanded(key: string) { return this.expandedGroups.has(key); }
  toggleGroup(key: string) { if (this.expandedGroups.has(key)) this.expandedGroups.delete(key); else this.expandedGroups.add(key); }
  fixedGroupKey(month: DashboardMonth) { return `${month.id}:fixed`; }
  creditGroupKey(month: DashboardMonth, group: CreditCardGroup) { return `${month.id}:credit:${group.id}`; }

  requestDeletion(transaction: any) {
    if (this.hasSeries(transaction)) {
      this.pendingDeletion = transaction;
      return;
    }
    if (confirm('Excluir este lançamento?')) this.deleteTransaction(transaction.id, 'current');
  }

  cancelDeletion() {
    this.pendingDeletion = null;
  }

  confirmDeletion(scope: 'current' | 'series') {
    if (!this.pendingDeletion) return;
    this.deleteTransaction(this.pendingDeletion.id, scope);
    this.pendingDeletion = null;
  }

  private deleteTransaction(id: number, scope: 'current' | 'series') {
    this.financeService.deleteTransaction(id, scope).subscribe(() => {
      this.loadTransactions();
      this.loadSavings();
    });
  }

  directTransactions(month: DashboardMonth) {
    return month.transactions.filter(transaction => !this.isCreditExpense(transaction) && !this.isRecurringDirectExpense(transaction));
  }

  creditCardGroups(month: DashboardMonth): CreditCardGroup[] {
    const groups = new Map<number | string, CreditCardGroup>();
    month.transactions.filter(transaction => this.isCreditExpense(transaction)).forEach(transaction => {
      const id = transaction.paymentMethodId ?? transaction.paymentMethod?.id ?? 'credit-card';
      const name = transaction.paymentMethod?.name || 'Cartão de crédito';
      const group: CreditCardGroup = groups.get(id) ?? { id, name, total: 0, transactions: [] };
      group.transactions.push(transaction);
      group.total += Number(transaction.amount);
      groups.set(id, group);
    });
    return [...groups.values()].sort((a, b) => a.name.localeCompare(b.name));
  }

  fixedExpenseGroup(month: DashboardMonth): FixedExpenseGroup | null {
    const transactions = month.transactions.filter(transaction => this.isRecurringDirectExpense(transaction));
    if (!transactions.length) return null;
    return {
      transactions,
      total: transactions.reduce((sum, transaction) => sum + Number(transaction.amount), 0)
    };
  }

  private loadSavings() {
    this.financeService.getSavingsBalance().subscribe(res => {
      this.savingsBalance = res.balance;
      this.cdr.markForCheck();
    });
  }

  private loadSelectedMonths() {
    this.months = this.selectedMonths
      .map(id => this.monthFromId(id))
      .sort((a, b) => (a.year * 100 + a.month) - (b.year * 100 + b.month));
    this.loadTransactions();
  }

  private loadTransactions() {
    this.months.forEach(month => {
      this.financeService.getTransactions(month.year, month.month).subscribe(res => {
        month.transactions = this.selectedPersons.length
          ? res.filter((transaction: any) => transaction.personId != null && this.selectedPersons.includes(Number(transaction.personId)))
          : res;
        this.calculateTotals(month);
        this.cdr.markForCheck();
      });
    });
  }

  private monthFromId(id: string): DashboardMonth {
    const [year, month] = id.split('-').map(Number);
    return {
      id,
      year,
      month,
      label: new Date(year, month - 1, 1).toLocaleDateString('pt-BR', { month: 'long', year: 'numeric' }),
      transactions: [],
      totalIncome: 0,
      totalExpense: 0,
      total: 0
    };
  }

  private calculateTotals(month: DashboardMonth) {
    month.totalIncome = month.transactions.filter(transaction => transaction.type === 0).reduce((sum, transaction) => sum + Number(transaction.amount), 0);
    month.totalExpense = month.transactions.filter(transaction => transaction.type === 1).reduce((sum, transaction) => sum + Number(transaction.amount), 0);
    month.total = month.totalIncome - month.totalExpense;
  }

  private isCreditExpense(transaction: any) {
    return transaction.type === 1 && !!transaction.paymentMethod?.isCreditCard;
  }

  private isRecurringDirectExpense(transaction: any) {
    return transaction.type === 1
      && (transaction.isFixed || Number(transaction.installmentTotal) > 1)
      && !this.isCreditExpense(transaction);
  }

  private hasSeries(transaction: any) {
    return !!transaction.installmentGroupId && (transaction.isFixed || transaction.installmentTotal > 1);
  }

  private monthId(year: number, month: number) {
    return `${year}-${String(month).padStart(2, '0')}`;
  }
}
