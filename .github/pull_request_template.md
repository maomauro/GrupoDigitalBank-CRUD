## Qué cambia y por qué

<!-- Una o dos frases. Enlaza el issue. -->

Closes #

## Cómo se probó

- [ ] Modo A (stack completo en Docker): flujo afectado probado a mano
- [ ] Pruebas automáticas nuevas o ajustadas

## Checklist (Definition of Done)

- [ ] Compila sin advertencias (`dotnet build -warnaserror`) y `dotnet format --verify-no-changes` limpio
- [ ] `dotnet test` en verde
- [ ] Sin secretos ni archivos generados en el diff
- [ ] Si toca la BD: script SQL numerado nuevo (no se edita uno ya aplicado)
- [ ] Documentación y `CLAUDE.md` actualizados si cambió algo estructural
- [ ] Commits en formato Conventional Commits; el título del PR también (se usa en el squash)
