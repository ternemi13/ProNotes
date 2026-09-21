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

`metadata.json` contiene los datos globales del cuaderno: id, titulo, portada, fechas y la lista ordenada de paginas.

Cada pagina contiene:

- `id`: identificador estable.
- `title`: nombre visible de la pagina.
- `backgroundType`: `Plain`, `Grid`, `Lined` o `Dotted`.
- `inkBase64`: trazos WPF serializados en formato ISF y convertidos a Base64.
- `images`: lista reservada para la Fase 3.

El formato esta pensado para poder guardar todo sin base de datos, copiar cuadernos entre maquinas y versionar el archivo como una unidad.
