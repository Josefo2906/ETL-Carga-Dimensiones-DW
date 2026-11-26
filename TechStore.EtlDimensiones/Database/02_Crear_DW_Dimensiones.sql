CREATE DATABASE DW_TechStore;
GO
USE DW_TechStore;
GO

-- =============================================
-- 1. TABLAS DE DIMENSIONES
-- =============================================

CREATE TABLE DimFecha (
    FechaKey INT PRIMARY KEY,          -- Ej: 20261115
    Fecha DATE NOT NULL,
    Anio INT NOT NULL,
    Trimestre INT NOT NULL,
    Mes INT NOT NULL,
    NombreMes VARCHAR(20) NOT NULL,
    Dia INT NOT NULL,
    DiaSemana VARCHAR(20) NOT NULL,
    EsFinDeSemana BIT NOT NULL
);

CREATE TABLE DimCliente (
    ClienteKey INT IDENTITY(1,1) PRIMARY KEY,
    CodigoClienteOrigen VARCHAR(30) NOT NULL UNIQUE,
    NombreCompleto VARCHAR(150) NOT NULL,
    Correo VARCHAR(150) NOT NULL,
    Segmento VARCHAR(50) NOT NULL,
    OrigenRegistro VARCHAR(30) NOT NULL,
    FechaActualizacion DATETIME NOT NULL DEFAULT GETDATE()
);

CREATE TABLE DimProducto (
    ProductoKey INT IDENTITY(1,1) PRIMARY KEY,
    ProductoIdOrigen INT NOT NULL UNIQUE,
    SKU VARCHAR(20) NOT NULL,
    NombreProducto VARCHAR(120) NOT NULL,
    Marca VARCHAR(60) NOT NULL,
    Categoria VARCHAR(60) NOT NULL,
    PrecioCatalogo DECIMAL(10,2) NOT NULL,
    FechaActualizacion DATETIME NOT NULL DEFAULT GETDATE()
);

CREATE TABLE DimCanalFuente (
    CanalFuenteKey INT IDENTITY(1,1) PRIMARY KEY,
    CodigoCanal VARCHAR(30) NOT NULL UNIQUE,
    NombreCanal VARCHAR(100) NOT NULL,
    TipoFuente VARCHAR(50) NOT NULL,
    Descripcion VARCHAR(200) NOT NULL
);

-- =============================================
-- 2. TABLAS STAGING PARA CARGA MASIVA (.NET)
-- =============================================

CREATE TABLE Staging_DimCliente (
    CodigoClienteOrigen VARCHAR(30),
    NombreCompleto VARCHAR(150),
    Correo VARCHAR(150),
    Segmento VARCHAR(50),
    OrigenRegistro VARCHAR(30)
);

CREATE TABLE Staging_DimProducto (
    ProductoIdOrigen INT,
    SKU VARCHAR(20),
    NombreProducto VARCHAR(120),
    Marca VARCHAR(60),
    Categoria VARCHAR(60),
    PrecioCatalogo DECIMAL(10,2)
);

CREATE TABLE Staging_DimCanalFuente (
    CodigoCanal VARCHAR(30),
    NombreCanal VARCHAR(100),
    TipoFuente VARCHAR(50),
    Descripcion VARCHAR(200)
);
GO

-- =============================================
-- 3. PROCEDIMIENTOS MERGE (SCD TIPO 1)
-- =============================================

CREATE OR ALTER PROCEDURE sp_Merge_DimCliente
AS
BEGIN
    SET NOCOUNT ON;

    WITH ClientesUnicos AS (
        SELECT 
            LTRIM(RTRIM(CodigoClienteOrigen)) AS CodigoClienteOrigen,
            LTRIM(RTRIM(NombreCompleto)) AS NombreCompleto,
            LOWER(LTRIM(RTRIM(Correo))) AS Correo,
            LTRIM(RTRIM(Segmento)) AS Segmento,
            OrigenRegistro,
            ROW_NUMBER() OVER (PARTITION BY LTRIM(RTRIM(CodigoClienteOrigen)) ORDER BY OrigenRegistro DESC) AS rn
        FROM Staging_DimCliente
        WHERE CodigoClienteOrigen IS NOT NULL
    )
    MERGE INTO DimCliente AS Target
    USING (SELECT * FROM ClientesUnicos WHERE rn = 1) AS Source
    ON Target.CodigoClienteOrigen = Source.CodigoClienteOrigen
    WHEN MATCHED AND (
        Target.NombreCompleto <> Source.NombreCompleto OR
        Target.Correo <> Source.Correo OR
        Target.Segmento <> Source.Segmento
    ) THEN
        UPDATE SET
            Target.NombreCompleto = Source.NombreCompleto,
            Target.Correo = Source.Correo,
            Target.Segmento = Source.Segmento,
            Target.FechaActualizacion = GETDATE()
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (CodigoClienteOrigen, NombreCompleto, Correo, Segmento, OrigenRegistro)
        VALUES (Source.CodigoClienteOrigen, Source.NombreCompleto, Source.Correo, Source.Segmento, Source.OrigenRegistro);

    TRUNCATE TABLE Staging_DimCliente;
END;
GO

CREATE OR ALTER PROCEDURE sp_Merge_DimProducto
AS
BEGIN
    SET NOCOUNT ON;

    MERGE INTO DimProducto AS Target
    USING (
        SELECT DISTINCT ProductoIdOrigen, SKU, NombreProducto, Marca, Categoria, PrecioCatalogo
        FROM Staging_DimProducto
        WHERE ProductoIdOrigen IS NOT NULL
    ) AS Source
    ON Target.ProductoIdOrigen = Source.ProductoIdOrigen
    WHEN MATCHED AND (
        Target.NombreProducto <> Source.NombreProducto OR
        Target.Categoria <> Source.Categoria OR
        Target.PrecioCatalogo <> Source.PrecioCatalogo
    ) THEN
        UPDATE SET
            Target.SKU = Source.SKU,
            Target.NombreProducto = Source.NombreProducto,
            Target.Marca = Source.Marca,
            Target.Categoria = Source.Categoria,
            Target.PrecioCatalogo = Source.PrecioCatalogo,
            Target.FechaActualizacion = GETDATE()
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (ProductoIdOrigen, SKU, NombreProducto, Marca, Categoria, PrecioCatalogo)
        VALUES (Source.ProductoIdOrigen, Source.SKU, Source.NombreProducto, Source.Marca, Source.Categoria, Source.PrecioCatalogo);

    TRUNCATE TABLE Staging_DimProducto;
END;
GO

CREATE OR ALTER PROCEDURE sp_Merge_DimCanalFuente
AS
BEGIN
    SET NOCOUNT ON;

    MERGE INTO DimCanalFuente AS Target
    USING (
        SELECT DISTINCT CodigoCanal, NombreCanal, TipoFuente, Descripcion
        FROM Staging_DimCanalFuente
        WHERE CodigoCanal IS NOT NULL
    ) AS Source
    ON Target.CodigoCanal = Source.CodigoCanal
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (CodigoCanal, NombreCanal, TipoFuente, Descripcion)
        VALUES (Source.CodigoCanal, Source.NombreCanal, Source.TipoFuente, Source.Descripcion);

    TRUNCATE TABLE Staging_DimCanalFuente;
END;
GO