# CRECISOL — Frontend inicial del capítulo 01

**Estado:** fuente Angular 22 creada, todavía sin compilación verificada.
Identidad pública de solo lectura; no hay pagos, tenant selector ni administración.

## Herramientas requeridas
Node 24.15+ compatible con Angular 22, npm, Python 3 y API local .NET 10.
El logo se extrae sin alteraciones del Documento Maestro Único Word,
cuya huella SHA-256 debe coincidir con la versión aprobada.

## Arranque exclusivamente local
1. Ejecutar la API en http://127.0.0.1:5180 según backend/README.md.
2. Instalar Node 24 compatible; ejecutar npm install desde frontend/.
3. Versionar package-lock.json generado con el toolchain correcto antes de fusionar.
4. Ejecutar npm start y abrir http://127.0.0.1:4200.
5. Ejecutar npm run build, npm run test:contract y registrar logs y capturas reales.

El entorno de esta ejecución tiene Node 22.16 y TypeScript 5.8, incompatibles
con Angular 22. No se declaran compilación, rendimiento ni accesibilidad probados.
