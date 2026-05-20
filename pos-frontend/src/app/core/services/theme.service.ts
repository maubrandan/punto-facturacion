import { DOCUMENT } from '@angular/common';
import { effect, inject, Injectable } from '@angular/core';
import { AuthService } from './auth.service';
import { User } from '../models/user.model';

type BusinessType = User['businessType'];

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly authService = inject(AuthService);
  private readonly document = inject(DOCUMENT);

  private readonly themeClassByBusinessType: Record<BusinessType, string> = {
    farmacia: 'theme-pharmacy',
    ferreteria: 'theme-hardware',
    kiosco: 'theme-kiosk'
  };
  private readonly allThemeClasses = [
    'theme-pharmacy',
    'theme-hardware',
    'theme-kiosk',
    'theme-farmacia',
    'theme-ferreteria',
    'theme-kiosco'
  ];

  constructor() {
    effect(() => {
      const businessType = this.authService.currentUser()?.businessType ?? 'kiosco';
      this.applyTheme(businessType);
    });
  }

  private applyTheme(businessType: BusinessType): void {
    const body = this.document.body;
    body.classList.remove(...this.allThemeClasses);
    body.classList.add(this.themeClassByBusinessType[businessType]);
  }
}
