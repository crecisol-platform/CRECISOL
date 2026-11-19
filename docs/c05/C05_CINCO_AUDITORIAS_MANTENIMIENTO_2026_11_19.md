# CRECISOL — Cinco auditorías de mantenimiento de C05 (19-11-2026)

**Proyecto y repositorio:** `crecisol-platform/CRECISOL` · ID `1450414592` · PR #172 en rama `feature/c05-totp-sha1-integrated-qa-20261118` con destino `dev`.  
**Objeto:** cambios C05 sobre TOTP-SHA1 exclusivo, identidad empresarial y plataforma, QR/semillas, privacidad Angular, recuperación, respaldos locales y política global de inactividad C05-018.  
**Estado de esta acta:** cinco auditorías de fuente y pruebas negativas incorporadas; **dictamen final condicionado a ejecutar y revisar CI para el SHA definitivo**. Los 9/9 SUCCESS del commit anterior `b09a78c0` no certifican ningún commit posterior. Los procesos GitHub pueden omitir jobs por filtros; SKIPPED no equivale a PASS.  
**Gobierno:** Documento Maestro Único según `docs/MASTER_REFERENCE.md`; 5/37 capítulos certificados exclusivamente en alcances históricos, C06 bloqueado. Ninguna modificación en SOLQARYN, producción o los binarios maestros.

## Auditoría 1 — Funcionalidad, vencimiento y consistencia de sesiones

**Criterio:** 30 minutos de inactividad por defecto; vigencia deslizante cuando existe una operación autenticada válida; intervalo administrable de 5 a 120 minutos; tokens ya vencidos no se recuperan ni mediante incremento posterior del plazo; rotación no restablece la hora del MFA.

**Inspección:** `MySqlOperationalIdentityFlow.SessionPolicy.cs`, `Authorized.cs`, `Platform.cs`, `OperationalBrowserSessionTransport.cs`, `browser-session-window.ts`, dos servicios Angular y `018_c05_session_idle_policy.sql`. La duración se almacena en una fila versionada de MySQL; el backend vuelve a validar la sesión y se encarga de actualizar el cookie HttpOnly.

**Hallazgo corregido:** la actualización deslizante inicialmente dependía de una comprobación previa al `UPDATE`. Se añadieron predicados de expiración absoluta e inactividad calculados con el reloj MySQL **también en la instrucción de actualización** de ambos ámbitos. Eso impide que una sesión rebasada durante el intervalo transaccional se renueve por una respuesta tardía.

**Evidencia de QA:** pruebas de 31 minutos simulados en base descartable, denegación de replay, MFA original preservado al rotar, tests de CAS, cambio 30→5→30 y Chromium/actividad real en `qa/c05_operational_flow_e2e.mjs` y `frontend/tests/c05-totp-async-privacy.spec.ts`; nuevo guard de fuente con negativa de borrado.

## Auditoría 2 — Autorización, separación plataforma/tenant y seguridad MFA

**Criterio:** ningún usuario empresarial ni operador de solo lectura modifica la política; únicamente un operador con `can_manage_session_policy=1`, MFA verificable y revisión vigente puede hacerlo. Limitar cuotas y registrar cada operación. TOTP SHA1 de seis dígitos y período de 30 segundos; no reutilizar factores/recovery ni aceptar MFA SHA256 heredado.

**Inspección:** `OperationalIdentityModule.cs`, operaciones específicas de plataforma y cuenta, grants SQL limitados a columnas, hashes HMAC, migración 017, rekey offline de tres operadores sintéticos, contratos criptográficos y negativos A/B de C05. La migración 018 crea el permiso administrativo como `FALSE`, nunca lo concede por URL, tenant o navegador.

**Resultado de fuente:** rutas, permisos, ámbito empresarial y privilegios de plataforma separados; MFA adicional obligatorio para editar; resistencia frente a rotación y cambio de política concurrente, con revocación segura de sesiones antiguas. La prueba con teléfonos reales sigue siendo una aceptación **fuera de esta auditoría de código**.

## Auditoría 3 — Privacidad y custodia de secretos

**Criterio:** las contraseñas, semillas MFA, códigos de respaldo, tokens y rutas privadas no se registran ni exponen por argumentos de procesos, frontends o cambios de rama. Validar archivos restringidos y la limpieza de datos efímeros.

