# ProNotes

ProNotes es un cuaderno digital de escritorio para Windows, creado con C#/.NET y WPF. Esta version implementa el nucleo del spec: biblioteca de cuadernos, formato `.cdgz`, paginas, escritura/dibujo con `InkCanvas`, fondos de hoja, herramientas de tinta, zoom, autoguardado, imagenes embebidas, cajas de texto editables con formato, tablas editables, graficas simples y editor de portadas.

## Requisitos

- Windows 10/11
- .NET 8 SDK
- Visual Studio 2022 con workload ".NET desktop development" o CLI `dotnet`

## Ejecutar

```powershell
dotnet restore
dotnet build .\CuadernoDigital.sln
dotnet run --project .\src\CuadernoDigital.App\CuadernoDigital.App.csproj
```

Los cuadernos se guardan en `Documentos\ProNotes` como archivos `.cdgz`.

## Publicar EXE

```powershell
dotnet publish .\src\CuadernoDigital.App\CuadernoDigital.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\publish
```

## Instalador

1. Instala Inno Setup.
2. Ejecuta `installer\ProNotes.iss`.
3. El instalador se genera como `installer\dist\ProNotesSetup.exe`.

## Roadmap

- Fase 1: cuadernos y paginas, implementada.
- Fase 2: lienzo de dibujo, fondos, herramientas, zoom y autoguardado, implementada.
- Fase 3: imagenes embebidas, movibles, redimensionables, rotables, duplicables y ordenables por capas, implementada parcialmente. Falta recorte.
- Fase 4: cajas de texto editables con formato, tablas editables y graficas simples, movibles, redimensionables, rotables, duplicables y ordenables por capas. Las tablas permiten elegir filas/columnas al insertarlas y modificar filas/columnas despues. Las graficas soportan barras, lineas y circular desde datos ingresados por el usuario.
- Fase 5: editor de portadas implementado con titulo, subtitulo, color, acento, imagen de fondo opcional, plantillas y vista previa en la biblioteca.
- Fase 6: exportacion PDF con QuestPDF.
- Fase 7: asistente Gemini con API key protegida localmente.
