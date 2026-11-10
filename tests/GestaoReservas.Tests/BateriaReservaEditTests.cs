using System.Net;
using GestaoReservas.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GestaoReservas.Tests;

public class BateriaReservaEditTests
{
    private static async Task<(AplicacaoTeste app, HttpClient client, Reserva reserva)> CriarReservaAtiva(
        string data = "2027-01-18", int qtd = 2, int lugares = 4, int mesasExtra = 3)
    {
        var app = new AplicacaoTeste();
        TesteApoio.PrepararMesaTeste(app.Services);
        if (mesasExtra > 1)
            TesteApoio.PrepararMaisMesas(app.Services, mesasExtra);
        var client = app.Cliente();
        await TesteApoio.LoginClienteTeste(client, app.Services);
        var resp = await TesteApoio.PostReservaComQtd(client, data, qtd, 1);
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        var reserva = Assert.Single(app.Services.GetRequiredService<IReservaRepository>().Read());
        return (app, client, reserva);
    }

    [Fact]
    public async Task EditClienteNaoAlteraStatusNemRemoveMesas()
    {
        var (app, client, reserva) = await CriarReservaAtiva();
        using (app)
        using (client)
        {
            var statusAntes = reserva.Status;
            var mesasAntes = reserva.MesaIds.ToList();
            var resp = await TesteApoio.PostEdicaoReserva(client, reserva.Id, "2027-01-19", reserva.QtdPessoas, new Dictionary<string, string>
            {
                ["Status"] = "Confirmada",
                ["MesaIds"] = "999"
            });
            Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
            var atual = app.Services.GetRequiredService<IReservaRepository>().Read(reserva.Id)!;
            Assert.Equal(statusAntes, atual.Status);
            Assert.Equal("Pendente", atual.Status);
            Assert.Equal(mesasAntes, atual.MesaIds);
        }
    }

    [Fact]
    public async Task EditClienteNaoDiminuiQuantidadeDePessoas()
    {
        var (app, client, reserva) = await CriarReservaAtiva(qtd: 4);
        using (app)
        using (client)
        {
            var resp = await TesteApoio.PostEdicaoReserva(client, reserva.Id, reserva.DataHora.ToString("yyyy-MM-dd"), 2);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            Assert.Contains("diminuir", await TesteApoio.Html(resp));
            Assert.Equal(4, app.Services.GetRequiredService<IReservaRepository>().Read(reserva.Id)!.QtdPessoas);
        }
    }

    [Fact]
    public async Task EditAumentarPessoasJuntaMesasSemRemoverAsOriginais()
    {
        var (app, client, reserva) = await CriarReservaAtiva(qtd: 2, mesasExtra: 3);
        using (app)
        using (client)
        {
            var mesasAntes = reserva.MesaIds.ToList();
            var resp = await TesteApoio.PostEdicaoReserva(client, reserva.Id, reserva.DataHora.ToString("yyyy-MM-dd"), 8);
            Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
            var atual = app.Services.GetRequiredService<IReservaRepository>().Read(reserva.Id)!;
            Assert.Equal(8, atual.QtdPessoas);
            foreach (var id in mesasAntes)
                Assert.Contains(id, atual.MesaIds);
            Assert.True(atual.MesaIds.Count > mesasAntes.Count, "Deveria juntar mesas extras sem remover as originais.");
        }
    }

