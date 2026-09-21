# Especificación de Proyecto: Cuaderno Digital con IA (Windows, táctil)

## 1. Resumen del producto

App de escritorio para Windows dirigida a estudiantes universitarios. Debe funcionar igual de bien en **portátiles táctiles con lápiz/stylus** (para dibujar y escribir a mano) que en **PCs normales con teclado y mouse** (para escribir texto, como en un procesador de texto). Permite crear cuadernos digitales, tomar apuntes a mano o escritos, insertar texto editable, tablas y gráficas, dibujar, subrayar, pegar imágenes, exportar a PDF, y cuenta con un asistente de IA (Gemini API, plan gratuito) que puede leer las páginas (incluida escritura a mano) y responder preguntas, explicar temas y corregir al estudiante.

**Principio de diseño clave:** cada página combina dos capas independientes: una capa de **tinta** (`InkCanvas`, para dibujar/escribir a mano con lápiz o mouse) y una capa de **objetos** (cajas de texto editable, tablas, gráficas, imágenes) que se manipulan con clic/arrastre igual que en Word o PowerPoint. Así la app sirve tanto para quien solo tiene mouse como para quien tiene lápiz táctil.

## 2. Stack tecnológico

- **Lenguaje/Framework:** C# con .NET 8 y WPF (Windows Presentation Foundation)
- **Motor de tinta digital:** `System.Windows.Controls.InkCanvas` (soporte nativo de Windows Ink: presión, inclinación, borrador físico, distinción dedo/lápiz)
- **Exportación PDF:** librería `QuestPDF` (gratuita, licencia Community)
- **Serialización de datos:** JSON (`System.Text.Json`) + formato ISF nativo de WPF para guardar trazos de tinta
- **HTTP/IA:** `HttpClient` para llamadas REST a la API de Gemini (`generativelanguage.googleapis.com`)
- **Persistencia de cuadernos:** cada cuaderno es una carpeta comprimida (`.cdgz`, en realidad un ZIP renombrado) gestionada con `System.IO.Compression`
- **Gestor de paquetes:** NuGet

## 3. Estructura de carpetas del proyecto

```
CuadernoDigital/
├── CuadernoDigital.sln
├── src/
│   ├── CuadernoDigital.App/              # Proyecto WPF principal
│   │   ├── App.xaml
│   │   ├── Views/
│   │   │   ├── MainWindow.xaml           # Ventana principal / biblioteca de cuadernos
│   │   │   ├── NotebookEditorView.xaml   # Vista de edición de un cuaderno (páginas)
│   │   │   ├── PageCanvasView.xaml       # El lienzo InkCanvas de cada página
│   │   │   ├── CoverDesignerView.xaml    # Diseñador de portada
│   │   │   └── AiAssistantPanel.xaml     # Panel lateral del asistente IA
│   │   ├── ViewModels/                   # MVVM: un VM por cada View
│   │   ├── Controls/
│   │   │   ├── PageBackgroundRenderer.cs # Dibuja fondo: liso/cuadriculado/rayado
│   │   │   └── ImageStickerControl.cs    # Imagen pegada/recortable en la página
│   │   ├── Services/
│   │   │   ├── NotebookRepository.cs     # CRUD de cuadernos en disco
│   │   │   ├── PageSerializer.cs         # Guarda/carga trazos ISF + imágenes
│   │   │   ├── PdfExportService.cs       # Exporta cuaderno/página a PDF
│   │   │   ├── GeminiClient.cs           # Cliente HTTP hacia la API de Gemini
│   │   │   └── ImageClipboardService.cs  # Pegar/recortar imágenes del portapapeles
│   │   ├── Models/
│   │   │   ├── Notebook.cs
│   │   │   ├── Page.cs
│   │   │   ├── PageBackgroundType.cs     # enum: Plain, Grid, Lined, Dotted
│   │   │   └── StickerImage.cs
│   │   └── Assets/                       # Iconos, plantillas de portada
│   └── CuadernoDigital.Tests/            # Pruebas unitarias (xUnit)
├── docs/
│   └── formato-cuaderno.md               # Documentación del formato .cdgz
└── README.md
```

## 4. Modelo de datos

### Notebook (metadata.json dentro del .cdgz)
```json
{
  "id": "uuid",
  "titulo": "Cálculo II",
  "portada": {
    "colorFondo": "#2B4C7E",
    "imagenFondo": "cover.png",
    "titulo": "Cálculo II",
    "subtitulo": "2do semestre"
  },
  "fechaCreacion": "2026-09-21T00:00:00Z",
  "paginas": ["pagina_001.json", "pagina_002.json"]
}
```

### Page
```json
{
  "id": "uuid",
  "tipoHoja": "Cuadriculada",   // Plana | Cuadriculada | Rayada | Punteada
  "trazosInk": "base64-ISF",     // strokes serializados de InkCanvas
  "imagenes": [
    { "id": "uuid", "archivo": "img_01.png", "x": 100, "y": 200, "ancho": 300, "alto": 200, "rotacion": 0 }
  ]
}
```

## 5. Funcionalidades por fase (roadmap para el agente de código)

### Fase 1 — Núcleo de cuadernos (sin dibujo aún)
- Crear, renombrar, eliminar, abrir cuadernos
- Biblioteca de cuadernos en la pantalla principal (grid con portadas)
- Añadir/eliminar páginas dentro de un cuaderno
- Guardado y carga desde disco (formato .cdgz)

