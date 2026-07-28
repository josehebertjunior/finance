import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { FinanceService } from './finance.service';
import { environment } from '../../environments/environment';

describe('FinanceService', () => {
  let service: FinanceService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [FinanceService]
    });

    service = TestBed.inject(FinanceService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should fetch categories', () => {
    const mockCategories = [{ id: 1, name: 'Alimentação', colorHex: '#ff0000' }];

    service.getCategories().subscribe(categories => {
      expect(categories).toEqual(mockCategories);
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/categories`);
    expect(req.request.method).toBe('GET');
    req.flush(mockCategories);
  });

  it('should fetch transactions for year and month', () => {
    const mockTransactions = [{ id: 1, description: 'Teste', amount: 10, type: 1, date: '2026-07-01', referenceMonth: '2026-07-01' }];

    service.getTransactions(2026, 7).subscribe(transactions => {
      expect(transactions).toEqual(mockTransactions);
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/transactions?year=2026&month=7`);
    expect(req.request.method).toBe('GET');
    req.flush(mockTransactions);
  });
});
