import { TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { of } from 'rxjs';
import { DashboardComponent } from './dashboard.component';
import { FinanceService } from '../services/finance.service';

describe('DashboardComponent', () => {
  let finance: {
    getPersons: ReturnType<typeof vi.fn>;
    getSavingsBalance: ReturnType<typeof vi.fn>;
    getTransactions: ReturnType<typeof vi.fn>;
    deleteTransaction: ReturnType<typeof vi.fn>;
  };

  beforeEach(async () => {
    finance = {
      getPersons: vi.fn(() => of([{ id: 7, name: 'Ana' }])),
      getSavingsBalance: vi.fn(() => of({ balance: 100 })),
      getTransactions: vi.fn(() => of([])),
      deleteTransaction: vi.fn(() => of({}))
    };
    await TestBed.configureTestingModule({
      imports: [DashboardComponent, RouterTestingModule],
      providers: [{ provide: FinanceService, useValue: finance }]
    }).compileComponents();
  });

  it('updates transactions immediately when a month filter is selected', () => {
    const component = TestBed.createComponent(DashboardComponent).componentInstance;
    component.ngOnInit();
    const initialCalls = finance.getTransactions.mock.calls.length;
    const extraMonth = component.availableMonths[1].id;

    component.toggleMonth(extraMonth);

    expect(component.selectedMonths).toContain(extraMonth);
    expect(finance.getTransactions.mock.calls.length).toBeGreaterThan(initialCalls);
  });

  it('updates transactions immediately when a person filter is selected', () => {
    const component = TestBed.createComponent(DashboardComponent).componentInstance;
    component.ngOnInit();
    const initialCalls = finance.getTransactions.mock.calls.length;

    component.togglePerson(7);

    expect(component.selectedPersons).toEqual([7]);
    expect(finance.getTransactions.mock.calls.length).toBeGreaterThan(initialCalls);
  });
});
