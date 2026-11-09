import type { Routes } from '@angular/router';

export const appRoutes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./pages/platform-home.component')
      .then(module => module.PlatformHomeComponent)
  },
  // C02-03: allowlisted, lazy, read-only platform-information routes.
  // No tenant domains, login, private shells or registration endpoints are created.
  {
    path: 'experiencias',
    pathMatch: 'full',
    data: { publicPage: 'experiencias' },
    loadComponent: () => import('./pages/public-information.component')
      .then(module => module.PublicInformationComponent)
  },
  {
    path: 'demostraciones',
    pathMatch: 'full',
    data: { publicPage: 'demostraciones' },
    loadComponent: () => import('./pages/public-information.component')
      .then(module => module.PublicInformationComponent)
  },
  {
    path: 'alta-empresas',
    pathMatch: 'full',
    data: { publicPage: 'alta-empresas' },
    loadComponent: () => import('./pages/public-information.component')
      .then(module => module.PublicInformationComponent)
  },
  {
    path: '**',
    loadComponent: () => import('./pages/not-found.component')
      .then(module => module.NotFoundComponent)
  }
];
