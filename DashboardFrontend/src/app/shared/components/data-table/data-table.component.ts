import { Component, input, output, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

/**
 * TableColumn — Interface for table column configuration.
 */
export interface TableColumn<T> {
  key: keyof T | string;
  label: string;
  sortable?: boolean;
  width?: string;
}

/**
 * PageChangeEvent — Event emitted when page changes.
 */
export interface PageChangeEvent {
  page: number;
  pageSize: number;
}

/**
 * SortChangeEvent — Event emitted when sort changes.
 */
export interface SortChangeEvent {
  column: string;
  direction: 'asc' | 'desc';
}

/**
 * DataTableComponent — Reusable data table with sorting and pagination.
 *
 * Features:
 * - Generic data binding
 * - Column configuration with optional sorting
 * - Client-side pagination
 * - Sortable columns
 * - Loading state
 * - Empty state
 * - Row click event
 *
 * Follows Angular Signals best practices from .claude/skills/angular-signals.md
 */
@Component({
  selector: 'app-data-table',
  imports: [FormsModule],
  templateUrl: './data-table.component.html',
  styleUrl: './data-table.component.css'
})
export class DataTableComponent<T extends Record<string, unknown>> {
  // ── Input Signals ─────────────────────────────────────────────
  readonly data = input.required<T[]>();
  readonly columns = input.required<TableColumn<T>[]>();
  readonly loading = input<boolean>(false);
  readonly pageSizeOptions = input<number[]>([10, 25, 50, 100]);
  readonly defaultPageSize = input<number>(10);

  // ── Output Signals ────────────────────────────────────────────
  readonly pageChange = output<PageChangeEvent>();
  readonly sortChange = output<SortChangeEvent>();
  readonly rowClick = output<T>();

  // ── Writable Signals (private) ────────────────────────────────
  private readonly _currentPage = signal(1);
  private readonly _pageSize = signal(10);
  private readonly _sortColumn = signal<string | null>(null);
  private readonly _sortDirection = signal<'asc' | 'desc'>('asc');

  // ── Readonly Signals (expose to template) ─────────────────────
  readonly currentPage = this._currentPage.asReadonly();
  readonly pageSize = this._pageSize.asReadonly();
  readonly sortColumn = this._sortColumn.asReadonly();
  readonly sortDirection = this._sortDirection.asReadonly();

  // ── Computed Signals ───────────────────────────────────────────
  readonly totalItems = computed(() => this.data().length);
  readonly totalPages = computed(() => Math.ceil(this.totalItems() / this._pageSize()) || 1);

  readonly paginatedData = computed(() => {
    const start = (this._currentPage() - 1) * this._pageSize();
    const end = start + this._pageSize();
    return this.data().slice(start, end);
  });

  readonly hasData = computed(() => this.data().length > 0);
  readonly hasPages = computed(() => this.totalPages() > 1);

  readonly pageNumbers = computed(() => {
    const total = this.totalPages();
    const current = this._currentPage();
    const pages: (number | '...')[] = [];

    if (total <= 7) {
      for (let i = 1; i <= total; i++) pages.push(i);
    } else {
      pages.push(1);
      if (current > 3) pages.push('...');
      for (let i = Math.max(2, current - 1); i <= Math.min(total - 1, current + 1); i++) {
        pages.push(i);
      }
      if (current < total - 2) pages.push('...');
      pages.push(total);
    }

    return pages;
  });

  // ── Public Methods ─────────────────────────────────────────────

  /**
   * Navigate to a specific page.
   */
  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this._currentPage()) return;
    this._currentPage.set(page);
    this.pageChange.emit({ page, pageSize: this._pageSize() });
  }

  /**
   * Change page size.
   */
  changePageSize(newSize: number): void {
    this._pageSize.set(newSize);
    this._currentPage.set(1);
    this.pageChange.emit({ page: 1, pageSize: newSize });
  }

  /**
   * Sort by column.
   */
  sortBy(column: TableColumn<T>): void {
    if (!column.sortable) return;

    const key = column.key as string;
    if (this._sortColumn() === key) {
      this._sortDirection.update((dir) => (dir === 'asc' ? 'desc' : 'asc'));
    } else {
      this._sortColumn.set(key);
      this._sortDirection.set('asc');
    }

    this.sortChange.emit({ column: key, direction: this._sortDirection() });
  }

  /**
   * Handle row click.
   */
  onRowClick(item: T): void {
    this.rowClick.emit(item);
  }

  /**
   * Get cell value from object by key path.
   */
  getCellValue(item: T, key: string): unknown {
    return key.split('.').reduce((obj: unknown, k) => {
      if (obj && typeof obj === 'object' && k in obj) {
        return (obj as Record<string, unknown>)[k];
      }
      return undefined;
    }, item);
  }

  /**
   * Check if column is currently sorted.
   */
  isSorted(column: TableColumn<T>): boolean {
    return this._sortColumn() === column.key;
  }
}