    [Fact]
    public async Task EditRecusaTrocaParaDataPassadaDomingoOuAlemDe90()
    {
        var (app, client, reserva) = await CriarReservaAtiva();
        using (app)
        using (client)
        {
            var passada = await TesteApoio.PostEdicaoReserva(client, reserva.Id, DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd"), reserva.QtdPessoas);
            Assert.Equal(HttpStatusCode.OK, passada.StatusCode);
            Assert.Contains("não pode ser anterior", await TesteApoio.Html(passada));

            var domingo = await TesteApoio.PostEdicaoReserva(client, reserva.Id, TesteApoio.ProximoDomingoIso(), reserva.QtdPessoas);
            Assert.Equal(HttpStatusCode.OK, domingo.StatusCode);
            Assert.Contains("domingo", (await TesteApoio.Html(domingo)).ToLowerInvariant());

            var longe = await TesteApoio.PostEdicaoReserva(client, reserva.Id, TesteApoio.DiaDaquiA(91), reserva.QtdPessoas);
            Assert.Equal(HttpStatusCode.OK, longe.StatusCode);
            Assert.Contains("90 dias", await TesteApoio.Html(longe));

            var atual = app.Services.GetRequiredService<IReservaRepository>().Read(reserva.Id)!;
            Assert.Equal(reserva.DataHora.Date, atual.DataHora.Date);
        }
    }

    [Fact]
    public async Task EditReservaAntigaSemMudarDataPermaneceOk()
    {
        using var app = new AplicacaoTeste();
        TesteApoio.PrepararMesaTeste(app.Services);
        TesteApoio.PrepararMaisMesas(app.Services, 2);
        using var client = app.Cliente();
        await TesteApoio.LoginClienteTeste(client, app.Services);
        // Insere direto no repositório uma reserva já no passado (cria como se fosse antiga).
        var repo = app.Services.GetRequiredService<IReservaRepository>();
        var cliente = app.Services.GetRequiredService<IClienteRepository>().Read().First();
        repo.Create(new Reserva
        {
            ClienteId = cliente.Id,
            Cliente = cliente,
            MesaIds = new List<int> { 1 },
            DataHora = DateTime.Today.AddDays(-10),
            QtdPessoas = 2,
            Status = "Confirmada"
        });
        // O Create em memoria inicia em Pendente (igual ao fluxo normal); o status confirmado e aplicado em seguida.
        repo.UpdateStatus(repo.Read().Single().Id, "Confirmada");
        var reserva = repo.Read().Single();
        var resp = await TesteApoio.PostEdicaoReserva(client, reserva.Id, reserva.DataHora.ToString("yyyy-MM-dd"), 2);
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        var atual = repo.Read(reserva.Id)!;
        Assert.Equal(reserva.DataHora.Date, atual.DataHora.Date);
        Assert.Equal("Confirmada", atual.Status);
        Assert.Equal(2, atual.QtdPessoas);
    }

    [Fact]
    public async Task CancelarReservaFunciona()
    {
        var (app, client, reserva) = await CriarReservaAtiva();
        using (app)
        using (client)
        {
            var resp = await TesteApoio.Post(client, $"/Reserva/Delete/{reserva.Id}", $"/Reserva/Delete/{reserva.Id}", new() { ["Id"] = reserva.Id.ToString() });
            Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
            Assert.Equal("Cancelada", app.Services.GetRequiredService<IReservaRepository>().Read(reserva.Id)!.Status);
        }
    }

    [Fact]
    public async Task MinhasReservasSeparaProximasEHistoricoEOrdena()
    {
        using var app = new AplicacaoTeste();
        TesteApoio.PrepararMesaTeste(app.Services);
        TesteApoio.PrepararMaisMesas(app.Services, 4);
        using var client = app.Cliente();
        await TesteApoio.LoginClienteTeste(client, app.Services);
        var repo = app.Services.GetRequiredService<IReservaRepository>();
        var cli = app.Services.GetRequiredService<IClienteRepository>().Read().First();
        repo.Create(new Reserva { ClienteId = cli.Id, Cliente = cli, MesaIds = new List<int> { 1 }, DataHora = DateTime.Today.AddDays(-5), QtdPessoas = 2, Status = "Confirmada" });
        repo.Create(new Reserva { ClienteId = cli.Id, Cliente = cli, MesaIds = new List<int> { 2 }, DataHora = DateTime.Today.AddDays(-2), QtdPessoas = 2, Status = "Cancelada" });
        repo.Create(new Reserva { ClienteId = cli.Id, Cliente = cli, MesaIds = new List<int> { 3 }, DataHora = DateTime.Today.AddDays(3), QtdPessoas = 2, Status = "Pendente" });
        repo.Create(new Reserva { ClienteId = cli.Id, Cliente = cli, MesaIds = new List<int> { 4 }, DataHora = DateTime.Today.AddDays(10), QtdPessoas = 2, Status = "Pendente" });

        var padrao = await client.GetStringAsync("/Reserva");
        Assert.Contains(DateTime.Today.AddDays(3).ToString("yyyy-MM-dd") is var d3 ? DateTime.Today.AddDays(3).ToString("dd/MM/yyyy") : "", padrao);
        // Próximas: datas futuras presentes, passadas ausentes.
        Assert.Contains(DateTime.Today.AddDays(3).ToString("dd/MM/yyyy"), padrao);
        Assert.Contains(DateTime.Today.AddDays(10).ToString("dd/MM/yyyy"), padrao);
        Assert.DoesNotContain(DateTime.Today.AddDays(-5).ToString("dd/MM/yyyy"), padrao);
        var i3 = padrao.IndexOf(DateTime.Today.AddDays(3).ToString("dd/MM/yyyy"), StringComparison.Ordinal);
        var i10 = padrao.IndexOf(DateTime.Today.AddDays(10).ToString("dd/MM/yyyy"), StringComparison.Ordinal);
        Assert.True(i3 >= 0 && i10 > i3, "Próximas devem aparecer da mais cedo para a mais tarde.");

        var hist = await client.GetStringAsync("/Reserva?filtro=historico");
        Assert.Contains(DateTime.Today.AddDays(-2).ToString("dd/MM/yyyy"), hist);
        Assert.Contains(DateTime.Today.AddDays(-5).ToString("dd/MM/yyyy"), hist);
        Assert.DoesNotContain(DateTime.Today.AddDays(10).ToString("dd/MM/yyyy"), hist);
        var h2 = hist.IndexOf(DateTime.Today.AddDays(-2).ToString("dd/MM/yyyy"), StringComparison.Ordinal);
        var h5 = hist.IndexOf(DateTime.Today.AddDays(-5).ToString("dd/MM/yyyy"), StringComparison.Ordinal);
        Assert.True(h2 >= 0 && h5 > h2, "Histórico deve listar as mais recentes primeiro.");
    }
}
