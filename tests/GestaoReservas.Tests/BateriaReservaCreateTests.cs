using System.Net;
using GestaoReservas.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GestaoReservas.Tests;

public class BateriaReservaCreateTests
{
    [Fact]
    public async Task CreateRecusaDataPassada()
    {
        using var app = new AplicacaoTeste();
        TesteApoio.PrepararMesaTeste(app.Services);
        using var client = app.Cliente();
        await TesteApoio.LoginClienteTeste(client, app.Services);
        var resp = await TesteApoio.PostReservaComQtd(client, DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd"), 2, 1);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Empty(app.Services.GetRequiredService<IReservaRepository>().Read());
        Assert.Contains("não pode ser anterior", await TesteApoio.Html(resp));
    }

    [Fact]
    public async Task CreateAceitaDataDeHoje()
    {
        using var app = new AplicacaoTeste();
        TesteApoio.PrepararMesaTeste(app.Services);
        using var client = app.Cliente();
        await TesteApoio.LoginClienteTeste(client, app.Services);
        // Hoje (9/11/2026) é segunda; se o teste rodar num domingo, a regra de domingo prevalece.
        if (DateTime.Today.DayOfWeek == DayOfWeek.Sunday)
        {
            var recusa = await TesteApoio.PostReservaComQtd(client, TesteApoio.HojeIso(), 2, 1);
            Assert.Equal(HttpStatusCode.OK, recusa.StatusCode);
            Assert.Contains("domingo", (await TesteApoio.Html(recusa)).ToLowerInvariant());
            return;
        }
        var resp = await TesteApoio.PostReservaComQtd(client, TesteApoio.HojeIso(), 2, 1);
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        var reserva = Assert.Single(app.Services.GetRequiredService<IReservaRepository>().Read());
        Assert.Equal(DateTime.Today.Date, reserva.DataHora.Date);
        Assert.Equal("Pendente", reserva.Status);
    }

    [Fact]
    public async Task CreateRecusaDomingo()
    {
        using var app = new AplicacaoTeste();
        TesteApoio.PrepararMesaTeste(app.Services);
        using var client = app.Cliente();
        await TesteApoio.LoginClienteTeste(client, app.Services);
        var resp = await TesteApoio.PostReservaComQtd(client, TesteApoio.ProximoDomingoIso(), 2, 1);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Contains("domingos", await TesteApoio.Html(resp));
        Assert.Empty(app.Services.GetRequiredService<IReservaRepository>().Read());
    }

    [Fact]
    public async Task CreateRecusaMaisDe90Dias()
    {
        using var app = new AplicacaoTeste();
        TesteApoio.PrepararMesaTeste(app.Services);
        using var client = app.Cliente();
        await TesteApoio.LoginClienteTeste(client, app.Services);
        var resp = await TesteApoio.PostReservaComQtd(client, TesteApoio.DiaDaquiA(91), 2, 1);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Contains("90 dias", await TesteApoio.Html(resp));
        Assert.Empty(app.Services.GetRequiredService<IReservaRepository>().Read());
    }

    [Fact]
    public async Task CreateAceitaLimiteExatoDe90DiasQuandoNaoEDomingo()
    {
        using var app = new AplicacaoTeste();
        TesteApoio.PrepararMesaTeste(app.Services);
        using var client = app.Cliente();
        await TesteApoio.LoginClienteTeste(client, app.Services);
        var dia90 = DateTime.Today.AddDays(90);
        if (dia90.DayOfWeek == DayOfWeek.Sunday)
        {
            // Neste calendário o 90º dia cai no domingo: a recusa deve ser por domingo, não por prazo.
            var recusa = await TesteApoio.PostReservaComQtd(client, dia90.ToString("yyyy-MM-dd"), 2, 1);
            Assert.Equal(HttpStatusCode.OK, recusa.StatusCode);
            var html = await TesteApoio.Html(recusa);
            Assert.Contains("domingo", html.ToLowerInvariant());
            Assert.Empty(app.Services.GetRequiredService<IReservaRepository>().Read());
            // O dia 89 (sábado, neste calendário) ainda está dentro da janela e deve ser aceito.
            var ok89 = await TesteApoio.PostReservaComQtd(client, TesteApoio.DiaDaquiA(89), 2, 1);
            Assert.Equal(HttpStatusCode.Redirect, ok89.StatusCode);
            return;
        }
        var resp = await TesteApoio.PostReservaComQtd(client, dia90.ToString("yyyy-MM-dd"), 2, 1);
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Single(app.Services.GetRequiredService<IReservaRepository>().Read());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public async Task CreateRecusaQuantidadeForaDoIntervalo(int qtd)
    {
        using var app = new AplicacaoTeste();
        TesteApoio.PrepararMesaTeste(app.Services);
        TesteApoio.PrepararMaisMesas(app.Services, 10);
        using var client = app.Cliente();
        await TesteApoio.LoginClienteTeste(client, app.Services);
        var resp = await TesteApoio.PostReservaComQtd(client, "2027-01-13", qtd, 1);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Empty(app.Services.GetRequiredService<IReservaRepository>().Read());
        var html = await TesteApoio.Html(resp);
        Assert.True(html.Contains("1 e 30") || html.Contains("30 pessoas") || html.Contains("pessoas"), html);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    public async Task CreateAceitaQuantidadeLimite(int qtd)
    {
        using var app = new AplicacaoTeste();
        TesteApoio.PrepararMesaTeste(app.Services);
        TesteApoio.PrepararMaisMesas(app.Services, 8);
        using var client = app.Cliente();
        await TesteApoio.LoginClienteTeste(client, app.Services);
        var resp = await TesteApoio.PostReservaComQtd(client, "2027-01-14", qtd, 1);
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        var reserva = Assert.Single(app.Services.GetRequiredService<IReservaRepository>().Read());
        Assert.Equal(qtd, reserva.QtdPessoas);
        Assert.Equal("Pendente", reserva.Status);
        if (qtd == 30)
            Assert.True(reserva.MesaIds.Count >= 8, "30 pessoas em mesas de 4 exigem junção automática.");
    }

    [Fact]
    public async Task CreateRecusaMesaOcupadaNaMesmaData()
    {
        using var app = new AplicacaoTeste();
        TesteApoio.PrepararMesaTeste(app.Services);
        using var client = app.Cliente();
        await TesteApoio.LoginClienteTeste(client, app.Services);
        Assert.Equal(HttpStatusCode.Redirect, (await TesteApoio.PostReservaComQtd(client, "2027-01-15", 2, 1)).StatusCode);

        using var outro = app.Cliente();
        await TesteApoio.LoginClienteTeste(outro, app.Services, cpf: "98765432100");
        var resp = await TesteApoio.PostReservaComQtd(outro, "2027-01-15", 2, 1);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Single(app.Services.GetRequiredService<IReservaRepository>().Read());
        var html = await TesteApoio.Html(resp);
        Assert.True(html.Contains("não está livre") || html.Contains("não est� livre") || html.Contains("livre"), html);
    }

    [Fact]
    public async Task MesaReservadaPermaneceLivreEmOutroDia()
    {
        using var app = new AplicacaoTeste();
        TesteApoio.PrepararMesaTeste(app.Services);
        using var client = app.Cliente();
        await TesteApoio.LoginClienteTeste(client, app.Services);
        Assert.Equal(HttpStatusCode.Redirect, (await TesteApoio.PostReservaComQtd(client, "2027-01-15", 2, 1)).StatusCode);
        var noDia = await client.GetStringAsync("/Reserva/MesasDisponiveis?dataHora=2027-01-15");
        Assert.Contains("Reservada", noDia);
        var outroDia = await client.GetStringAsync("/Reserva/MesasDisponiveis?dataHora=2027-01-16");
        Assert.Contains("Livre", outroDia);
        Assert.DoesNotContain("Reservada", outroDia);

        using var outro = app.Cliente();
        await TesteApoio.LoginClienteTeste(outro, app.Services, cpf: "98765432100");
        var resp = await TesteApoio.PostReservaComQtd(outro, "2027-01-16", 2, 1);
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Equal(2, app.Services.GetRequiredService<IReservaRepository>().Read().Count);
    }

    [Fact]
    public async Task StatusOcupadaBloqueiaMesaEmQualquerData()
    {
        using var app = new AplicacaoTeste();
        TesteApoio.PrepararMesaTeste(app.Services);
        var mesa = app.Services.GetRequiredService<IMesaRepository>().Read(1)!;
        mesa.Status = "Ocupada";
        app.Services.GetRequiredService<IMesaRepository>().Update(mesa);
        using var client = app.Cliente();
        await TesteApoio.LoginClienteTeste(client, app.Services);
        var resp = await TesteApoio.PostReservaComQtd(client, "2027-01-20", 2, 1);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Empty(app.Services.GetRequiredService<IReservaRepository>().Read());
        var mapa = await client.GetStringAsync("/Reserva/MesasDisponiveis?dataHora=2027-01-20");
        Assert.True(mapa.Contains("Ocupada") || mapa.Contains("Indispon"), mapa);
        var outroDia = await client.GetStringAsync("/Reserva/MesasDisponiveis?dataHora=2027-01-22");
        Assert.True(outroDia.Contains("Ocupada") || outroDia.Contains("Indispon") || !outroDia.Contains("Livre"), outroDia);
    }

    [Fact]
    public async Task StatusInicialDaReservaEPendenteMesmoComConfirmadaNoFormulario()
    {
        using var app = new AplicacaoTeste();
        TesteApoio.PrepararMesaTeste(app.Services);
        using var client = app.Cliente();
        await TesteApoio.LoginClienteTeste(client, app.Services);
        var campos = TesteApoio.DadosReserva("2027-01-21");
        campos["Status"] = "Confirmada";
        var resp = await TesteApoio.Post(client, "/Reserva/Create", "/Reserva/Create", campos);
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Equal("Pendente", Assert.Single(app.Services.GetRequiredService<IReservaRepository>().Read()).Status);
    }

    [Fact]
    public async Task SegundaReservaAtivaNoMesmoDiaERecusada()
    {
        using var app = new AplicacaoTeste();
        TesteApoio.PrepararMesaTeste(app.Services);
        TesteApoio.PrepararMaisMesas(app.Services, 2);
        using var client = app.Cliente();
        await TesteApoio.LoginClienteTeste(client, app.Services);
        Assert.Equal(HttpStatusCode.Redirect, (await TesteApoio.PostReservaComQtd(client, "2027-01-22", 2, 1)).StatusCode);
        var resp = await TesteApoio.PostReservaComQtd(client, "2027-01-22", 2, 2);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Contains("já possui uma reserva ativa", await TesteApoio.Html(resp));
        Assert.Single(app.Services.GetRequiredService<IReservaRepository>().Read());
    }

    [Fact]
    public async Task ReservaCanceladaNoMesmoDiaPermiteNova()
    {
        using var app = new AplicacaoTeste();
        TesteApoio.PrepararMesaTeste(app.Services);
        TesteApoio.PrepararMaisMesas(app.Services, 2);
        using var client = app.Cliente();
        await TesteApoio.LoginClienteTeste(client, app.Services);
        Assert.Equal(HttpStatusCode.Redirect, (await TesteApoio.PostReservaComQtd(client, "2027-01-22", 2, 1)).StatusCode);
        var cancelar = await TesteApoio.Post(client, "/Reserva/Delete/1", "/Reserva/Delete/1", new() { ["Id"] = "1" });
        Assert.Equal(HttpStatusCode.Redirect, cancelar.StatusCode);
        Assert.Equal("Cancelada", app.Services.GetRequiredService<IReservaRepository>().Read(1)!.Status);
        var nova = await TesteApoio.PostReservaComQtd(client, "2027-01-22", 2, 2);
        Assert.Equal(HttpStatusCode.Redirect, nova.StatusCode);
        Assert.Equal(2, app.Services.GetRequiredService<IReservaRepository>().Read().Count);
        Assert.Contains(app.Services.GetRequiredService<IReservaRepository>().Read(), r => r.Status == "Pendente");
    }

    [Fact]
    public async Task CreateJuntaAutomaticamente4PessoasEmMesaDe2()
    {
        using var app = new AplicacaoTeste();
        app.Services.GetRequiredService<IAmbienteRepository>().Create(new Ambiente { Nome = "Interno", Capacidade = 20, Status = "Disponivel" });
        var mesas = app.Services.GetRequiredService<IMesaRepository>();
        mesas.Create(new Mesa { Numero = 1, AmbienteId = 1, QtdLugares = 2, Status = "Livre", AmbienteNome = "Interno" });
        mesas.Create(new Mesa { Numero = 2, AmbienteId = 1, QtdLugares = 2, Status = "Livre", AmbienteNome = "Interno" });
        using var client = app.Cliente();
        await TesteApoio.LoginClienteTeste(client, app.Services);
        var resp = await TesteApoio.PostReservaComQtd(client, "2027-02-02", 4, 1);
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        var reserva = Assert.Single(app.Services.GetRequiredService<IReservaRepository>().Read());
        Assert.Equal(2, reserva.MesaIds.Count);
        Assert.Contains(1, reserva.MesaIds);
        Assert.Contains(2, reserva.MesaIds);
    }

    [Fact]
    public async Task CreateSemMesasSuficientesNoAmbienteDevolveErro()
    {
        using var app = new AplicacaoTeste();
        app.Services.GetRequiredService<IAmbienteRepository>().Create(new Ambiente { Nome = "Interno", Capacidade = 4, Status = "Disponivel" });
        app.Services.GetRequiredService<IMesaRepository>().Create(new Mesa { Numero = 1, AmbienteId = 1, QtdLugares = 2, Status = "Livre", AmbienteNome = "Interno" });
        using var client = app.Cliente();
        await TesteApoio.LoginClienteTeste(client, app.Services);
        var resp = await TesteApoio.PostReservaComQtd(client, "2027-02-03", 7, 1);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Empty(app.Services.GetRequiredService<IReservaRepository>().Read());
        var html = await TesteApoio.Html(resp);
        Assert.True(html.Contains("mesa", StringComparison.OrdinalIgnoreCase) || html.Contains("lugar", StringComparison.OrdinalIgnoreCase) || html.Contains("suficiente", StringComparison.OrdinalIgnoreCase), html);
    }

    [Fact]
    public async Task CreateNaoJuntaMesaDeOutroAmbiente()
    {
        using var app = new AplicacaoTeste();
        app.Services.GetRequiredService<IAmbienteRepository>().Create(new Ambiente { Nome = "Interno", Capacidade = 4, Status = "Disponivel" });
        app.Services.GetRequiredService<IAmbienteRepository>().Create(new Ambiente { Nome = "Externo", Capacidade = 8, Status = "Disponivel" });
        var mesas = app.Services.GetRequiredService<IMesaRepository>();
        mesas.Create(new Mesa { Numero = 1, AmbienteId = 1, QtdLugares = 2, Status = "Livre", AmbienteNome = "Interno" });
        mesas.Create(new Mesa { Numero = 2, AmbienteId = 2, QtdLugares = 4, Status = "Livre", AmbienteNome = "Externo" });
        using var client = app.Cliente();
        await TesteApoio.LoginClienteTeste(client, app.Services);
        var resp = await TesteApoio.PostReservaComQtd(client, "2027-02-04", 4, 1);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Empty(app.Services.GetRequiredService<IReservaRepository>().Read());
    }

    [Fact]
    public async Task CreateRecusaAmbientesMisturados()
    {
        using var app = new AplicacaoTeste();
        app.Services.GetRequiredService<IAmbienteRepository>().Create(new Ambiente { Nome = "Interno", Capacidade = 8, Status = "Disponivel" });
        app.Services.GetRequiredService<IAmbienteRepository>().Create(new Ambiente { Nome = "Externo", Capacidade = 8, Status = "Disponivel" });
        var mesas = app.Services.GetRequiredService<IMesaRepository>();
        mesas.Create(new Mesa { Numero = 1, AmbienteId = 1, QtdLugares = 4, Status = "Livre", AmbienteNome = "Interno" });
        mesas.Create(new Mesa { Numero = 2, AmbienteId = 2, QtdLugares = 4, Status = "Livre", AmbienteNome = "Externo" });
        using var client = app.Cliente();
        await TesteApoio.LoginClienteTeste(client, app.Services);
        var resp = await TesteApoio.PostReservaComQtd(client, "2027-02-05", 4, 1, 2);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Empty(app.Services.GetRequiredService<IReservaRepository>().Read());
        var html = await TesteApoio.Html(resp);
        Assert.True(html.Contains("ambiente", StringComparison.OrdinalIgnoreCase), html);
    }

    [Fact]
    public async Task CreateRecusaMesaExtraDesnecessaria()
    {
        using var app = new AplicacaoTeste();
        TesteApoio.PrepararMesaTeste(app.Services);
        TesteApoio.PrepararMaisMesas(app.Services, 2);
        using var client = app.Cliente();
        await TesteApoio.LoginClienteTeste(client, app.Services);
        var resp = await TesteApoio.PostReservaComQtd(client, "2027-02-06", 2, 1, 2);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Empty(app.Services.GetRequiredService<IReservaRepository>().Read());
        var html = await TesteApoio.Html(resp);
        Assert.True(html.Contains("extra") || html.Contains("desnecess") || html.Contains("mais mesas") || html.Contains("mesa"), html);
    }
}
