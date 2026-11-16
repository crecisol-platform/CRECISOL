# C05 — Segunda auditoría integral: funcionalidad, arquitectura, datos y calidad visual

**Fecha:** 16-11-2026. **Alcance:** únicamente CRECISOL C05, rama feature del PR #156 derivada de `dev` en `74cdf1268c2c6f3710f4426260965035dc90f85c`. Sin cambios de esquema, producción, `main` ni SOLQARYN.

## Autoridad y condiciones de cierre

Fuentes consultadas en `dev`: `AGENTS.md`, `.agents/skills/crecisol-project-governance/SKILL.md`, `docs/MASTER_REFERENCE.md`, `docs/PLAN_37_CAPITULOS.md`, `docs/ESTADO_VIGENTE_CAPITULOS.md`, acta integral C05 y acta del PR #155. Documento Maestro Único inalterado. No revocar las 11 microfases originales ni interpretar pruebas de entorno descartable como producción. Mantener C06 bloqueado según el estado institucional vigente.

### Diagnóstico multidisciplinar

| Dimensión | Hallazgo de lectura del código | Tratamiento y gate |
| --- | --- | --- |
| Interfaz pública | `/cuenta` ya usa vistas diferenciadas de acceso, registro, confirmación y recuperación; la marca publicada es validada por `PublicBrandService`. Falta inspección visual manual real de la versión actual frente a C01/C02. | Conservar navegación separada, contraste, estados y pruebas responsive. No proclamar perfección visual por aprobar axe. |
| Cuenta privada | `identity-workspace.component.html` concentra perfiles, llaves, MFA, aprobaciones y diagnóstico; usa `@switch` para montar solo la vista elegida, pero el formulario de UUID privado estaba visible en «Resumen». | Pasar diagnóstico a `<details>` voluntario y adaptar el E2E real de autorización. No mostrar identificadores técnicos por defecto. |
| Operadores | `platform-identity-panel.component.html` conservaba 18 formularios aunque separados del cliente. Todos los grupos eran visibles en una columna de grandes acordeones. | Mostrar solo **Casos, Sesiones, Perfil, Seguridad o Monitor**, mediante Signals y acciones ya existentes. Mantener íntegros los 18 formularios, los 17 dispatches de `action()` y la recuperación institucional de dos operadores. |
| Estética C01/C02 | C01/C02 usa superficies claras, composición editorial, jerarquía tipográfica, espaciado y marca versionada. C05 heredó estilos operativos de formularios muy técnicos. | Navegación contextual, objetivos táctiles, contraste y progresión de tareas. Sin dependencia CSS/fuentes externas ni usar el constructor C09 anticipadamente. |
| Angular | 22 standalone, lazy `/cuenta`, Signals y servicios diferenciados de cliente/operador. `identity-api.service.ts` aún tiene ~378 líneas y `platform-identity-panel.component.html` ~16 KB. | No duplicar lógica de credenciales ni dividir servicios ciegamente solo por tamaño. Cualquier extracción futura debe medir acoplamiento y ejecutar E2E. |
| Backend | ASP.NET Core 10, `OperationalIdentityModule.cs` ~15 KB, flujo MySQL distribuido en clases `partial`. `Authorized.cs` consulta `tenant_id=@t`, sesiones revocables, factor bajo transacción y recurso por tenant/branch/owner. | Sin cambios backend en este incremento. Exigir regresiones reales y denegaciones BOLA/IDOR/CSRF/MFA antes de certificar. |
| MySQL | Migraciones operativas 001–016 y down; FKs, restricciones de estado y claves compuestas en tablas de tenant/branch. La reversión fue probada anteriormente en CI. | No tocar migraciones ya aplicadas ni datos de prueba del propietario. Nueva ejecución real MySQL 8.4 y rollback en el SHA final, no inferir resultado futuro. |
| Seguridad | Host de ensayo `.example.test`, cookies HttpOnly, MFA, recuperación y controles de operador segregados. UI no concede privilegios por mostrar botones. | Preservar los controles backend y toda prueba negativa; funcionalidad de producción/COM-T08-10 fuera del alcance. |
| QA | PR #155 obtuvo 294 comprobaciones operativas PASS y 4/4 CI POSTMERGE SUCCESS en `74cdf126...`. | Repetir pruebas sobre HEAD nuevo del PR #156; evidencia histórica no equivale a prueba de este cambio. Nuevas pruebas públicas: operador a 390/1366 px, WCAG/axe, sin formulario de cliente ni scroll horizontal. |

### Auditorías A/B/C — estado provisional del PR #156

- **A / funcional — EN VALIDACIÓN:** comparar los 18 formularios anteriores y actuales, comprobar que no se eliminó una sola acción, ejecutar flujo real Angular → API → MySQL y recorrido de recuperación asistida.
- **B / seguridad — EN VALIDACIÓN:** mantener actor/tenant/sucursal/propietario, scopes de operador, revocación MFA, contraseñas, cookies HttpOnly/CSRF, auditoría y negativas concurrentes. Cero privilegios por navegación; SQL y migraciones sin cambios.
- **C / arquitectura, rendimiento y UX — EN VALIDACIÓN:** compilar Angular 22 y .NET 10, ejecutar regresiones C01–C05, Playwright/Chromium, contraste, navegabilidad por teclado, móvil y estabilidad de formularios largos. Una revisión visual humana del navegador no se considera ejecutada sin captura actual.

**Precaución:** tampoco se debe presentar el hostname de ensayo, el buzón de prueba, el KMS o SMTP descartables como certificación de producción.

## No-go y rollback

PR #156 queda en borrador si alguna prueba aplicable falla, hay accesos inseguros, regresiones de permisos o se omite un flujo obligatorio. Revertir exclusivamente el PR sin tocar las migraciones ni el Documento Maestro. `main`, producción y C06 permanecen inalterados.

**Este documento registra la inspección del código y el plan de auditoría. No es por sí solo certificación de la nueva revisión visual.**