### Fase 2 — Lienzo de dibujo y escritura
- Integrar `InkCanvas` por página
- Selector de fondo de hoja: Plana / Cuadriculada / Rayada / Punteada (renderizado como capa detrás del InkCanvas)
- Herramientas: lápiz (grosor/color), resaltador (subrayar, con transparencia y punta ancha), borrador, selector de trazos
- Zoom y desplazamiento (pan) de la página
- Deshacer/rehacer

### Fase 3 — Imágenes
- Pegar imagen desde portapapeles (Ctrl+V)
- Insertar imagen desde archivo
- Recorte simple (crop) antes de insertar
- Mover, escalar y rotar imágenes ya insertadas en la página

### Fase 4 — Edición de texto, tablas y gráficas (modo "tipo Word")
- Insertar cajas de texto editable (fuente, tamaño, negrita/cursiva/subrayado, color, alineación), movibles y redimensionables con clic y arrastre
- Insertar tablas: número de filas/columnas configurable, celdas editables con clic, añadir/eliminar filas y columnas
- Insertar gráficas simples (barras, líneas, circular) a partir de datos que el usuario ingresa en una tabla auxiliar (usar una librería como `LiveCharts2`, gratuita)
- Todos estos elementos deben poder seleccionarse, moverse, escalarse y eliminarse tanto con mouse como con lápiz/dedo (usar los eventos de manipulación táctil de WPF, `ManipulationDelta`, además de los de mouse)
- El teclado en pantalla de Windows debe activarse automáticamente al tocar una caja de texto en modo táctil (comportamiento estándar si se usa `TextBox`/`RichTextBox` nativos de WPF)

### Fase 5 — Portadas
- Editor de portada: color, imagen de fondo, título, subtítulo, plantillas prediseñadas

### Fase 6 — Exportación
- Exportar una página a PDF
- Exportar cuaderno completo a PDF (todas las páginas en orden, con portada como primera página, incluyendo texto, tablas, gráficas e imágenes)

### Fase 7 — Asistente de IA (Gemini)
- Panel lateral de chat con el asistente
- Modo "pregunta general": el usuario escribe una pregunta y el asistente responde (útil para estudiar)
- Modo "revisar mi página": se captura la página actual como imagen (`RenderTargetBitmap`) y se envía a Gemini junto con un prompt tipo *"Lee esta página de apuntes escritos a mano. Corrige errores, explica lo que esté mal y sugiere mejoras"*
- Historial de conversación por cuaderno (para dar contexto)
- Configuración de la API Key de Gemini (guardada localmente, nunca en el código fuente ni subida a ningún repositorio)

## 6. Integración con Gemini API (detalles para el agente)

- Endpoint: `POST https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key=API_KEY`
- Para enviar imagen + texto (multimodal), el body incluye un `inline_data` con la imagen en base64 y un `mime_type: image/png`, junto al texto del prompt en el mismo array de `parts`
- Manejar límites del plan gratuito (rate limiting) con reintentos y mensajes claros al usuario si se excede la cuota
- La API Key debe pedirse en un diálogo de configuración la primera vez que se abre la app y guardarse cifrada localmente (ej. con `ProtectedData` de Windows/DPAPI), nunca hardcodeada en el código

## 7. Requisitos no funcionales

- La app debe distinguir automáticamente toque de dedo (para pan/scroll) de toque de lápiz (para dibujar), usando las propiedades nativas de `StylusDevice` en WPF
- Autoguardado periódico (cada ~30s o al cambiar de página) para no perder apuntes
- Rendimiento fluido con páginas de muchos trazos (usar `InkCanvas` con `EditingMode` optimizado, evitar renders innecesarios)
- La app debe funcionar completamente offline excepto la función de asistente IA

## 8. Distribución e instalación (Fase final)

El objetivo es que cualquier persona pueda descargar e instalar la app como un programa normal, sin necesitar Visual Studio, .NET ni conocimientos técnicos. **No se usa Docker** (eso es para servidores, no para apps de escritorio). El camino correcto:

1. **Publicar como "self-contained"**: `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`
   Esto genera un único `.exe` que ya incluye el runtime de .NET embebido — el usuario no necesita instalar nada aparte.
2. **Empaquetar con un instalador** (recomendado para que se vea profesional, tenga icono, se registre en "Agregar o quitar programas", cree acceso directo en el escritorio, etc.):
   - **Inno Setup** (gratuito, muy usado, genera un `.exe` instalador clásico) — opción más simple
   - o **MSIX** (formato moderno de Microsoft, permite actualizaciones automáticas y se puede subir a la Microsoft Store si se desea a futuro)
3. Resultado final: un archivo como `CuadernoDigitalSetup.exe` que el usuario descarga, ejecuta, y sigue el asistente de instalación como con cualquier programa (similar a instalar Spotify, Zoom, etc.)
4. Firmar el instalador (code signing) es opcional pero evita que Windows Defender/SmartScreen muestre advertencias de "editor desconocido"; si no se tiene certificado, al menos advertir al usuario que puede aparecer ese aviso la primera vez.

## 9. Entregable esperado del agente

1. Proyecto WPF compilable en Visual Studio / `dotnet build`
2. Fase 1 y 2 completamente funcionales como primer entregable (cuadernos + dibujo)
3. Código organizado en MVVM, comentado, con nombres de clases en inglés y textos de UI en español
4. Todas las herramientas y elementos (dibujo, texto, tablas, imágenes) deben responder correctamente tanto a mouse+teclado como a lápiz/dedo táctil, sin necesitar dos versiones distintas de la app
5. Script o configuración de publicación (`dotnet publish` + proyecto de Inno Setup) que genere el instalador final `.exe`
6. README con instrucciones de compilación, ejecución, y de cómo generar el instalador
