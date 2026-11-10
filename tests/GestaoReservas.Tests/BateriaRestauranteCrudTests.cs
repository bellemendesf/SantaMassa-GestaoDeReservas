using System.Net;
using System.Text.RegularExpressions;
using GestaoReservas.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GestaoReservas.Tests;

public class BateriaRestauranteTests
{
    private static async Task PrepararReservasParaFiltro(AplicacaoTeste app, HttpClient client)
    {
        TesteApoio.PrepararMesaTeste(app.Services);
        TesteApoio.PrepararMaisMesas(app.Services, 4);
        await TesteApoio.LoginClienteTeste(client, app.Services, cpf: "11111111111");
        // Ajusta o nome do primeiro cliente para ter acento.
        var repoCli = app.Services.GetRequiredService<IClienteRepository>();
        var joao = repoCli.Read().First(c => c.Cpf == "11111111111");
        joao.Nome = "João Mendes";
        repoCli.Update(joao);
        Assert.Equal(HttpStatusCode.Redirect, (await TesteApoio.PostReservaComQtd(client, "2027-01-25", 2, 1)).StatusCode);

        using var mariaClient = app.Cliente();
        await TesteApoio.LoginClienteTeste(mariaClient, app.Services, cpf: "22222222222");
        var maria = repoCli.Read().First(c => c.Cpf == "22222222222");
        maria.Nome = "Maria Oliveira";
        repoCli.Update(maria);
        Assert.Equal(HttpStatusCode.Redirect, (await TesteApoio.PostReservaComQtd(mariaClient, "2027-01-26", 2, 2)).StatusCode);

        var reservas = app.Services.GetRequiredService<IReservaRepository>();
        // Segunda reserva da Maria já confirmada (via repositório, como faria o restaurante).
        var rMaria = reservas.Read().First(r => r.ClienteId == maria.Id);
        rMaria.Status = "Confirmada";
        reservas.Update(rMaria);
    }

    [Fact]
    public async Task RestauranteAlteraStatusPendenteConfirmadaCancelada()
    {
        using var app = new AplicacaoTeste();
        TesteApoio.PrepararMesaTeste(app.Services);
        using var cli = app.Cliente();
        await TesteApoio.LoginClienteTeste(cli, app.Services);
        Assert.Equal(HttpStatusCode.Redirect, (await TesteApoio.PostReservaComQtd(cli, "2027-01-27", 2, 1)).StatusCode);
        var id = app.Services.GetRequiredService<IReservaRepository>().Read().Single().Id;

        using var func = app.Cliente();
        await TesteApoio.LoginFuncionarioTeste(func, app.Services);
        var conf = await TesteApoio.Post(func, $"/AreaRestaurante/Edit/{id}", $"/AreaRestaurante/Edit/{id}", new() { ["status"] = "Confirmada" });
        Assert.Equal(HttpStatusCode.Redirect, conf.StatusCode);
        Assert.Equal("Confirmada", app.Services.GetRequiredService<IReservaRepository>().Read(id)!.Status);

        var pend = await TesteApoio.Post(func, $"/AreaRestaurante/Edit/{id}", $"/AreaRestaurante/Edit/{id}", new() { ["status"] = "Pendente" });
        Assert.Equal(HttpStatusCode.Redirect, pend.StatusCode);
        Assert.Equal("Pendente", app.Services.GetRequiredService<IReservaRepository>().Read(id)!.Status);

        var canc = await TesteApoio.Post(func, $"/AreaRestaurante/Edit/{id}", $"/AreaRestaurante/Edit/{id}", new() { ["status"] = "Cancelada" });
        Assert.Equal(HttpStatusCode.Redirect, canc.StatusCode);
        Assert.Equal("Cancelada", app.Services.GetRequiredService<IReservaRepository>().Read(id)!.Status);

        var getCancelada = await func.GetAsync($"/AreaRestaurante/Edit/{id}");
        Assert.Equal(HttpStatusCode.BadRequest, getCancelada.StatusCode);
        var novamente = await func.PostAsync($"/AreaRestaurante/Edit/{id}", new FormUrlEncodedContent(new Dictionary<string, string> { ["status"] = "Confirmada" }));
        Assert.Equal(HttpStatusCode.BadRequest, novamente.StatusCode);
    }

