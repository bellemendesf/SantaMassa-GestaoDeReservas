using System.Net;
using System.Text.RegularExpressions;
using GestaoReservas.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GestaoReservas.Tests;

public class BateriaFrontTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/Auth/Login")]
    [InlineData("/Auth/LoginCliente")]
    [InlineData("/Cliente/Create")]
    public async Task HomeELoginsUsamTemaPretoSemTemaClaroESemSidebar(string rota)
    {
        using var app = new AplicacaoTeste();
        using var client = app.Cliente();
        var resp = await client.GetAsync(rota);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var html = await resp.Content.ReadAsStringAsync();
        Assert.DoesNotContain("tema-claro", html);
        Assert.DoesNotContain("menu-restaurante", html);
        Assert.DoesNotContain("```", html);
        AssertCaixaErroNaoVaziaVisivel(html);
    }

    [Theory]
    [InlineData("/Reserva")]
    [InlineData("/Reserva/Create")]
    [InlineData("/Reserva?filtro=historico")]
    [InlineData("/Reserva?filtro=proximas")]
    public async Task PaginasDoClienteAbrem200ComTemaClaroSemSidebar(string rota)
    {
        using var app = new AplicacaoTeste();
        TesteApoio.PrepararMesaTeste(app.Services);
        using var client = app.Cliente();
        await TesteApoio.LoginClienteTeste(client, app.Services);
        var resp = await client.GetAsync(rota);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var html = await resp.Content.ReadAsStringAsync();
        Assert.Contains("tema-claro", html);
        Assert.Contains("<body", html);
        Assert.Matches("(?is)<body[^>]*class=\"[^\"]*tema-claro", html);
        Assert.DoesNotContain("menu-restaurante", html);
        Assert.DoesNotContain("```", html);
        AssertCaixaErroNaoVaziaVisivel(html);
    }

    [Theory]
    [InlineData("/AreaRestaurante")]
    [InlineData("/AreaRestaurante/Reservas")]
    [InlineData("/Mesa")]
    [InlineData("/Mesa/Create")]
    [InlineData("/Ambiente")]
    [InlineData("/Ambiente/Create")]
    [InlineData("/Cliente")]
    [InlineData("/Funcionario")]
    [InlineData("/Funcionario/Create")]
    public async Task PaginasDoFuncionarioAbrem200ComTemaClaroESidebar(string rota)
    {
        using var app = new AplicacaoTeste();
        TesteApoio.PrepararMesaTeste(app.Services);
        using var client = app.Cliente();
        await TesteApoio.LoginFuncionarioTeste(client, app.Services);
        var resp = await client.GetAsync(rota);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var html = await resp.Content.ReadAsStringAsync();
        Assert.Matches("(?is)<body[^>]*class=\"[^\"]*tema-claro", html);
        Assert.Contains("menu-restaurante", html);
        Assert.DoesNotContain("```", html);
        AssertCaixaErroNaoVaziaVisivel(html);
    }

    [Fact]
    public async Task ClienteEditDetailsEDeleteAbrem200()
    {
        using var app = new AplicacaoTeste();
        TesteApoio.PrepararMesaTeste(app.Services);
        using var client = app.Cliente();
        await TesteApoio.LoginClienteTeste(client, app.Services);
        Assert.Equal(HttpStatusCode.Redirect, (await TesteApoio.PostReservaComQtd(client, "2027-01-11", 2, 1)).StatusCode);
        foreach (var rota in new[] { "/Reserva/Edit/1", "/Reserva/Details/1", "/Reserva/Delete/1" })
        {
            var resp = await client.GetAsync(rota);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var html = await resp.Content.ReadAsStringAsync();
            Assert.Matches("(?is)<body[^>]*class=\"[^\"]*tema-claro", html);
            Assert.DoesNotContain("menu-restaurante", html);
            AssertCaixaErroNaoVaziaVisivel(html);
        }
    }

    [Fact]
    public async Task FuncionarioEditDetailsClienteAmbienteMesaAbrem200()
    {
        using var app = new AplicacaoTeste();
        TesteApoio.PrepararMesaTeste(app.Services);
        using var seed = app.Cliente();
        await TesteApoio.LoginClienteTeste(seed, app.Services);
        await TesteApoio.PostReservaComQtd(seed, "2027-01-11", 2, 1);
        using var func = app.Cliente();
        await TesteApoio.LoginFuncionarioTeste(func, app.Services);
        var funcId = app.Services.GetRequiredService<IFuncionarioRepository>().Read().First().Id;
        var rotas = new[]
        {
            "/AreaRestaurante/Edit/1", "/AreaRestaurante/Details/1",
            "/Mesa/Edit/1", "/Mesa/Details/1", "/Mesa/Delete/1",
            "/Ambiente/Edit/1", "/Ambiente/Details/1",
            "/Cliente/Details/1",
            $"/Funcionario/Edit/{funcId}", $"/Funcionario/Details/{funcId}"
        };
        foreach (var rota in rotas)
        {
            var resp = await func.GetAsync(rota);
            Assert.True(resp.StatusCode == HttpStatusCode.OK, $"{rota} => {resp.StatusCode}");
            var html = await resp.Content.ReadAsStringAsync();
            Assert.Matches("(?is)<body[^>]*class=\"[^\"]*tema-claro", html);
            Assert.Contains("menu-restaurante", html);
            AssertCaixaErroNaoVaziaVisivel(html);
        }
    }

    [Fact]
    public async Task InputsDeDataEQuantidadeTemMinMaxNoCreateENoEdit()
    {
        using var app = new AplicacaoTeste();
        TesteApoio.PrepararMesaTeste(app.Services);
        using var client = app.Cliente();
        await TesteApoio.LoginClienteTeste(client, app.Services);
        var create = await client.GetStringAsync("/Reserva/Create");
        Assert.Contains("min=\"1\"", create);
        Assert.Contains("max=\"30\"", create);
        Assert.Contains($"min=\"{DateTime.Today:yyyy-MM-dd}\"", create);
        Assert.Contains($"max=\"{DateTime.Today.AddDays(90):yyyy-MM-dd}\"", create);

        Assert.Equal(HttpStatusCode.Redirect, (await TesteApoio.PostReservaComQtd(client, "2027-01-12", 2, 1)).StatusCode);
        var edit = await client.GetStringAsync("/Reserva/Edit/1");
        Assert.Contains("min=\"", edit);
        Assert.Contains("max=\"30\"", edit);
        Assert.Contains($"max=\"{DateTime.Today.AddDays(90):yyyy-MM-dd}\"", edit);
    }

    private static void AssertCaixaErroNaoVaziaVisivel(string html)
    {
        // Caixas de erro "válidas" (sem mensagem) devem vir com validation-summary-valid
        // para o CSS ocultá-las. Uma div.alerta-erro sem essa classe e sem texto é um bug visual.
        var caixas = Regex.Matches(html, "<div[^>]*class=\"([^\"]*alerta-erro[^\"]*)\"[^>]*>(.*?)</div>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        foreach (Match caixa in caixas)
        {
            var classes = caixa.Groups[1].Value;
            var corpo = Regex.Replace(caixa.Groups[2].Value, "<[^>]+>", " ");
            corpo = WebUtility.HtmlDecode(corpo);
            corpo = Regex.Replace(corpo, @"\s+", " ").Trim();
            if (classes.Contains("validation-summary-valid", StringComparison.OrdinalIgnoreCase))
                continue;
            if (classes.Contains("validation-summary-errors", StringComparison.OrdinalIgnoreCase))
                Assert.False(string.IsNullOrWhiteSpace(corpo), "Caixa de erro visível deveria ter mensagem.");
            else if (string.IsNullOrWhiteSpace(corpo) && !classes.Contains("mapa-aviso", StringComparison.OrdinalIgnoreCase))
                Assert.Fail("Caixa de erro renderizou vazia: " + classes);
        }
    }
}

