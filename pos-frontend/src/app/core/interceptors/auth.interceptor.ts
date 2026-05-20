import { HttpInterceptorFn } from '@angular/common/http';

const TENANT_TOKEN_STORAGE_KEY = 'auth_token';
const PLATFORM_TOKEN_STORAGE_KEY = 'platform_auth_token';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const isPlatformRequest = req.url.startsWith('/api/platform');
  const token = isPlatformRequest
    ? localStorage.getItem(PLATFORM_TOKEN_STORAGE_KEY)
    : localStorage.getItem(TENANT_TOKEN_STORAGE_KEY);

  if (!token) {
    return next(req);
  }

  const authReq = req.clone({
    setHeaders: {
      Authorization: `Bearer ${token}`
    }
  });

  return next(authReq);
};
