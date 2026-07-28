# Angular Signals Skill

## Trigger
當使用者需要實作 Angular 19 前端功能、建立元件、管理狀態、或處理 HTTP 請求時使用此技能。

## 架構概覽

本專案使用 Angular 19 Standalone Components（非 NgModules）搭配 Signals 進行狀態管理：

```
┌─────────────────────────────────────────────────────────┐
│                    Angular 19 架構                       │
├─────────────────────────────────────────────────────────┤
│  Standalone Components (無 NgModules)                    │
│  ↓                                                      │
│  Signals 狀態管理 (signal, computed, effect)             │
│  ↓                                                      │
│  Services with inject()                                 │
│  ↓                                                      │
│  HttpClient + Interceptors                              │
│  ↓                                                      │
│  API Backend (.NET 8)                                   │
└─────────────────────────────────────────────────────────┘
```

### 專案結構

| 資料夾 | 用途 |
|--------|------|
| `src/app/core/models/` | TypeScript 介面與型別定義 |
| `src/app/core/services/` | Angular Services（injectable） |
| `src/app/core/interceptors/` | HTTP interceptors |
| `src/app/core/guards/` | Route guards |
| `src/app/core/` | 共用元件、工具函式 |

### 關鍵檔案

| 檔案 | 用途 |
|------|------|
| [app.config.ts](src/app/app.config.ts) | App 設定、providers |
| [auth.service.ts](src/app/core/services/auth.service.ts) | JWT 認證、token 管理 |
| [api.interceptor.ts](src/app/core/interceptors/api.interceptor.ts) | API 回應解包 |
| [auth.interceptor.ts](src/app/core/interceptors/auth.interceptor.ts) | JWT header 附加 |

## 必要模式

### 1. Standalone Component 基本結構

```typescript
// src/app/components/example/example.component.ts
import { Component, input, output, signal, computed } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-example',
  imports: [RouterOutlet],  // Standalone: 直接 import 需要的元件
  templateUrl: './example.component.html',
  styleUrl: './example.component.css'
})
export class ExampleComponent {
  // Input signals (Angular 17+)
  title = input.required<string>();
  count = input<number>(0);
  
  // Output signals
  valueChange = output<number>();
  
  // Writable signal
  private readonly _data = signal<string[]>([]);
  
  // Readonly signal (expose to template)
  readonly data = this._data.asReadonly();
  
  // Computed signal
  readonly isEmpty = computed(() => this.data().length === 0);
  readonly dataCount = computed(() => this.data().length);
  
  // 同步更新
  addItem(item: string): void {
    this._data.update(items => [...items, item]);
  }
  
  // 替換更新
  clearItems(): void {
    this._data.set([]);
  }
  
  // 非同步操作
  async loadData(): Promise<void> {
    const result = await fetch('/api/data');
    this._data.set(await result.json());
  }
}
```

### 2. Service 與 inject()

```typescript
// src/app/core/services/example.service.ts
import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ExampleService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;
  
  // Service-level signal for state
  readonly items = signal<Item[]>([]);
  readonly isLoading = signal(false);
  readonly error = signal<string | null>(null);
  
  loadItems(): Observable<Item[]> {
    this.isLoading.set(true);
    this.error.set(null);
    
    return this.http.get<Item[]>(`${this.baseUrl}/items`).pipe(
      tap({
        next: (items) => this.items.set(items),
        error: (err) => this.error.set(err.message),
        finalize: () => this.isLoading.set(false)
      })
    );
  }
}
```

### 3. API Response 包裝器處理

API 回應格式：
```json
{
  "success": true,
  "data": { ... },
  "message": "...",
  "timestamp": "..."
}
```

```typescript
// src/app/core/interceptors/api.interceptor.ts
import { HttpInterceptorFn, HttpResponse } from '@angular/common/http';

export const apiInterceptor: HttpInterceptorFn = (req, next) => {
  return next(req).pipe(
    // 只對成功回應進行解包
    filter((event) => event instanceof HttpResponse),
    map((event) => event as HttpResponse<ApiResult<unknown>>),
    map((event) => {
      const body = event.body;
      // 如果是標準 ApiResult 格式，解包 data
      if (body && 'success' in body && 'data' in body) {
        return event.clone({ body: body.data });
      }
      return event;
    })
  );
};
```

### 4. JWT Interceptor

```typescript
// src/app/core/interceptors/auth.interceptor.ts
import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const token = authService.token();
  
  if (token && !req.url.includes('/auth/')) {
    const authReq = req.clone({
      setHeaders: { Authorization: `Bearer ${token}` }
    });
    return next(authReq);
  }
  
  return next(req);
};
```

