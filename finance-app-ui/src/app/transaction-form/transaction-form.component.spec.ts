import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of } from 'rxjs';
import { TransactionFormComponent } from './transaction-form.component';
import { FinanceService } from '../services/finance.service';

describe('TransactionFormComponent', () => {
  let finance: {
    getCategories: ReturnType<typeof vi.fn>;
    getPersons: ReturnType<typeof vi.fn>;
    getPaymentMethods: ReturnType<typeof vi.fn>;
    createTransaction: ReturnType<typeof vi.fn>;
  };
  let router: { navigate: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    finance = {
      getCategories: vi.fn(() => of([{ id: 1, name: 'Moradia' }])),
      getPersons: vi.fn(() => of([{ id: 2, name: 'Ana' }])),
      getPaymentMethods: vi.fn(() => of([{ id: 3, name: 'Cartão' }])),
      createTransaction: vi.fn(() => of({}))
    };
    router = { navigate: vi.fn() };
    await TestBed.configureTestingModule({
      imports: [TransactionFormComponent],
      providers: [
        { provide: FinanceService, useValue: finance },
        { provide: Router, useValue: router },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null } } } }
      ]
    }).compileComponents();
  });

  it('loads form options on initialization', () => {
    const component = TestBed.createComponent(TransactionFormComponent).componentInstance;

    component.ngOnInit();

    expect(component.categories).toEqual([{ id: 1, name: 'Moradia' }]);
    expect(component.persons).toEqual([{ id: 2, name: 'Ana' }]);
    expect(component.paymentMethods).toEqual([{ id: 3, name: 'Cartão' }]);
  });

  it('normalizes the reference month and returns to the dashboard after save', () => {
    const component = TestBed.createComponent(TransactionFormComponent).componentInstance;
    component.transaction = {
      description: 'Aluguel', amount: '850.50', type: 1, date: '2026-07-05',
      referenceMonth: '2026-07', installmentTotal: 3
    };
    component.isInstallment = false;

    component.save();

    expect(finance.createTransaction).toHaveBeenCalledWith(expect.objectContaining({
      amount: 850.5,
      referenceMonth: '2026-07-01',
      installmentTotal: 1
    }));
    expect(router.navigate).toHaveBeenCalledWith(['/']);
  });
});
