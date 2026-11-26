using TechStore.EtlDimensiones.Services;

namespace TechStore.EtlDimensiones;

public class Worker : BackgroundService
{
    private readonly DimensionEtlService _etlService;
    private readonly ILogger<Worker> _logger;

    public Worker(DimensionEtlService etlService, ILogger<Worker> logger)
    {
        _etlService = etlService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _etlService.EjecutarCargaDimensionesAsync(stoppingToken);
        _logger.LogInformation("Ejecución inicial finalizada. Presiona Ctrl+C para salir.");
    }
}