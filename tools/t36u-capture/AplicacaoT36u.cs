using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using GestaoReservas.Tests;

public class AplicacaoT36u : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseContentRoot(@"C:\Users\isabe\GestaoReservas_BUILD_TMP");
        builder.UseWebRoot(@"C:\Users\isabe\GestaoReservas_BUILD_TMP\wwwroot");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAmbienteRepository>();
            services.RemoveAll<IMesaRepository>();
            services.RemoveAll<IClienteRepository>();
            services.RemoveAll<IFuncionarioRepository>();
            services.RemoveAll<IReservaRepository>();
            services.RemoveAll<IAuthRepository>();
            services.AddSingleton<IAmbienteRepository, AmbienteMemoryRepository>();
            services.AddSingleton<IMesaRepository, MesaMemoryRepository>();
            services.AddSingleton<IClienteRepository, ClienteMemoryRepository>();
            services.AddSingleton<IFuncionarioRepository, FuncionarioMemoryRepository>();
            services.AddSingleton<IReservaRepository, ReservaMemoryRepository>();
            services.AddSingleton<IAuthRepository, AuthMemoryRepository>();
        });
    }

    public HttpClient Cliente() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false
    });
}
