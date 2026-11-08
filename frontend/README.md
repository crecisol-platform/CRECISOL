# CRECISOL — Frontend del capítulo 01

**Estado:** Angular 22.2.2 compiló en GitHub Actions con Node 24.22.2, TS 6.0.3 y `npm ci`. No se ha probado aún la UI en navegador ni certificado el capítulo.

Vista inicial pública de solo lectura; sin pagos, tenant selector ni administración.

## Inicio local

1. Iniciar la API en http://127.0.0.1:5180 según `backend/README.md`.
2. Disponer de Node 24.15+ y Python 3. Desde `frontend/`, ejecutar `npm ci --no-audit --no-fund`.
3. Ejecutar `npm start` y abrir http://127.0.0.1:4200.
4. Ejecutar `npm run build` y `npm run test:contract`.

El logo se extrae sin alteraciones del Word maestro tras verificar hashes SHA-256. El contenedor local conserva Node 22.16, pero la compilación real se efectuó en CI #42516130198.

**Pendiente:** accesibilidad, móvil, SEO/SSR y revisión visual reales; ninguna UI determina permisos ni acceso a tenants.
