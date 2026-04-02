import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { FinanceService } from '../services/finance.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css'
})
export class DashboardComponent implements OnInit {
  financeService = inject(FinanceService);
  
  savingsBalance: number = 0;
  
  availableMonths: { id: string, label: string }[] = [];
  selectedMonths: string[] = []; // YYYY-MM array
  
  availablePersons: any[] = [];
  selectedPersons: number[] = []; // Person ID array

  months: { year: number, month: number, label: string, transactions: any[], totalIncome: number, totalExpense: number, total: number }[] = [];

  ngOnInit() {
    this.financeService.getPersons().subscribe(res => this.availablePersons = res);

    let now = new Date();
    for(let i = -3; i <= 12; i++) {
        let d = new Date(now.getFullYear(), now.getMonth() + i, 1);
        let id = `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2, '0')}`;
        this.availableMonths.push({ 
           id: id, 
           label: d.toLocaleString('pt-BR', { month: 'long', year: 'numeric' }) 
        });
    }
    
    let currentId = `${now.getFullYear()}-${String(now.getMonth()+1).padStart(2, '0')}`;
    this.selectedMonths = [currentId];

    this.generateMonths();
    this.loadSavings();
  }

  loadSavings() {
    this.financeService.getSavingsBalance().subscribe(res => {
      this.savingsBalance = res.balance;
    });
  }

  generateMonths() {
    if (!this.selectedMonths || this.selectedMonths.length === 0) {
      this.months = [];
      return;
    }
    
    this.months = [];
    this.selectedMonths.forEach(mStr => {
      let parts = mStr.split('-');
      let y = parseInt(parts[0]);
      let m = parseInt(parts[1]);
      let d = new Date(y, m - 1, 1);
      
      this.months.push({ 
          year: y, 
          month: m, 
          label: d.toLocaleString('pt-BR', { month: 'long', year: 'numeric' }),
          transactions: [],
          totalIncome: 0,
          totalExpense: 0,
          total: 0
      });
    });

    this.months.sort((a,b) => (a.year * 100 + a.month) - (b.year * 100 + b.month));
    this.loadTransactions();
  }

  onFilterChange() {
    this.generateMonths();
  }

  loadTransactions() {
    this.months.forEach(m => {
      this.financeService.getTransactions(m.year, m.month).subscribe(res => {
        let filtered = res;
        if (this.selectedPersons && this.selectedPersons.length > 0) {
           filtered = filtered.filter((t: any) => t.personId != null && this.selectedPersons.includes(t.personId));
        }
        m.transactions = filtered;
        this.calculateTotals(m);
      });
    });
  }

  calculateTotals(m: any) {
    m.totalIncome = m.transactions.filter((t:any) => t.type === 0).reduce((a:any, b:any) => a + b.amount, 0);
    m.totalExpense = m.transactions.filter((t:any) => t.type === 1).reduce((a:any, b:any) => a + b.amount, 0);
    m.total = m.totalIncome - m.totalExpense;
  }

  deleteTransaction(id: number) {
    if(confirm('Tem certeza que deseja excluir este lançamento?')) {
      this.financeService.deleteTransaction(id).subscribe(() => {
        this.loadTransactions();
      });
    }
  }
}
