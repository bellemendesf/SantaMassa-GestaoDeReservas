using System.Net;
using System.Text.RegularExpressions;
using GestaoReservas.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GestaoReservas.Tests;

public class BateriaAuthTests
{
    [Fact]
    public async Task LoginClienteCpfSenhaCorretosRedireciona()
    {
        using var app = new AplicacaoTeste();
        using var client = app.Cliente();
        await TesteApoio.CadastrarCliente(client);
        var login = await TesteApoio.Post(client, "/Auth/LoginCliente", "/Auth/LoginCliente", new()
        {
            ["cpf"] = "12345678901",
            ["senha"] = "Cliente@123"
        });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Contains("/Reserva", TesteApoio.Localizacao(login), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoginClienteSenhaErradaNaoEntra()
    {
        using var app = new AplicacaoTeste();
        using var client = app.Cliente();
        await TesteApoio.CadastrarCliente(client);
        var login = await TesteApoio.Post(client, "/Auth/LoginCliente", "/Auth/LoginCliente", new()
        {
            ["cpf"] = "12345678901",
            ["senha"] = "SenhaErrada@1"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.DoesNotContain("set-cookie", login.Headers.SelectMany(h => h.Value).FirstOrDefault(v => v.Contains("Cliente", StringComparison.OrdinalIgnoreCase)) ?? "");
        var html = await TesteApoio.Html(login);
        Assert.True(html.Contains("senha", StringComparison.OrdinalIgnoreCase) || html.Contains("inválid", StringComparison.OrdinalIgnoreCase) || html.Contains("incorret", StringComparison.OrdinalIgnoreCase) || html.Contains("não", StringComparison.OrdinalIgnoreCase), html);
    }

    [Fact]
    public async Task LoginClienteCpfInexistenteNaoEntra()
    {
        using var app = new AplicacaoTeste();
        using var client = app.Cliente();
        var login = await TesteApoio.Post(client, "/Auth/LoginCliente", "/Auth/LoginCliente", new()
        {
            ["cpf"] = "00000000000",
            ["senha"] = "Cliente@123"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Empty(login.Headers.Where(h => h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase)
            && h.Value.Any(v => v.Contains("ClienteScheme", StringComparison.OrdinalIgnoreCase) || v.StartsWith(".AspNetCore", StringComparison.OrdinalIgnoreCase) && v.Contains("Cliente", StringComparison.OrdinalIgnoreCase))));
    }

    [Fact]
    public async Task LoginFuncionarioEmailSenhaCorretosRedireciona()
    {
        using var app = new AplicacaoTeste();
        using var client = app.Cliente();
        await TesteApoio.LoginFuncionarioTeste(client, app.Services);
        // O próprio helper já exige redirect de sucesso.
    }

    [Fact]
    public async Task LoginFuncionarioEmailMaiusculasAceito()
    {
        using var app = new AplicacaoTeste();
        using var client = app.Cliente();
        app.Services.GetRequiredService<IFuncionarioRepository>().Create(new Funcionario
        {
            Nome = "Funcionario teste",
            Cpf = "111.222.333-44",
            EmailInstitucional = "func@santamassa.com.br",
            Senha = "Func@123"
        });
        var login = await TesteApoio.Post(client, "/Auth/Login", "/Auth/Login", new()
        {
            ["Email"] = "FUNC@SANTAMASSA.COM.BR",
            ["senha"] = "Func@123"
        });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Contains("/AreaRestaurante", TesteApoio.Localizacao(login), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoginFuncionarioSemDominioSantamassaNaoEntra()
    {
        using var app = new AplicacaoTeste();
        using var client = app.Cliente();
        app.Services.GetRequiredService<IFuncionarioRepository>().Create(new Funcionario
        {
            Nome = "Fora",
            Cpf = "111.222.333-44",
            EmailInstitucional = "func@santamassa.com.br",
            Senha = "Func@123"
        });
        var login = await TesteApoio.Post(client, "/Auth/Login", "/Auth/Login", new()
        {
            ["Email"] = "func@gmail.com",
            ["senha"] = "Func@123"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task LoginFuncionarioSenhaErradaNaoEntra()
    {
        using var app = new AplicacaoTeste();
        using var client = app.Cliente();
        app.Services.GetRequiredService<IFuncionarioRepository>().Create(new Funcionario
        {
            Nome = "Funcionario teste",
            Cpf = "111.222.333-44",
            EmailInstitucional = "func@santamassa.com.br",
            Senha = "Func@123"
        });
        var login = await TesteApoio.Post(client, "/Auth/Login", "/Auth/Login", new()
        {
            ["Email"] = "func@santamassa.com.br",
            ["senha"] = "Errada@123"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task SenhaDeFuncionarioNaoServeNoLoginCliente()
    {
        using var app = new AplicacaoTeste();
        using var client = app.Cliente();
        await TesteApoio.CadastrarCliente(client, TesteApoio.DadosCliente(senha: "Cliente@123"));
        app.Services.GetRequiredService<IFuncionarioRepository>().Create(new Funcionario
        {
            Nome = "Funcionario teste",
            Cpf = "12345678901",
            EmailInstitucional = "func@santamassa.com.br",
            Senha = "Func@123"
        });
        var login = await TesteApoio.Post(client, "/Auth/LoginCliente", "/Auth/LoginCliente", new()
        {
            ["cpf"] = "12345678901",
            ["senha"] = "Func@123"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task SenhaDeClienteNaoServeNoLoginFuncionario()
    {
        using var app = new AplicacaoTeste();
        using var client = app.Cliente();
        await TesteApoio.CadastrarCliente(client, TesteApoio.DadosCliente(senha: "Cliente@123"));
        app.Services.GetRequiredService<IFuncionarioRepository>().Create(new Funcionario
        {
            Nome = "Funcionario teste",
            Cpf = "111.222.333-44",
            EmailInstitucional = "func@santamassa.com.br",
            Senha = "Func@123"
        });
        var login = await TesteApoio.Post(client, "/Auth/Login", "/Auth/Login", new()
        {
            ["Email"] = "func@santamassa.com.br",
            ["senha"] = "Cliente@123"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task SairHeaderClienteNaoDevolve400()
    {
        using var app = new AplicacaoTeste();
        using var client = app.Cliente();
        await TesteApoio.LoginClienteTeste(client, app.Services);
        var resp = await client.PostAsync("/Auth/LogoutCliente", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.NotEqual(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.True(resp.StatusCode == HttpStatusCode.Redirect || resp.StatusCode == HttpStatusCode.RedirectKeepVerb || resp.StatusCode == HttpStatusCode.SeeOther, resp.StatusCode.ToString());
    }

    [Fact]
    public async Task SairHeaderFuncionarioNaoDevolve400()
    {
        using var app = new AplicacaoTeste();
        using var client = app.Cliente();
        await TesteApoio.LoginFuncionarioTeste(client, app.Services);
        var resp = await client.PostAsync("/Auth/Logout", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.NotEqual(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.True(resp.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.RedirectKeepVerb or HttpStatusCode.SeeOther, resp.StatusCode.ToString());
    }

    [Theory]
    [InlineData("/AreaRestaurante")]
    [InlineData("/AreaRestaurante/Reservas")]
    [InlineData("/Mesa")]
    [InlineData("/Ambiente")]
    [InlineData("/Funcionario")]
    [InlineData("/Cliente")]
    public async Task TelasRestauranteSemLoginRedirecionamParaLoginFuncionario(string rota)
    {
        using var app = new AplicacaoTeste();
        using var client = app.Cliente();
        var resp = await client.GetAsync(rota);
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        var loc = TesteApoio.Localizacao(resp);
        Assert.True(loc.Contains("/Auth/Login", StringComparison.OrdinalIgnoreCase)
                    && !loc.Contains("LoginCliente", StringComparison.OrdinalIgnoreCase), loc);
    }

    [Theory]
    [InlineData("/AreaRestaurante")]
    [InlineData("/AreaRestaurante/Reservas")]
    [InlineData("/Mesa")]
    [InlineData("/Ambiente")]
    [InlineData("/Funcionario")]
    public async Task TelasRestauranteComoClienteRedirecionamParaLoginFuncionario(string rota)
    {
        using var app = new AplicacaoTeste();
        using var client = app.Cliente();
        await TesteApoio.LoginClienteTeste(client, app.Services);
        var resp = await client.GetAsync(rota);
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        var loc = TesteApoio.Localizacao(resp);
        Assert.True(loc.Contains("/Auth/Login", StringComparison.OrdinalIgnoreCase)
                    && !loc.Contains("LoginCliente", StringComparison.OrdinalIgnoreCase), loc);
    }

    [Fact]
    public async Task ClienteNaoAcessaReservaDeOutroDevolve404()
    {
        using var app = new AplicacaoTeste();
        TesteApoio.PrepararMesaTeste(app.Services);
        TesteApoio.PrepararMaisMesas(app.Services, 2);
        using var client = app.Cliente();
        await TesteApoio.LoginClienteTeste(client, app.Services);
        Assert.Equal(HttpStatusCode.Redirect, (await TesteApoio.PostReservaComQtd(client, "2027-01-12", 2, 1)).StatusCode);

        using var outro = app.Cliente();
        await TesteApoio.LoginClienteTeste(outro, app.Services, cpf: "98765432100", senha: "Cliente@123");
        var resp = await outro.GetAsync("/Reserva/Edit/1");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var details = await outro.GetAsync("/Reserva/Details/1");
        Assert.Equal(HttpStatusCode.NotFound, details.StatusCode);
    }

    [Fact]
    public async Task CadastroClienteRedirecionaParaNovaReserva()
    {
        // Regra pedida: após o cadastro, o fluxo deve seguir para Nova reserva.
        using var app = new AplicacaoTeste();
        using var client = app.Cliente();
        var resp = await TesteApoio.Post(client, "/Cliente/Create", "/Cliente/Create", TesteApoio.DadosCliente());
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        var loc = TesteApoio.Localizacao(resp);
        Assert.True(
            loc.Contains("/Reserva/Create", StringComparison.OrdinalIgnoreCase),
            "Esperado redirecionamento para Nova reserva (/Reserva/Create), mas foi: " + loc);
    }
}
