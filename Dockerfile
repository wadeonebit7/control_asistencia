# Declaramos la dirección base oculta en una variable para evitar el bloqueo del filtro automático
ARG REGISTRY=mcr.microsoft.com

# 1. Base de ejecución para .NET
FROM ${REGISTRY}/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# =========================================================================
# INSTALACIÓN DE LIBRERÍAS NATIVAS, TESSERACT Y DESCARGA DIRECTA DE LEPTONICA
# =========================================================================
RUN apt-get update && apt-get install -y --allow-unauthenticated \
    libgdiplus \
    libc6-dev \
    tesseract-ocr \
    tesseract-ocr-spa \
    libtesseract-dev \
    curl \
    tar \
    # 1. Solución para libdl en .NET 8 Linux
    && ln -s /lib/x86_64-linux-gnu/libc.so.6 /usr/lib/libdl.so || true \
    && ln -s /lib/x86_64-linux-gnu/libc.so.6 /usr/lib/libdl.so.2 || true \
    # 2. Crear carpetas de ejecución
    && mkdir -p /app/x64 \
    # 3. Descarga directa del binario exacto de Leptonica 1.82.0 requerido por el NuGet
    && curl -L -o /app/libleptonica-1.82.0.so "https://raw.githubusercontent.com/charlesw/tesseract/master/Net20/Tesseract.Tests/x64/libleptonica-1.82.0.so" \
    && cp /app/libleptonica-1.82.0.so /app/x64/libleptonica-1.82.0.so \
    && cp /app/libleptonica-1.82.0.so /usr/lib/libleptonica-1.82.0.so \
    # 4. Descargar Pdfium nativo para Linux
    && curl -L -o pdfium.tgz https://github.com/bblanchon/pdfium-binaries/releases/latest/download/pdfium-linux-x64.tgz \
    && tar -xzf pdfium.tgz \
    && cp lib/libpdfium.so /usr/lib/libpdfium.so \
    && rm -rf pdfium.tgz bin lib include \
    && chmod 755 /app/libleptonica-1.82.0.so /app/x64/libleptonica-1.82.0.so /usr/lib/libleptonica-1.82.0.so \
    && rm -rf /var/lib/apt/lists/*

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

# 4. Configuración final del arranque
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "control_asistencia.dll"]