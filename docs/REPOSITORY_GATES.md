# GitHub — protecciones deseadas y realidad comprobada

Repositorio: solqaryn-platform/CRECISOL (privado, propietario cuenta personal).

**Verificado el 2026-11-08:** main.protected=false y dev.protected=false.
**Al consultar la API de rulesets de este repositorio, GitHub devolvió HTTP 403**: "Upgrade to GitHub Pro or make this repository public to enable this feature."
Por tanto, NI rulesets NI branch protection clásica están acreditados como aplicables con el plan actual en este repositorio privado. GitHub indica que estas funciones en privados requieren Pro/Team/Enterprise; **NO convertir el repo a público** ni comprar un plan sin decisión explícita.

## Política operacional mientras no haya enforcement de plataforma
- Solo GitHub privado, autenticación reforzada y acceso mínimo.
- Todo cambio en feature/cNN-* -> dev; todo paso a main vía PR con revisión y CI, aunque no pueda exigirse automáticamente.
- Nada de force-push, borrar ramas principales, subir secretos ni bypass por automatización.
- GitHub Actions foundation es validación inicial estática, no CI suficiente para certificar SaaS.
- No fusionar capítulos sin evidencia de QA, tres auditorías, ausencia P0/P1 y aprobación.
- El PR #1 de fundación fue fusionado manualmente a main, sin protección de ramas. Revisar diff de próximos PR.

## Reglas deseadas al disponer de un plan que las admita
**main:** Required pull request; al menos una aprobación independiente cuando exista revisor; require status checks *solo después de que hayan corrido*; block deletion y block force push; no bypass rutinario.
**dev:** block deletion y block force push; PR antes de integrar cambios de capítulo; CI mínimo y pruebas por módulo cuando existan.

No activar reglas para un check que nunca haya corrido ni declarar protegido hasta obtener readback positivo. No desplegar a VPS ni publicar ambientes desde GitHub todavía.

Documentación: https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/creating-rulesets-for-a-repository