**Hallazgos corregidos:**  
1. Supervisor, arnés E2E y workflow pasaban `MYSQL_PWD` o la contraseña raíz como **valor de un argumento Docker**. Sustituidos por `-e MYSQL_PWD` y variables del entorno de un proceso aislado.  
2. Se reforzó el rechazo de enlaces simbólicos y de ficheros privados accesibles para grupo/otros en POSIX, tanto para escribir como antes de verificar respaldos.  
3. El código MFA del nuevo formulario administrativo ahora se muestra en un campo enmascarado; se conservan pruebas Chromium de limpieza de QR, recuperación, códigos y respuestas tardías.

**Control negativo nuevo:** `c05_sha1_persistent_guards.test.mjs` verifica que los comandos no interpolen credenciales en argumentos. Workflow de respaldo comprueba que un enlace simbólico de `runtime.env` provoque fallo cerrado. En Windows las ACL NTFS requieren verificación local propia por el propietario/Codex.

## Auditoría 4 — Persistencia, migraciones y restauración

**Criterio:** no repetir C05-017 ni modificar el volumen existente del propietario; migración 018 aditiva con `idle_minutes=30` e historial completo; respaldos realmente restaurables y autenticados; guardias contra corte sin consentimiento, datos o llaves incompletos.

**Hallazgo corregido:** el manifiesto anterior V1 almacenaba solo huellas SHA-256 sin autenticación, por lo que alguien con acceso de escritura podía coordinar alteración de archivo y manifiesto. Los respaldos nuevos V2 se firman con **HMAC-SHA256** derivado de una llave privada local, distinta del directorio de backups, y se verifican en tiempo constante. V1 queda solo para `verify-backup` de lectura con advertencia; una actualización offline 017→018 exige respaldo V2 nuevo autenticado.

**Control negativo nuevo:** modificación de manifiesto firmado, alteración de llaves y archivo comprimido, interrupción de rekey, symlink privado y repetición de migración. La restauración SQL/ledger/recuentos se realiza en MySQL 8.4 descartable sin red ni volumen compartido. El HMAC **no sustituye** a una copia cifrada e independiente de todas las llaves del propietario ni prueba por sí solo la recuperación funcional completa en otro equipo.

## Auditoría 5 — UX, accesibilidad, alcance y calidad de QA

**Criterio:** conservar 18 formularios de operador anteriores y 15 de cuenta, añadir un solo formulario de política de sesiones, coherencia visual institucional C01/C02 y C05, navegación móvil/escritorio, semántica accesible, mensajes claros, foco seguro, ausencia de secretos persistidos y mantenimiento de pruebas negativas.

**Inspección:** componentes `platform-identity-panel` y `platform-operator-security`, `identity-workspace`, señales Angular, `BrowserSessionWindow`, scripts institucionales A/B/C/D y tests Chromium con axe. Verificar PR #172 solo en `frontend/`, `backend/`, `database/`, `qa/`, `docs/c05/` y workflows. La calidad estética humana no se infiere de una CI verde.

**Resultado de fuente:** nuevo modal de Seguridad integrado y protegido por permiso backend + MFA; sin alterar contratos públicos C01/C02 ni documento maestro. Se preservan y amplían pruebas de accesibilidad, roles del modal y concurrencia.

## Matriz de cierre y responsabilidades

| Control | Responsable | Criterio de cierre |
|---|---|---|
| Fuente, compilaciones y auditorías A-E del último SHA | Equipo de código | Workflows obligatorios SUCCESS, pruebas negativas y registro de evidencia, 0 P0/P1 conocidos |
| Comparación de `dev`/`main`, archivos de PR y Documento Maestro | Equipo de código | Sin modificaciones fuera de C05; ningún trabajo en SOLQARYN |
| Copia SQL y **copia externa cifrada** de `runtime.env`, credenciales y llaves, restauración independiente | Propietario asistido por Codex | Evidencia local redactada; ninguna ejecución en el volumen propietario sin aceptación separada |
| Google/Microsoft Authenticator en teléfonos reales, biometría si procede, prueba visual/accesible humana | Propietario asistido por Codex | Capturas sin QR ni secretos, PASS/FAIL por escenario |
| Protección de ramas GitHub | Gobierno del repositorio | La consulta informa `protected: false` en `dev` y `main`; no modificar configuración fuera del alcance de código/QA de C05 |

**No equivale a certificación de producción.** La validación aislada no habilita despliegues, cobros, servicios reales, `main` ni C06. Se conserva PR #172 como borrador hasta aceptación y cierre de las condiciones manuales.
