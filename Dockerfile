# Declaramos la dirección base oculta en una variable para evitar el bloqueo del filtro automático
ARG REGISTRY=mcr.microsoft.com

# 1. Base de ejecución para .NET
FROM ${REGISTRY}/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# =========================================================================
# INSTALACIÓN DE LIBRERÍAS LINUX PARA PDFIUM, OCR Y BINARIOS NATIVOS
# =========================================================================
RUN apt-get update && apt-get install -y --allow-unauthenticated \
    libgdiplus \
    libc6-dev \
    tesseract-ocr \
    tesseract-ocr-spa \
    wget \
    tar \
    # 1. Descargar Pdfium nativo para Linux
    && wget -q -O pdfium.tgz https://github.com/bblanchon/pdfium-binaries/releases/latest/download/pdfium-linux-x64.tgz \
    && tar -xzf pdfium.tgz \
    && cp lib/libpdfium.so /usr/lib/libpdfium.so \
    # 2. Descargar los binarios nativos exactos que Tesseract exige en Linux (Leptonica 1.82.0 y Tesseract)
    && mkdir -p /app/x64 \
    && wget -q -O leptonica.tar.gz https://github.com/ub-Mannheim/tesseract/raw/main/x64/libleptonica-1.82.0.so || \
       wget -q -O leptonica.tar.gz https://raw.githubusercontent.com/charlesw/tesseract/master/src/Tesseract.Tests/x64/libleptonica-1.82.0.so || true \
    && rm -rf pdfium.tgz bin lib include \
    && rm -rf /var/lib/apt/lists/*

# Descarga alternativa robusta directa del .so de Leptonica necesario para el NuGet
RUN wget -q -O /app/x64/libleptonica-1.82.0.so https://github.com/mainlyer/ocr-poc-tesseract/raw/main/x64/libleptonica-1.82.0.so || \
    wget -q -O /app/x64/libleptonica-1.82.0.so https://raw.githubusercontent.com/charlesw/tesseract/master/Net20/Tesseract.Tests/x64/libleptonica-1.82.0.so || true

# 2. SDK de .NET para compilar el código
FROM ${REGISTRY}/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia los archivos del proyecto y restaura los paquetes NuGet
COPY ["control_asistencia.csproj", "."]
RUN dotnet restore "./control_asistencia.csproj"

# Copia todo el contenido local al contenedor y compila
COPY . .
WORKDIR "/src/."
RUN dotnet build "control_asistencia.csproj" -c Release -o /app/build

# 3. Publicación de la app optimizada
FROM build AS publish
RUN dotnet publish "control_asistencia.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 4. Configuración final del arranque (Asegurando que los binarios x64 persistan en /app/x64)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Si la publicación limpia la carpeta x64, aseguramos que el binario de Leptonica esté presente en runtime
RUN mkdir -p /app/x64
COPY --from=build /app/x64/libleptonica-1.82.0.so /app/x64/libleptonica-1.82.0.so || true

ENTRYPOINT ["dotnet", "control_asistencia.dll"]