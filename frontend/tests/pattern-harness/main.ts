import { bootstrapApplication } from '@angular/platform-browser';
import { PatternHarnessComponent } from './pattern-harness.component';

// This entry point is used ONLY by build:pattern-harness in isolated CI.
void bootstrapApplication(PatternHarnessComponent).catch(() => {
  document.body.textContent = 'El patrón local de prueba no está disponible.';
});