public class BateriaBordasTests
{
    [Fact]
    public async Task ConcorrenciaDuasReservasNaMesmaMesaEDia()
    {
        using var app = new AplicacaoTeste();
        TesteApoio.PrepararMesaTeste(app.Services);
        using var a = app.Cliente();
        using var b = app.Cliente();
        await TesteApoio.LoginClienteTeste(a, app.Services, cpf: "11111111111");
        await TesteApoio.LoginClienteTeste(b, app.Services, cpf: "22222222222");

        var paginaA = await a.GetStringAsync("/Reserva/Create");
        var paginaB = await b.GetStringAsync("/Reserva/Create");
        var tokenA = TesteApoio.TokenDaPagina(paginaA);
        var tokenB = TesteApoio.TokenDaPagina(paginaB);
        FormUrlEncodedContent Campos(string token) => new(new[]
        {
            new KeyValuePair<string, string>("DataHora", "2027-01-13"),
            new KeyValuePair<string, string>("QtdPessoas", "2"),
            new KeyValuePair<string, string>("MesaIds", "1"),
            new KeyValuePair<string, string>("__RequestVerificationToken", token)
        });

        var t1 = a.PostAsync("/Reserva/Create", Campos(tokenA));
        var t2 = b.PostAsync("/Reserva/Create", Campos(tokenB));
        await Task.WhenAll(t1, t2);
        var criadas = app.Services.GetRequiredService<IReservaRepository>().Read()
            .Where(r => r.Status != "Cancelada" && r.MesaIds.Contains(1) && r.DataHora.Date == new DateTime(2027, 1, 13))
            .ToList();
        Assert.True(criadas.Count == 1,
            "Duas reservas ativas na mesma mesa e no mesmo dia foram aceitas (falha de concorrência). Total: " + criadas.Count);
    }

