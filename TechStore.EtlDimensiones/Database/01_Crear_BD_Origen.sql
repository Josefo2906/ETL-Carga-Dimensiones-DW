CREATE DATABASE BD_TechStore_OLTP;
GO
USE BD_TechStore_OLTP;
GO

CREATE TABLE Productos (
    ProductoId INT PRIMARY KEY,
    SKU VARCHAR(20) NOT NULL,
    NombreProducto VARCHAR(120) NOT NULL,
    Marca VARCHAR(60) NOT NULL,
    Categoria VARCHAR(60) NOT NULL,
    PrecioUnitario DECIMAL(10,2) NOT NULL
);

CREATE TABLE ResenasProductos (
    ResenaId INT PRIMARY KEY,
    ProductoId INT NOT NULL FOREIGN KEY REFERENCES Productos(ProductoId),
    CodigoCliente VARCHAR(20) NOT NULL,
    NombreCliente VARCHAR(120) NOT NULL,
    CorreoCliente VARCHAR(120) NOT NULL,
    SegmentoCliente VARCHAR(40) NOT NULL,
    Puntuacion INT NOT NULL,
    DetalleResena VARCHAR(300) NOT NULL,
    FechaResena DATE NOT NULL
);

INSERT INTO Productos (ProductoId, SKU, NombreProducto, Marca, Categoria, PrecioUnitario) VALUES
(10, 'LAP-DELL-15', 'Laptop Latitude 5540 Core i7', 'Dell', 'Computadoras', 58500.00),
(20, 'MON-LG-27', 'Monitor UltraGear 27 Pulgadas QHD', 'LG', 'Monitores', 19800.00),
(30, 'TEC-LOG-MX', 'Teclado Mecánico Inalámbrico MX Keys', 'Logitech', 'Periféricos', 6400.00),
(40, 'SIL-ERG-01', 'Silla Ergonómica Malla Pro X', 'Herman', 'Mobiliario', 14200.00),
(50, 'ROU-ASU-AX', 'Router WiFi 6 Doble Banda AX3000', 'ASUS', 'Redes', 7900.00),
(60, 'SSD-SAM-1T', 'Unidad SSD NVMe 990 PRO 1TB', 'Samsung', 'Almacenamiento', 6800.00),
(70, 'AUD-SON-XM', 'Audífonos Cancelación de Ruido WH-1000XM5', 'Sony', 'Audio', 17500.00),
(80, 'WEB-LOG-4K', 'Cámara Web Brio 4K Ultra HD', 'Logitech', 'Accesorios', 9200.00);

INSERT INTO ResenasProductos (ResenaId, ProductoId, CodigoCliente, NombreCliente, CorreoCliente, SegmentoCliente, Puntuacion, DetalleResena, FechaResena) VALUES
(501, 10, 'CLI-1003', 'Miguel Ángel Rosario', 'mrosario@gmail.com', 'Minorista', 5, 'Rendimiento excelente para desarrollo y contenedores Docker', '2026-08-20'),
(502, 20, 'CLI-1002', 'Laura Peña', 'laura.pena@empresa.com', 'VIP', 4, 'Colores muy nítidos para diseño y análisis de datos', '2026-08-18'),
(503, 30, 'CLI-1011', 'Fernando Díaz', 'fdiaz@consultores.do', 'Corporativo', 5, 'Batería de larga duración y escritura silenciosa', '2026-09-05'),
(504, 40, 'CLI-1008', 'Valeria Santana', 'vsantana@estudio.do', 'VIP', 5, 'Muy cómoda para jornadas largas frente al computador', '2026-10-12'),
(505, 50, 'CLI-1012', 'Natalia Vargas', 'nvargas@correo.do', 'Minorista', 4, 'Mejoró bastante la cobertura en toda la casa', '2026-10-15'),
(506, 60, 'CLI-1013', 'Ricardo Gómez', 'rgomez@devmail.com', 'Corporativo', 5, 'Velocidades de lectura impresionantes en SQL Server', '2026-10-22'),
(507, 70, 'CLI-1006', 'Patricia Guzmán', 'pguzman@outlook.com', 'VIP', 5, 'Aislamiento acústico espectacular para videollamadas', '2026-10-28'),
(508, 80, 'CLI-1014', 'Daniela Reyes', 'dreyes@agencia.do', 'Corporativo', 4, 'Imagen muy clara incluso con poca iluminación', '2026-11-02'),
(509, 10, 'CLI-1015', 'Javier Morales', 'jmorales@correo.do', 'Minorista', 3, 'Buen equipo pero el cargador calienta un poco', '2026-11-07'),
(510, 60, 'CLI-1005', 'Roberto Castillo', 'rcastillo@techcorp.do', 'Corporativo', 5, 'Compramos 10 unidades para actualizar servidores locales', '2026-11-12');
GO