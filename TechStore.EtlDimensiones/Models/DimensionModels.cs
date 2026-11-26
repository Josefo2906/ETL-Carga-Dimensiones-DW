namespace TechStore.EtlDimensiones.Models;

public class EncuestaCsvRow
{
    public string CodigoEncuesta { get; set; } = string.Empty;
    public string CodigoCliente { get; set; } = string.Empty;
    public string NombreCliente { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Segmento { get; set; } = string.Empty;
    public string CanalCompra { get; set; } = string.Empty;
    public int Calificacion { get; set; }
    public string Comentario { get; set; } = string.Empty;
    public DateTime FechaEncuesta { get; set; }
}

public record DummyJsonCommentsResponse(List<DummyComment> Comments);
public record DummyComment(int Id, string Body, int PostId, int Likes, DummyUser User);
public record DummyUser(int Id, string Username, string FullName);

public record DimClienteStage(string CodigoClienteOrigen, string NombreCompleto, string Correo, string Segmento, string OrigenRegistro);
public record DimProductoStage(int ProductoIdOrigen, string SKU, string NombreProducto, string Marca, string Categoria, decimal PrecioCatalogo);
public record DimCanalFuenteStage(string CodigoCanal, string NombreCanal, string TipoFuente, string Descripcion);