    [Fact]
    public async Task CadastroRecusaCpfDuplicado()
    {
        using var app = new AplicacaoTeste();
        using var client = app.Cliente();
        var primeiro = await TesteApoio.Post(client, "/Cliente/Create", "/Cliente/Create", TesteApoio.DadosCliente());
        Assert.Equal(HttpStatusCode.Redirect, primeiro.StatusCode);
        var segundo = await TesteApoio.Post(client, "/Cliente/Create", "/Cliente/Create",
            TesteApoio.DadosCliente(nome: "Outro", email: "outro@email.com", telefone: "17988887777"));
        Assert.Equal(HttpStatusCode.OK, segundo.StatusCode);
        Assert.Equal(1, app.Services.GetRequiredService<IClienteRepository>().Read().Count(c => c.Cpf == "12345678901"));
        var html = await TesteApoio.Html(segundo);
        Assert.True(html.Contains("CPF", StringComparison.OrdinalIgnoreCase) || html.Contains("já", StringComparison.OrdinalIgnoreCase) || html.Contains("duplic"), html);
    }

    [Fact]
    public async Task CamposVaziosSaoRecusadosNoCadastroENoLogin()
    {
        using var app = new AplicacaoTeste();
        using var client = app.Cliente();
        var cadastro = await TesteApoio.Post(client, "/Cliente/Create", "/Cliente/Create", new Dictionary<string, string>
        {
            ["Nome"] = "",
            ["Cpf"] = "",
            ["Telefone"] = "",
            ["EmailPessoal"] = "",
            ["Senha"] = ""
        });
        Assert.Equal(HttpStatusCode.OK, cadastro.StatusCode);
        Assert.Empty(app.Services.GetRequiredService<IClienteRepository>().Read());

        var loginCli = await TesteApoio.Post(client, "/Auth/LoginCliente", "/Auth/LoginCliente", new()
        {
            ["cpf"] = "",
            ["senha"] = ""
        });
        Assert.Equal(HttpStatusCode.OK, loginCli.StatusCode);

        var loginFunc = await TesteApoio.Post(client, "/Auth/Login", "/Auth/Login", new()
        {
            ["Email"] = "",
            ["senha"] = ""
        });
        Assert.Equal(HttpStatusCode.OK, loginFunc.StatusCode);
    }

