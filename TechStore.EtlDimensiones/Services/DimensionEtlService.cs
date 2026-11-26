using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using CsvHelper;
using Microsoft.Data.SqlClient;
using TechStore.EtlDimensiones.Models;

namespace TechStore.EtlDimensiones.Services;

public class DimensionEtlService
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<DimensionEtlService> _logger;

    public DimensionEtlService(
        IConfiguration config,
        IHttpClientFactory httpFactory,
        ILogger<DimensionEtlService> logger)
    {
        _config = config;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task EjecutarCargaDimensionesAsync(CancellationToken ct)
    {
        var swTotal = Stopwatch.StartNew();
        _logger.LogInformation("=== INICIANDO PROCESO ETL DE CARGA DE DIMENSIONES (DW_TechStore) ===");

        // 1. Extracción en paralelo de las 3 fuentes
        var csvTask = ExtraerCsvAsync(ct);
        var sqlTask = ExtraerSqlOrigenAsync(ct);
        var apiTask = ExtraerApiRestAsync(ct);

        await Task.WhenAll(csvTask, sqlTask, apiTask);

        var datosCsv = await csvTask;
        var (productosSql, resenasSql) = await sqlTask;
        var usuariosApi = await apiTask;

        // 2. Transformación y consolidación de clientes
        var clientesConsolidados = new List<DimClienteStage>();

        clientesConsolidados.AddRange(datosCsv.Select(c => new DimClienteStage(
            c.CodigoCliente.Trim(),
            c.NombreCliente.Trim(),
            c.Correo.Trim().ToLower(),
            c.Segmento.Trim(),
            "CSV_Encuestas"
        )));

        clientesConsolidados.AddRange(resenasSql);

        clientesConsolidados.AddRange(usuariosApi.Select(u => new DimClienteStage(
            $"API-{u.Id}",
            u.FullName.Trim(),
            $"{u.Username.ToLower()}@dummyjson.org",
            "Comunidad Digital",
            "API_DummyJSON"
        )));

        var clientesUnicos = clientesConsolidados
            .GroupBy(c => c.CodigoClienteOrigen, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        var canales = new List<DimCanalFuenteStage>
        {
            new("CSV_ENCUESTA", "Encuestas Postventa CSV", "Archivo Plano", "Encuestas internas exportadas desde CRM"),
            new("SQL_RESENA", "Reseñas Tienda Online SQL", "Base de Datos", "Reseñas verificadas en BD_TechStore_OLTP"),
            new("API_COMENTARIO", "Comentarios Externos API", "API REST", "Interacciones sociales obtenidas vía DummyJSON API"),
            new("WEB_ECOMMERCE", "Portal Web E-Commerce", "Canal Digital", "Compras y opiniones directas en tienda web"),
            new("APP_MOVIL", "Aplicación Móvil TechStore", "Canal Digital", "Compras y encuestas desde iOS/Android")
        };

        // 3. Carga en el Data Warehouse (DW_TechStore)
        string dwConnStr = _config.GetConnectionString("DestinoDw")!;
        await using var dwConn = new SqlConnection(dwConnStr);
        await dwConn.OpenAsync(ct);

        await CargarDimFechaAsync(dwConn, new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), ct);
        await CargarDimClienteAsync(dwConn, clientesUnicos, ct);
        await CargarDimProductoAsync(dwConn, productosSql, ct);
        await CargarDimCanalFuenteAsync(dwConn, canales, ct);

        swTotal.Stop();
        _logger.LogInformation("=== CARGA DE DIMENSIONES COMPLETADA EXITOSAMENTE EN {Ms} ms ===", swTotal.ElapsedMilliseconds);
    }

    private async Task<List<EncuestaCsvRow>> ExtraerCsvAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        string path = _config["EtlConfig:CsvEncuestasPath"]!;
        var lista = new List<EncuestaCsvRow>();

        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        await foreach (var row in csv.GetRecordsAsync<EncuestaCsvRow>(ct))
        {
            if (row.Calificacion is >= 1 and <= 5)
                lista.Add(row);
        }

        sw.Stop();
        _logger.LogInformation("[EXTRACTOR CSV] {Count} registros válidos leídos en {Ms} ms.", lista.Count, sw.ElapsedMilliseconds);
        return lista;
    }

    private async Task<(List<DimProductoStage> Productos, List<DimClienteStage> ClientesResenas)> ExtraerSqlOrigenAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        string origenConnStr = _config.GetConnectionString("OrigenOltp")!;
        var productos = new List<DimProductoStage>();
        var clientes = new List<DimClienteStage>();

        await using var conn = new SqlConnection(origenConnStr);
        await conn.OpenAsync(ct);

        await using (var cmdProd = new SqlCommand("SELECT ProductoId, SKU, NombreProducto, Marca, Categoria, PrecioUnitario FROM Productos", conn))
        await using (var rdr = await cmdProd.ExecuteReaderAsync(ct))
        {
            while (await rdr.ReadAsync(ct))
            {
                productos.Add(new DimProductoStage(
                    rdr.GetInt32(0),
                    rdr.GetString(1),
                    rdr.GetString(2),
                    rdr.GetString(3),
                    rdr.GetString(4),
                    rdr.GetDecimal(5)
                ));
            }
        }

        await using (var cmdCli = new SqlCommand("SELECT DISTINCT CodigoCliente, NombreCliente, CorreoCliente, SegmentoCliente FROM ResenasProductos", conn))
        await using (var rdr = await cmdCli.ExecuteReaderAsync(ct))
        {
            while (await rdr.ReadAsync(ct))
            {
                clientes.Add(new DimClienteStage(
                    rdr.GetString(0),
                    rdr.GetString(1),
                    rdr.GetString(2),
                    rdr.GetString(3),
                    "SQL_Resenas"
                ));
            }
        }

        sw.Stop();
        _logger.LogInformation("[EXTRACTOR SQL] {Prod} productos y {Cli} clientes extraídos en {Ms} ms.", productos.Count, clientes.Count, sw.ElapsedMilliseconds);
        return (productos, clientes);
    }

    private async Task<List<DummyUser>> ExtraerApiRestAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var client = _httpFactory.CreateClient("DummyJsonClient");
        string endpoint = _config["EtlConfig:ApiCommentsEndpoint"]!;

        var response = await client.GetFromJsonAsync<DummyJsonCommentsResponse>(endpoint, ct);
        var usuarios = response?.Comments
            .Select(c => c.User)
            .DistinctBy(u => u.Id)
            .ToList() ?? [];

        sw.Stop();
        _logger.LogInformation("[EXTRACTOR API] {Count} usuarios únicos extraídos desde DummyJSON en {Ms} ms.", usuarios.Count, sw.ElapsedMilliseconds);
        return usuarios;
    }

    private async Task CargarDimFechaAsync(SqlConnection conn, DateTime inicio, DateTime fin, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        const string sql = """
            SET LANGUAGE Spanish;
            DECLARE @Fecha DATE = @Inicio;
            WHILE @Fecha <= @Fin
            BEGIN
                DECLARE @Key INT = CAST(CONVERT(VARCHAR(8), @Fecha, 112) AS INT);
                IF NOT EXISTS (SELECT 1 FROM DimFecha WHERE FechaKey = @Key)
                BEGIN
                    INSERT INTO DimFecha (FechaKey, Fecha, Anio, Trimestre, Mes, NombreMes, Dia, DiaSemana, EsFinDeSemana)
                    VALUES (
                        @Key, @Fecha, YEAR(@Fecha), DATEPART(QUARTER, @Fecha), MONTH(@Fecha),
                        DATENAME(MONTH, @Fecha), DAY(@Fecha), DATENAME(WEEKDAY, @Fecha),
                        CASE WHEN DATEPART(WEEKDAY, @Fecha) IN (1, 7) THEN 1 ELSE 0 END
                    );
                END
                SET @Fecha = DATEADD(DAY, 1, @Fecha);
            END
            """;

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Inicio", inicio);
        cmd.Parameters.AddWithValue("@Fin", fin);
        await cmd.ExecuteNonQueryAsync(ct);

        sw.Stop();
        _logger.LogInformation("[CARGA DW -> DimFecha] Rango 2026 verificado/cargado en {Ms} ms.", sw.ElapsedMilliseconds);
    }

    private async Task CargarDimClienteAsync(SqlConnection conn, List<DimClienteStage> clientes, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var dt = new DataTable();
        dt.Columns.Add("CodigoClienteOrigen", typeof(string));
        dt.Columns.Add("NombreCompleto", typeof(string));
        dt.Columns.Add("Correo", typeof(string));
        dt.Columns.Add("Segmento", typeof(string));
        dt.Columns.Add("OrigenRegistro", typeof(string));

        foreach (var c in clientes)
            dt.Rows.Add(c.CodigoClienteOrigen, c.NombreCompleto, c.Correo, c.Segmento, c.OrigenRegistro);

        using (var bulk = new SqlBulkCopy(conn))
        {
            bulk.DestinationTableName = "Staging_DimCliente";
            await bulk.WriteToServerAsync(dt, ct);
        }

        await using var cmd = new SqlCommand("EXEC sp_Merge_DimCliente", conn);
        await cmd.ExecuteNonQueryAsync(ct);

        sw.Stop();
        _logger.LogInformation("[CARGA DW -> DimCliente] {Count} clientes únicos sincronizados (Staging + MERGE) en {Ms} ms.", clientes.Count, sw.ElapsedMilliseconds);
    }

    private async Task CargarDimProductoAsync(SqlConnection conn, List<DimProductoStage> productos, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var dt = new DataTable();
        dt.Columns.Add("ProductoIdOrigen", typeof(int));
        dt.Columns.Add("SKU", typeof(string));
        dt.Columns.Add("NombreProducto", typeof(string));
        dt.Columns.Add("Marca", typeof(string));
        dt.Columns.Add("Categoria", typeof(string));
        dt.Columns.Add("PrecioCatalogo", typeof(decimal));

        foreach (var p in productos)
            dt.Rows.Add(p.ProductoIdOrigen, p.SKU, p.NombreProducto, p.Marca, p.Categoria, p.PrecioCatalogo);

        using (var bulk = new SqlBulkCopy(conn))
        {
            bulk.DestinationTableName = "Staging_DimProducto";
            await bulk.WriteToServerAsync(dt, ct);
        }

        await using var cmd = new SqlCommand("EXEC sp_Merge_DimProducto", conn);
        await cmd.ExecuteNonQueryAsync(ct);

        sw.Stop();
        _logger.LogInformation("[CARGA DW -> DimProducto] {Count} productos sincronizados (Staging + MERGE) en {Ms} ms.", productos.Count, sw.ElapsedMilliseconds);
    }

    private async Task CargarDimCanalFuenteAsync(SqlConnection conn, List<DimCanalFuenteStage> canales, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var dt = new DataTable();
        dt.Columns.Add("CodigoCanal", typeof(string));
        dt.Columns.Add("NombreCanal", typeof(string));
        dt.Columns.Add("TipoFuente", typeof(string));
        dt.Columns.Add("Descripcion", typeof(string));

        foreach (var ch in canales)
            dt.Rows.Add(ch.CodigoCanal, ch.NombreCanal, ch.TipoFuente, ch.Descripcion);

        using (var bulk = new SqlBulkCopy(conn))
        {
            bulk.DestinationTableName = "Staging_DimCanalFuente";
            await bulk.WriteToServerAsync(dt, ct);
        }

        await using var cmd = new SqlCommand("EXEC sp_Merge_DimCanalFuente", conn);
        await cmd.ExecuteNonQueryAsync(ct);

        sw.Stop();
        _logger.LogInformation("[CARGA DW -> DimCanalFuente] {Count} canales sincronizados en {Ms} ms.", canales.Count, sw.ElapsedMilliseconds);
    }
}