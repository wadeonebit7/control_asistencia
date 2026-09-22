# Declaramos la dirección base oculta en una variable para evitar el bloqueo del filtro automático
ARG REGISTRY=mcr.microsoft.com

# 1. Base de ejecución para .NET
FROM ${REGISTRY}/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# =========================================================================
# INSTALACIÓN DE LIBRERÍAS NATIVAS, TESSERACT CLI Y PDFIUM PARA LINUX
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
    # 2. Copiar y crear los enlaces con los nombres exactos que PdfiumViewer busca en Linux
    && cp lib/libpdfium.so /app/libpdfium.so \
    && cp lib/libpdfium.so /app/pdfium.dll \
    && cp lib/libpdfium.so /usr/lib/libpdfium.so \
    && ln -s /app/libpdfium.so /app/libpdfium.dll.so || true \
    && rm -rf pdfium.tgz bin lib include \
    && rm -rf /var/lib/apt/lists/*

# 2. SDK de .NET para compilar el código
FROM ${REGISTRY}/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["control_asistencia.csproj", "."]
RUN dotnet restore "./control_asistencia.csproj"

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

# Aseguramos que el binario de pdfium persista en el directorio de trabajo final de la app
RUN wget -q -O pdfium.tgz https://github.com/bblanchon/pdfium-binaries/releases/latest/download/pdfium-linux-x64.tgz \
    && tar -xzf pdfium.tgz \
    && cp lib/libpdfium.so /app/libpdfium.so \
    && cp lib/libpdfium.so /app/pdfium.dll \
    && rm -rf pdfium.tgz bin lib include || true

ENTRYPOINT ["dotnet", "control_asistencia.dll"]