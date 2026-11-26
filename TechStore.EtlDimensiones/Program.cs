using TechStore.EtlDimensiones;
using TechStore.EtlDimensiones.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient("DummyJsonClient", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["EtlConfig:ApiBaseUrl"]!);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddSingleton<DimensionEtlService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();