import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FinanceService } from '../services/finance.service';

@Component({ selector: 'app-pdf-import', standalone: true, imports: [CommonModule, FormsModule], templateUrl: './pdf-import.component.html', styleUrl: './pdf-import.component.css' })
export class PdfImportComponent implements OnInit {
  private finance = inject(FinanceService);
  methods: any[] = [];
  file?: File;
  paymentMethodId?: number;
  referenceMonth = new Date().toISOString().slice(0, 7) + '-01';
  items: any[] = [];
  busy = false;

  ngOnInit() { this.finance.getPaymentMethods().subscribe(methods => this.methods = methods); }
  selectFile(event: Event) { this.file = (event.target as HTMLInputElement).files?.[0]; this.items = []; }
  preview() {
    if (!this.file || !this.paymentMethodId) return;
    this.busy = true;
    this.finance.previewPdfStatement(this.file, this.paymentMethodId, this.referenceMonth).subscribe({ next: result => { this.items = result.items.map((item: any) => ({ ...item, selected: true })); this.busy = false; }, error: () => this.busy = false });
  }
  confirm() {
    const items = this.items.filter(item => item.selected).map(item => ({ date: item.date, referenceMonth: this.referenceMonth, description: item.description, amount: item.amount, type: item.type, paymentMethodId: this.paymentMethodId }));
    if (!items.length || !this.paymentMethodId) return;
    this.busy = true;
    this.finance.confirmPdfImport(items).subscribe({ next: () => { this.items = []; this.file = undefined; this.busy = false; }, error: () => this.busy = false });
  }
}
