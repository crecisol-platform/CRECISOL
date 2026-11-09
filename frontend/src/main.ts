import { provideHttpClient, withFetch } from '@angular/common/http';
import { bootstrapApplication } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { AppComponent } from './app/app.component';
import { appRoutes } from './app/app.routes';

void bootstrapApplication(AppComponent, {
  providers: [provideHttpClient(withFetch()), provideRouter(appRoutes)]
}).catch(() => {
  // Deliberately omit exception details and any configuration or tenant identifiers.
  document.body.replaceChildren();
  const message = document.createElement('p');
  message.setAttribute('role', 'alert');
  message.textContent = 'La identidad pública no está disponible.';
  document.body.appendChild(message);
});
