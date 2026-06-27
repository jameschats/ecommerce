import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { TokenStorageService } from '../services/token-storage.service';

/** Attaches the JWT; on a 401 tries a single silent refresh, then retries. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const storage = inject(TokenStorageService);
  const auth = inject(AuthService);
  const router = inject(Router);

  const isAuthCall = req.url.includes('/auth/');
  const token = storage.getAccessToken();
  const authReq = token && !isAuthCall
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(authReq).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401 && !isAuthCall && storage.getRefreshToken()) {
        return auth.refresh().pipe(
          switchMap((res) =>
            next(req.clone({ setHeaders: { Authorization: `Bearer ${res.accessToken}` } })),
          ),
          catchError((refreshErr) => {
            auth.logout();
            router.navigate(['/login']);
            return throwError(() => refreshErr);
          }),
        );
      }
      return throwError(() => err);
    }),
  );
};