    [Fact]
    public async Task RestauranteListaPendentePrimeiroEFiltraNomeStatusLimpar()
    {
        using var app = new AplicacaoTeste();
        using var seed = app.Cliente();
        await PrepararReservasParaFiltro(app, seed);
        using var func = app.Cliente();
        await TesteApoio.LoginFuncionarioTeste(func, app.Services);

        var lista = await func.GetStringAsync("/AreaRestaurante/Reservas");
        lista = WebUtility.HtmlDecode(lista);
        var iPend = lista.IndexOf("João Mendes", StringComparison.OrdinalIgnoreCase);
        var iConf = lista.IndexOf("Maria Oliveira", StringComparison.OrdinalIgnoreCase);
        Assert.True(iPend >= 0 && iConf >= 0, lista);
        Assert.True(iPend < iConf, "Pendente deve aparecer antes de Confirmada.");

        var porAcento = WebUtility.HtmlDecode(await func.GetStringAsync("/AreaRestaurante/Reservas?nome=" + Uri.EscapeDataString("joao")));
        Assert.Contains("João Mendes", porAcento);
        Assert.DoesNotContain("Maria Oliveira", porAcento);

        var porMaiusculas = WebUtility.HtmlDecode(await func.GetStringAsync("/AreaRestaurante/Reservas?nome=" + Uri.EscapeDataString("JOÃO")));
        Assert.Contains("João Mendes", porMaiusculas);

        var porStatus = WebUtility.HtmlDecode(await func.GetStringAsync("/AreaRestaurante/Reservas?status=Confirmada"));
        Assert.Contains("Maria Oliveira", porStatus);
        Assert.DoesNotContain("João Mendes", porStatus);

        var combo = WebUtility.HtmlDecode(await func.GetStringAsync("/AreaRestaurante/Reservas?nome=" + Uri.EscapeDataString("maria") + "&status=Confirmada"));
        Assert.Contains("Maria Oliveira", combo);
        Assert.DoesNotContain("João Mendes", combo);

        var comboVazio = WebUtility.HtmlDecode(await func.GetStringAsync("/AreaRestaurante/Reservas?nome=" + Uri.EscapeDataString("maria") + "&status=Pendente"));
        Assert.DoesNotContain("Maria Oliveira", comboVazio);
        Assert.DoesNotContain("João Mendes", comboVazio);

        var limpar = await func.GetAsync("/AreaRestaurante/Reservas");
        Assert.Equal(HttpStatusCode.OK, limpar.StatusCode);
        var htmlLimpar = WebUtility.HtmlDecode(await limpar.Content.ReadAsStringAsync());
        Assert.Contains("João Mendes", htmlLimpar);
        Assert.Contains("Maria Oliveira", htmlLimpar);
        Assert.DoesNotContain("nome=joao", htmlLimpar, StringComparison.OrdinalIgnoreCase);
    }
}

public class BateriaCrudTests
{
    [Fact]
    public async Task CrudBasicoAmbienteMesaFuncionario()
    {
        using var app = new AplicacaoTeste();
        using var client = app.Cliente();
        await TesteApoio.LoginFuncionarioTeste(client, app.Services);

        var amb = await TesteApoio.Post(client, "/Ambiente/Create", "/Ambiente/Create", new()
        {
            ["Nome"] = "Externo",
            ["Capacidade"] = "20",
            ["Status"] = "Disponivel"
        });
        Assert.Equal(HttpStatusCode.Redirect, amb.StatusCode);
        Assert.Contains(app.Services.GetRequiredService<IAmbienteRepository>().Read(), a => a.Nome == "Externo");

        var mesa = await TesteApoio.Post(client, "/Mesa/Create", "/Mesa/Create", new()
        {
            ["Numero"] = "10",
            ["AmbienteId"] = "1",
            ["QtdLugares"] = "4",
            ["Status"] = "Livre"
        });
        Assert.Equal(HttpStatusCode.Redirect, mesa.StatusCode);
        Assert.Contains(app.Services.GetRequiredService<IMesaRepository>().Read(), m => m.Numero == 10);

        var funcCreate = await TesteApoio.Post(client, "/Funcionario/Create", "/Funcionario/Create", new()
        {
            ["Nome"] = "Novo Func",
            ["Cpf"] = "555.666.777-88",
            ["EmailInstitucional"] = "novo@santamassa.com.br",
            ["Senha"] = "Func@123"
        });
        Assert.Equal(HttpStatusCode.Redirect, funcCreate.StatusCode);
        var novo = app.Services.GetRequiredService<IFuncionarioRepository>().Read().First(f => f.EmailInstitucional == "novo@santamassa.com.br");

        var funcEditOk = await TesteApoio.Post(client, $"/Funcionario/Edit/{novo.Id}", $"/Funcionario/Edit/{novo.Id}", new()
        {
            ["Nome"] = "Novo Func Editado",
            ["Cpf"] = "555.666.777-88",
            ["EmailInstitucional"] = "novo.edit@santamassa.com.br"
        });
        Assert.Equal(HttpStatusCode.Redirect, funcEditOk.StatusCode);

        var funcEditRuim = await TesteApoio.Post(client, $"/Funcionario/Edit/{novo.Id}", $"/Funcionario/Edit/{novo.Id}", new()
        {
            ["Nome"] = "Novo Func Editado",
            ["Cpf"] = "555.666.777-88",
            ["EmailInstitucional"] = "novo@gmail.com"
        });
        Assert.Equal(HttpStatusCode.OK, funcEditRuim.StatusCode);
        Assert.Contains("santamassa", (await TesteApoio.Html(funcEditRuim)).ToLowerInvariant());
        Assert.Equal("novo.edit@santamassa.com.br", app.Services.GetRequiredService<IFuncionarioRepository>().Read(novo.Id)!.EmailInstitucional);
    }

