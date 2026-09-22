# Formato `.cdgz`

Un cuaderno de ProNotes es un archivo ZIP con extension `.cdgz`.

## Estructura

```text
metadata.json
pages/
  page_<id>.json
assets/
  img_<id>.png
```

`metadata.json` contiene los datos globales del cuaderno: id, titulo, portada, fechas, historial del asistente IA y la lista ordenada de paginas.

La portada guarda `backgroundColor`, `accentColor`, `backgroundImageBase64`, `backgroundImageMimeType`, `backgroundImageOffsetX`, `backgroundImageOffsetY`, `backgroundImageScale`, `templateName`, `title` y `subtitle`.

El historial IA se guarda en `aiChatHistory` con mensajes `{ role, text, createdAt }`. La API key de Gemini no forma parte del archivo `.cdgz`; se guarda cifrada localmente en el perfil de Windows.

Cada pagina contiene:

- `id`: identificador estable.
- `title`: nombre visible de la pagina.
- `backgroundType`: `Plain`, `Grid`, `Lined` o `Dotted`.
- `inkBase64`: trazos WPF serializados en formato ISF y convertidos a Base64.
- `images`: imagenes insertadas con metadata de posicion, tamano, rotacion, orden de capa y contenido Base64.
- `textBoxes`: cajas de texto editables con posicion, tamano, rotacion, orden de capa, contenido, color, tamano de fuente, negrita, cursiva, subrayado y alineacion.
- `tables`: tablas con posicion, tamano, rotacion, orden de capa, filas, columnas y texto editable por celda.
- `charts`: graficas simples con posicion, tamano, rotacion, orden de capa, titulo, tipo (`Bar`, `Line` o `Pie`) y datos etiqueta/valor.

El formato esta pensado para poder guardar todo sin base de datos, copiar cuadernos entre maquinas y versionar el archivo como una unidad. En esta version las imagenes quedan embebidas en el JSON de pagina para mantener el archivo `.cdgz` autocontenido.