### 5. Auth Guard

```typescript
// src/app/core/guards/auth.guard.ts
import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);
  
  if (authService.isLoggedIn()) {
    return true;
  }
  
  return router.createUrlTree(['/login']);
};
```

### 6. App Config 設定

```typescript
// src/app/app.config.ts
import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';

import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { apiInterceptor } from './core/interceptors/api.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(
      withInterceptors([authInterceptor, apiInterceptor]),
    ),
  ],
};
```

### 7. Model 定義

```typescript
// src/app/core/models/example.model.ts
export interface ApiResult<T> {
  success: boolean;
  data: T;
  message: string;
  timestamp: string;
}

// API Request/Response 型別
export interface LoginRequest {
  email: string;
  password: string;
}

export interface TokenPairResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}

export interface UserProfile {
  id: string;
  email: string;
  displayName: string;
}
```

### 8. Routes 設定

```typescript
// src/app/app.routes.ts
import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/home/home.component')
      .then(m => m.HomeComponent),
    canActivate: [authGuard]
  },
  {
    path: 'login',
    loadComponent: () => import('./pages/login/login.component')
      .then(m => m.LoginComponent)
  },
  {
    path: '**',
    redirectTo: ''
  }
];
```

## Signals 進階用法

### 1. Effect 副作用

```typescript
import { effect, inject } from '@angular/core';
import { AuthService } from '../services/auth.service';

@Component({...})
export class ProfileComponent {
  private readonly authService = inject(AuthService);
  
  constructor() {
    // 當 token 變化時執行副作用
    effect(() => {
      const token = this.authService.token();
      if (token) {
        console.log('Token updated:', token.substring(0, 10) + '...');
      }
    });
  }
}
```

### 2. Signals 之間的依賴

```typescript
@Component({...})
export class CartComponent {
  // 多個 signals 組合計算
  readonly items = signal<CartItem[]>([]);
  readonly discount = signal(0);
  
  readonly subtotal = computed(() => 
    this.items().reduce((sum, item) => sum + item.price * item.quantity, 0)
  );
  
  readonly discountAmount = computed(() => 
    this.subtotal() * (this.discount() / 100)
  );
  
  readonly total = computed(() => 
    this.subtotal() - this.discountAmount()
  );
}
```

### 3. 非同步 Signals（使用 toSignal）

```typescript
import { toSignal } from '@angular/core/rxjs-interop';
import { inject } from '@angular/core';
import { GameService } from '../services/game.service';
import { map, startWith } from 'rxjs';

@Component({...})
export class GameListComponent {
  private readonly gameService = inject(GameService);
  
  // 將 Observable 轉換為 Signal
  readonly games = toSignal(
    this.gameService.getGames().pipe(startWith([])),
    { initialValue: [] }
  );
  
  readonly isLoading = toSignal(this.gameService.loading$, { initialValue: false });
  
  // 使用 computed 進行轉換
  readonly gameCount = computed(() => this.games().length);
  readonly popularGames = computed(() => 
    this.games()
      .filter(g => g.playCount > 100)
      .sort((a, b) => b.playCount - a.playCount)
  );
}
```

## 測試 Signals

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';

describe('ExampleComponent', () => {
  let component: ExampleComponent;
  let fixture: ComponentFixture<ExampleComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ExampleComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(ExampleComponent);
    component = fixture.componentInstance;
  });

  it('should update signal value', () => {
    component.addItem('test');
    expect(component.data()).toContain('test');
  });

  it('should compute derived values', () => {
    component.addItem('a');
    component.addItem('b');
    expect(component.isEmpty()).toBe(false);
    expect(component.dataCount()).toBe(2);
  });
});
```

## 常用指令

```bash
# 啟動開發伺服器
cd DashboardFrontend
npm start              # port 4200

# 建置生產版本
npm run build

# 執行測試
npm test

# Lint
npm run lint
```

## 約束檢查清單

- [ ] 使用 Standalone Components（無 NgModules）
- [ ] 優先使用 Signals 而非 RxJS BehaviorSubject
- [ ] Services 使用 `inject()` 而非 constructor injection
- [ ] HTTP 請求透過 HttpClient（不使用 fetch）
- [ ] API interceptor 處理 `ApiResult<T>` 解包
- [ ] JWT interceptor 自動附加 Authorization header
- [ ] 受保護路由使用 authGuard
- [ ] 所有 HTTP 操作都是 Observable（非直接 Promise）
