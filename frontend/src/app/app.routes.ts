import type { Routes } from '@angular/router';

export const appRoutes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./pages/platform-home.component')
      .then(module => module.PlatformHomeComponent)
  },
  {
    path: '**',
    loadComponent: () => import('./pages/not-found.component')
      .then(module => module.NotFoundComponent)
  }
];
