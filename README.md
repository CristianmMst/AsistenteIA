# AsistenteIA

Asistente académico móvil con inteligencia artificial. Proyecto final de **Programación Avanzada y Aplicada** (VI semestre, Tecnología en Desarrollo de Software, Universidad de San Buenaventura). Docente: Mg. Sandra Esperanza Sánchez Barragán.

**Integrantes:** Cristian Mora Moreno · Sebastián Sosa Martínez · Samuel Barato

**Repositorio:** https://github.com/CristianmMst/AsistenteIA

Aplicación .NET MAUI (Android) que envía consultas académicas a una API REST de IA (Pollinations, compatible con OpenAI Chat Completions), muestra la respuesta con estados de carga y error, guarda el historial en SQLite y permite marcar respuestas como verificadas.

## Funcionalidades

| Pantalla | Qué hace |
|---|---|
| Inicio | Propósito, contadores, estado de clave e internet, accesos rápidos y recomendaciones de uso responsable |
| Consultar IA | Prompt con validación (5 a 2000 caracteres), plantillas, categoría, Consultar/Cancelar, Guardar |
| Historial | Búsqueda, filtro por categoría y verificadas, eliminar con confirmación |
| Detalle | Prompt, respuesta, fecha, modelo, categoría, verificada y notas (UPDATE) |
| Configuración y Acerca de | Clave con SecureStorage, modelo, endpoint, tiempo máximo, Probar conexión, registro HTTP, Acerca de |
| Plantillas | CRUD de plantillas de prompts |

## Tecnologías

.NET 10 · .NET MAUI · C# · XAML · MVVM (CommunityToolkit.Mvvm 8.4.2) · sqlite-net-pcl 1.11.285 · HttpClient + System.Text.Json · SecureStorage

## Ejecutar

Requisitos: SDK .NET 10 con el workload `maui-android`, Android SDK (API 35) y JDK 17 o 21.

```bash
dotnet build -f net10.0-android -t:Run
# Si no detecta el SDK o el JDK:
#   -p:AndroidSdkDirectory="C:/Users/<usuario>/AppData/Local/Android/Sdk"
#   -p:JavaSdkDirectory="C:/Program Files/Eclipse Adoptium/jdk-21..."
```

Generar el APK:

```bash
dotnet publish -f net10.0-android -c Release -p:AndroidPackageFormat=apk
```

## Clave de la API

**El repositorio no contiene ninguna clave.** Cree una en <https://enter.pollinations.ai> (con presupuesto limitado), ábrala en la app en **Configuración**, pulse **Guardar clave** y luego **Probar conexión**. La clave se guarda cifrada con SecureStorage y solo se muestra enmascarada.

## Estructura

```
Models/      JSON de la API y entidades SQLite
Services/    AiService (REST), DatabaseService (SQLite), ApiKeyService (SecureStorage),
             SettingsService, ConnectivityService, ApiLogService, DialogService
ViewModels/  Un ViewModel por pantalla
Views/       Páginas XAML
docs/        Documento Word, diagramas (.mmd/.png), capturas y evidencias (REST y SQLite)
```

## Documentación

- `docs/AsistenteIA_Documentacion.docx`: requisitos, historias, metodología, arquitectura, diagrama de clases, decisiones técnicas, consumo REST, SQLite CRUD, pantallas, pruebas P01 a P10, matriz de riesgos, declaración de uso de IA, manual de usuario, manual técnico y guion de sustentación.
- `docs/diagramas/`: arquitectura, clases, secuencia y navegación (fuente Mermaid y PNG).
- `docs/capturas/`: evidencias tomadas en un Samsung Galaxy A21s (Android 15).
- `docs/evidencias/`: solicitudes y respuestas HTTP reales (200, 400, 401) y la consulta a la base SQLite del celular.

## Uso de IA

En el desarrollo se usó Claude Code (Anthropic) como asistente de programación. El equipo revisa el código y debe poder explicarlo. Ver la sección 12 del documento.