    [Fact]
    public async Task StringsLongasRespeitamLimitesDoModelo()
    {
        using var app = new AplicacaoTeste();
        using var client = app.Cliente();
        await TesteApoio.LoginFuncionarioTeste(client, app.Services);

        // Status de ambiente: [StringLength(20)]. 21 caracteres devem ser recusados. (Nome nao tem esse teto.)
        var amb21 = await TesteApoio.Post(client, "/Ambiente/Create", "/Ambiente/Create", new()
        {
            ["Nome"] = "Patio coberto",
            ["Capacidade"] = "10",
            ["Status"] = new string('A', 21)
        });
        Assert.Equal(HttpStatusCode.OK, amb21.StatusCode);
        Assert.DoesNotContain(app.Services.GetRequiredService<IAmbienteRepository>().Read(), a => (a.Status ?? "").Length == 21);

        // Nome de pessoa: [StringLength(50)]. 31 caracteres (pedido >30) ainda cabem; 51 não.
        using var anon = app.Cliente();
        var nome31 = await TesteApoio.Post(anon, "/Cliente/Create", "/Cliente/Create",
            TesteApoio.DadosCliente(nome: new string('B', 31), cpf: "12312312312", email: "n31@email.com", telefone: "17911112222"));
        Assert.Equal(HttpStatusCode.Redirect, nome31.StatusCode);

        var nome51 = await TesteApoio.Post(anon, "/Cliente/Create", "/Cliente/Create",
            TesteApoio.DadosCliente(nome: new string('C', 51), cpf: "32132132132", email: "n51@email.com", telefone: "17911113333"));
        Assert.Equal(HttpStatusCode.OK, nome51.StatusCode);
        Assert.DoesNotContain(app.Services.GetRequiredService<IClienteRepository>().Read(), c => (c.Nome ?? "").Length == 51);
    }

    [Fact]
    public async Task FiltroPorNomeResisteAInjecaoSqlBasica()
    {
        using var app = new AplicacaoTeste();
        TesteApoio.PrepararMesaTeste(app.Services);
        using var seed = app.Cliente();
        await TesteApoio.LoginClienteTeste(seed, app.Services);
        var cli = app.Services.GetRequiredService<IClienteRepository>().Read().First();
        cli.Nome = "João Mendes";
        app.Services.GetRequiredService<IClienteRepository>().Update(cli);
        Assert.Equal(HttpStatusCode.Redirect, (await TesteApoio.PostReservaComQtd(seed, "2027-01-14", 2, 1)).StatusCode);

        using var func = app.Cliente();
        await TesteApoio.LoginFuncionarioTeste(func, app.Services);
        foreach (var injecao in new[]
        {
            "' OR 1=1 --",
            "'; DROP TABLE reservas; --",
            "1; DROP TABLE pessoas;--",
            "\" OR \"1\"=\"1"
        })
        {
            var resp = await func.GetAsync("/AreaRestaurante/Reservas?nome=" + Uri.EscapeDataString(injecao));
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var html = WebUtility.HtmlDecode(await resp.Content.ReadAsStringAsync());
            Assert.DoesNotContain("João Mendes", html);
            Assert.DoesNotContain("```", html);
        }
        // Dados seguem íntegros depois das tentativas.
        Assert.NotEmpty(app.Services.GetRequiredService<IReservaRepository>().Read());
        Assert.NotEmpty(app.Services.GetRequiredService<IClienteRepository>().Read());
    }
}
