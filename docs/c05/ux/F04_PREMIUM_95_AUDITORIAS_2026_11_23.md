# CRECISOL · C05 UX F04 — Elevación premium de consola de operadores

**Fecha:** 23-11-2026 (Honduras).  
**Rama:** feature/c05-ux-f04-premium-9p5-20261123, basada en `31ff32d348606e77d311b6250a0e341eb052485a` (F01/F02/F03 integradas en candidata F06).  
**Estado inicial:** cambios aplicados; **certificación final pendiente** de CI sobre el SHA nuevo y de revisión individual de las 25 capturas.

## 1. Diagnóstico real y correcciones

El PR histórico #177 sigue DRAFT en una rama de base F03 antigua. Su código productivo fue incorporado a la línea candidata mediante la cadena F05/F06. No fusionarlo nuevamente. Históricamente las notas editoriales IA eran Casos 9.1, Sesiones 9.2, Perfil 9.1, Seguridad 9.0, Monitor 9.2. Se descargaron 25 imágenes de F04 del workflow #44680701140 y se revisaron como punto de partida.

| Área | Corrección aplicada en la rama actual | Controles inalterables |
| --- | --- | --- |
| Casos | Consulta exacta visualmente primaria, resultado separado, revisión y emisión protegidas diferenciadas, nota de seguridad visible | Sin bandeja global inventada; decisión vacía, doble declaración, evidencia SHA-256, motivo y MFA |
| Sesiones | Dispositivo con identidad visual y referencia corta; revocación de peligro distinta de renovación ordinaria | Operador actual únicamente, sesión seleccionada por ID en backend, revocación total mediante MFA |
| Perfil | Identidad institucional y detalles con mejor ritmo; edición y fotografía como tareas distintas | Perfil no cambia roles ni pertenencias; fotografía validada y sanitizada |
| Seguridad | Credenciales agrupadas, política central de inactividad separada de operaciones privilegiadas | 30 minutos iniciales, valor administrable 5–120, revisión de versión, MFA y permisos backend |
| Monitor | KPI principal destacado y señales agregadas por gravedad; lista con prioridades visuales | Solo sumas y allowlist semántico; no IP, correo, UUID, empresa o datos de tenant |

Se conserva la cabecera, navegación y avisos institucionales. Las etiquetas y acciones existentes no se renombran para no romper contratos E2E. El CSS es específico de las cinco áreas y no oculta formularios ni reduce tamaño de letra como técnica de ajuste.

## 2. Alcance, dependencias y riesgos

Exactamente **12 rutas**: cinco plantillas HTML de operadores, CSS F04 y CSS premium segregado, modelo TS con cinco styleUrls, especificación Playwright F04, auditoría premium A/B/C, este documento y nuevo workflow CI. Ni frontend genérico, ni backend, base de datos, API, contratos de autorización, roles, cookies, MFA, Documento Maestro, rama `dev`, `main`, SOLQARYN, VPS ni producción se cambian.

Base SHA fija: `31ff32d348606e77d311b6250a0e341eb052485a`. Cualquier divergencia del diff es NO-GO. **0 P0/P1** demostrados en el alcance de las pruebas es requisito de aprobación; no se presupone ausencia de riesgos no probados.

## 3. Auditorías obligatorias

**A / Funcionalidad:** mismo orden y estructura de etiquetas HTML, cinco experiencias, rutas y acciones exactas, captura 5×5 y regresiones de navegación.  

**B / Privacidad y seguridad:** comparativa de formularios, inputs, required, modales y handlers contra base; confirmación de identidad, MFA, revocación de sesiones, política con revisión y categorías agregadas del monitor. Negativas reales entre tenants y realm de plataforma en Angular/.NET/MySQL.  

**C / Arquitectura UX:** tokens institucionales, responsive de 320 a 1440 px, controles táctiles >=44 px, teclado, lector axe WCAG 2.2 AA, zoom 200 %, colores forzados y reduced-motion; datos sintéticos y hashes SHA-256 por captura.

CI requerida: TypeScript, Angular 22 Node24, Chromium, F04 25 capturas+25 pruebas premium, regresiones F02–F06, inventario 65 ID y 340 E2E Angular → .NET10 → MySQL8.4 LTS con 18 migraciones y reversiones. **No atribuir PASS antes de los logs de SHA exacto.**

## 4. Evaluación editorial pendiente de nuevas capturas

Notas finales exigidas: cinco calificaciones IA justificadas individualmente y >=9.5/10, sin promedio sustitutivo. Se revisarán los PNG originales a 320,390,768,1024,1440 con especial atención a casos protegidos, MFA, sesiones, monitor y privacidad. Las notas históricas 9.0–9.2 no representan el código nuevo.

**F04 se puede fusionar únicamente a su rama base candidata F06 después de CI verde y acta; no habilita C06 ni entrega productiva.** La CI F06 preexistente que compara un allowlist contra una base antigua requiere reconciliación separada, registrada en #179. No relajar esa barrera en este trabajo de F04.

### Reparación del presupuesto CSS

La primera CI F04 #45183537220 aprobó A/B/C pero falló al compilar Angular: el CSS combinado del componente superó el máximo de 22 KB (23.59 KB compilados). Se conserva el límite; se separa exclusivamente la nueva capa premium en `platform-operator-f04-premium.component.css` cargada por las cinco vistas de plataforma desde `platform-operator-views.component.ts`. CSS original restaurado con solo una nota de localización. Ninguna regla global o seguridad fue modificada. Nueva CI obligatoria antes de calificar.
