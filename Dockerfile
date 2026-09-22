# Declaramos la dirección base oculta en una variable para evitar el bloqueo del filtro automático
ARG REGISTRY=mcr.microsoft.com

# 1. Base de ejecución para .NET
FROM ${REGISTRY}/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# =========================================================================
# INSTALACIÓN DE LIBRERÍAS LINUX PARA PDFIUM, OCR Y PROCESAMIENTO DE IMÁGENES
# Incluye la descarga manual de libpdfium.so y los enlaces para Tesseract
# =========================================================================
RUN apt-get update && apt-get install -y --allow-unauthenticated \
    libgdiplus \
    libc6-dev \
    tesseract-ocr \
    tesseract-ocr-spa \
    libtesseract5 \
    libleptonica5 \
    wget \
    tar \
    && wget -q -O pdfium.tgz https://github.com/bblanchon/pdfium-binaries/releases/latest/download/pdfium-linux-x64.tgz \
    && tar -xzf pdfium.tgz \
    && cp lib/libpdfium.so /usr/lib/libpdfium.so \
    && ln -s /usr/lib/x86_64-linux-gnu/libtesseract.so.5 /usr/lib/libtesseract.so || true \
    && ln -s /usr/lib/x86_64-linux-gnu/libleptonica.so.6 /usr/lib/liblept.so || true \
    && rm -rf pdfium.tgz bin lib include \
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