    [Fact]
    public async Task FuncionarioCreateExigeEmailSantamassa()
    {
        using var app = new AplicacaoTeste();
        using var client = app.Cliente();
        await TesteApoio.LoginFuncionarioTeste(client, app.Services);
        var resp = await TesteApoio.Post(client, "/Funcionario/Create", "/Funcionario/Create", new()
        {
            ["Nome"] = "Fora",
            ["Cpf"] = "555.666.777-00",
            ["EmailInstitucional"] = "fora@gmail.com",
            ["Senha"] = "Func@123"
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.DoesNotContain(app.Services.GetRequiredService<IFuncionarioRepository>().Read(), f => f.EmailInstitucional == "fora@gmail.com");
    }

    [Fact]
    public async Task ExcluirMesaComReservaMesmoCanceladaEBloqueado()
    {
        using var app = new AplicacaoTeste();
        TesteApoio.PrepararMesaTeste(app.Services);
        using var client = app.Cliente();
        await TesteApoio.LoginClienteTeste(client, app.Services);
        await TesteApoio.PostReservaComQtd(client, "2027-01-28", 2, 1);
        await TesteApoio.Post(client, "/Reserva/Delete/1", "/Reserva/Delete/1", new() { ["Id"] = "1" });
        Assert.Equal("Cancelada", app.Services.GetRequiredService<IReservaRepository>().Read(1)!.Status);
        await TesteApoio.LoginFuncionarioTeste(client, app.Services);
        var resp = await TesteApoio.Post(client, "/Mesa/Delete/1", "/Mesa/Delete/1", new() { ["Id"] = "1" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.NotNull(app.Services.GetRequiredService<IMesaRepository>().Read(1));
        Assert.Contains("reservas vinculadas", await TesteApoio.Html(resp));
    }

    [Fact]
    public async Task ExcluirMesaSemReservaFunciona()
    {
        using var app = new AplicacaoTeste();
        TesteApoio.PrepararMesaTeste(app.Services);
        using var client = app.Cliente();
        await TesteApoio.LoginFuncionarioTeste(client, app.Services);
        var resp = await TesteApoio.Post(client, "/Mesa/Delete/1", "/Mesa/Delete/1", new() { ["Id"] = "1" });
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Null(app.Services.GetRequiredService<IMesaRepository>().Read(1));
    }

    [Fact]
    public async Task ExcluirClienteComReservasEBloqueado()
    {
        using var app = new AplicacaoTeste();
        TesteApoio.PrepararMesaTeste(app.Services);
        using var client = app.Cliente();
        await TesteApoio.LoginClienteTeste(client, app.Services);
        Assert.Equal(HttpStatusCode.Redirect, (await TesteApoio.PostReservaComQtd(client, "2027-01-29", 2, 1)).StatusCode);
        await TesteApoio.LoginFuncionarioTeste(client, app.Services);
        var id = app.Services.GetRequiredService<IClienteRepository>().Read().First().Id;
        var resp = await TesteApoio.Post(client, $"/Cliente/Delete/{id}", $"/Cliente/Delete/{id}", new() { ["Id"] = id.ToString() });
        Assert.True(resp.StatusCode == HttpStatusCode.OK || resp.StatusCode == HttpStatusCode.Redirect, resp.StatusCode.ToString());
        Assert.NotNull(app.Services.GetRequiredService<IClienteRepository>().Read(id));
        Assert.NotEmpty(app.Services.GetRequiredService<IReservaRepository>().Read());
    }

    [Fact]
    public async Task MapaPorAmbienteExternoInternoESacada()
    {
        using var app = new AplicacaoTeste();
        TesteApoio.PrepararMesaTeste(app.Services); // Interno, mesa 1
        TesteApoio.PrepararAmbienteEMesas(app.Services, "Externo", 4, 2, 21);
        TesteApoio.PrepararAmbienteEMesas(app.Services, "Sacada", 2, 2, 33);
        app.Services.GetRequiredService<IAmbienteRepository>().Create(new Ambiente { Nome = "Salão Interno", Capacidade = 8, Status = "Disponivel" });
        var salaId = app.Services.GetRequiredService<IAmbienteRepository>().Read().First(a => a.Nome.Contains("Salão") || a.Nome.Contains("Salao")).Id;
        app.Services.GetRequiredService<IMesaRepository>().Create(new Mesa { Numero = 50, AmbienteId = salaId, QtdLugares = 4, Status = "Livre", AmbienteNome = "Salão Interno" });

        using var client = app.Cliente();
        await TesteApoio.LoginClienteTeste(client, app.Services);
        var data = "2027-01-20";
        var interno = app.Services.GetRequiredService<IAmbienteRepository>().Read().First(a => a.Nome == "Interno").Id;
        var externo = app.Services.GetRequiredService<IAmbienteRepository>().Read().First(a => a.Nome == "Externo").Id;
        var sacada = app.Services.GetRequiredService<IAmbienteRepository>().Read().First(a => a.Nome == "Sacada").Id;

        var mapInt = await client.GetStringAsync($"/Reserva/MesasDisponiveis?dataHora={data}&ambienteId={interno}");
        Assert.Contains("\"numero\":1", mapInt.Replace(" ", "").ToLowerInvariant().Replace("numero", "numero") );
        // A API pode devolver HTML/JSON; garante presença da mesa do ambiente e ausência das outras.
        Assert.True(mapInt.Contains("1"), mapInt);
        Assert.DoesNotContain("21", mapInt);
        Assert.DoesNotContain("33", mapInt);

        var mapExt = await client.GetStringAsync($"/Reserva/MesasDisponiveis?dataHora={data}&ambienteId={externo}");
        Assert.Contains("21", mapExt);
        Assert.DoesNotContain("\"Numero\":1", mapExt.Replace(" ", ""));

        var mapSac = await client.GetStringAsync($"/Reserva/MesasDisponiveis?dataHora={data}&ambienteId={sacada}");
        Assert.Contains("33", mapSac);

        var mapSalao = await client.GetStringAsync($"/Reserva/MesasDisponiveis?dataHora={data}&ambienteId={salaId}");
        Assert.Contains("50", mapSalao);
    }

    [Fact]
    public async Task MesasDisponiveisDataPassadaVaziaEOcupadasNoDia()
    {
        using var app = new AplicacaoTeste();
        TesteApoio.PrepararMesaTeste(app.Services);
        using var client = app.Cliente();
        await TesteApoio.LoginClienteTeste(client, app.Services);
        var passada = await client.GetStringAsync("/Reserva/MesasDisponiveis?dataHora=" + DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd"));
        Assert.True(passada == "[]" || passada.Contains("[]") || !passada.Contains("Livre"), passada);

        Assert.Equal(HttpStatusCode.Redirect, (await TesteApoio.PostReservaComQtd(client, "2027-01-21", 2, 1)).StatusCode);
        var noDia = await client.GetStringAsync("/Reserva/MesasDisponiveis?dataHora=2027-01-21");
        Assert.Contains("Reservada", noDia);
        var outro = await client.GetStringAsync("/Reserva/MesasDisponiveis?dataHora=2027-01-22");
        Assert.Contains("Livre", outro);
        Assert.DoesNotContain("Reservada", outro);
    }
}
