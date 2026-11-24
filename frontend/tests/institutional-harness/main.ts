import { bootstrapApplication } from '@angular/platform-browser';
import { InstitutionalHarnessComponent } from './institutional-harness.component';

// Isolated visual test entry. It is NEVER linked from production app.routes.ts.
void bootstrapApplication(InstitutionalHarnessComponent).catch(() => {
  document.body.textContent = 'Laboratorio institucional de pruebas no disponible.';
});
