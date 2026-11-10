using System.Net;
using System.Text.RegularExpressions;
using GestaoReservas.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GestaoReservas.Tests;

// Helpers compartilhados da bateria t38u (não alteram o app).
public static class TesteApoio
{
    public static async Task<HttpResponseMessage> Post(HttpClient client, string get, string post,
        Dictionary<string, string> fields)
    {
        var page = await client.GetAsync(get);
        page.EnsureSuccessStatusCode();
        var html = await page.Content.ReadAsStringAsync();
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success, "O formulário deve incluir token antifalsificação.");
        fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value);
        return await client.PostAsync(post, new FormUrlEncodedContent(fields));
    }

    public static async Task<HttpResponseMessage> PostComToken(HttpClient client, string post, string token,
        Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = token;
        return await client.PostAsync(post, new FormUrlEncodedContent(fields));
    }

    public static string TokenDaPagina(string html)
    {
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success, "O formulário deve incluir token antifalsificação.");
        return WebUtility.HtmlDecode(token.Groups[1].Value);
    }

    public static async Task<string> Html(HttpResponseMessage resp) =>
        WebUtility.HtmlDecode(await resp.Content.ReadAsStringAsync());

    public static Dictionary<string, string> DadosReserva(string? data = null, string qtd = "2", string mesa = "1") => new()
    {
        ["Cliente.Nome"] = "Cliente de teste",
        ["Cliente.Cpf"] = "12345678901",
        ["Cliente.Telefone"] = "17999999999",
        ["Cliente.EmailPessoal"] = "",
        ["MesaIds"] = mesa,
        ["FuncionarioID"] = "1",
        ["DataHora"] = data ?? "2027-01-02",
        ["QtdPessoas"] = qtd,
        ["Status"] = "Confirmada"
    };

    public static Dictionary<string, string> DadosCliente(
        string nome = "Cliente de teste",
        string cpf = "12345678901",
        string telefone = "17999999999",
        string email = "teste@email.com",
        string senha = "Cliente@123") => new()
    {
        ["Nome"] = nome,
        ["Cpf"] = cpf,
        ["Telefone"] = telefone,
        ["EmailPessoal"] = email,
        ["Senha"] = senha
    };

    public static async Task CadastrarCliente(HttpClient client, Dictionary<string, string>? dados = null)
    {
        var campos = dados ?? DadosCliente();
        var resp = await Post(client, "/Cliente/Create", "/Cliente/Create", campos);
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
    }

    public static async Task LoginClienteTeste(HttpClient client, IServiceProvider? services = null,
        string cpf = "12345678901", string senha = "Cliente@123")
    {
        if (services != null && !services.GetRequiredService<IClienteRepository>().Read().Any(c => c.Cpf == cpf))
            await CadastrarCliente(client, DadosCliente(cpf: cpf, senha: senha, email: cpf + "@email.com", telefone: TelefoneDe(cpf)));
        else if (services == null)
            await CadastrarCliente(client, DadosCliente(cpf: cpf, senha: senha, email: cpf + "@email.com", telefone: TelefoneDe(cpf)));

        var login = await Post(client, "/Auth/LoginCliente", "/Auth/LoginCliente", new Dictionary<string, string>
        {
            ["cpf"] = cpf,
            ["senha"] = senha
        });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
    }

    public static string TelefoneDe(string cpf)
    {
        var dig = new string(cpf.Where(char.IsDigit).ToArray());
        if (dig.Length < 8) dig = dig.PadRight(8, '0');
        return "17" + dig[^8..];
    }

    public static async Task LoginFuncionarioTeste(HttpClient client, IServiceProvider services,
        string email = "func@santamassa.com.br", string senha = "Func@123")
    {
        if (!services.GetRequiredService<IFuncionarioRepository>().Read().Any(f => f.EmailInstitucional == email))
        {
            services.GetRequiredService<IFuncionarioRepository>().Create(new Funcionario
            {
                Nome = "Funcionario teste",
                Cpf = "111.222.333-44",
                EmailInstitucional = email,
                Senha = senha
            });
        }
        var login = await Post(client, "/Auth/Login", "/Auth/Login", new Dictionary<string, string>
        {
            ["Email"] = email,
            ["senha"] = senha
        });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
    }

    public static void PrepararMesaTeste(IServiceProvider services)
    {
        services.GetRequiredService<IAmbienteRepository>().Create(new Ambiente
        {
            Nome = "Interno",
            Capacidade = 10,
            Status = "Disponivel"
        });
        services.GetRequiredService<IMesaRepository>().Create(new Mesa
        {
            Numero = 1,
            AmbienteId = 1,
            QtdLugares = 4,
            Status = "Livre",
            AmbienteNome = "Interno"
        });
    }

    public static void PrepararMaisMesas(IServiceProvider services, int ate, string ambienteNome = "Interno", int ambienteId = 1, int lugares = 4)
    {
        for (int n = 2; n <= ate; n++)
            services.GetRequiredService<IMesaRepository>().Create(new Mesa
            {
                Numero = n,
                AmbienteId = ambienteId,
                QtdLugares = lugares,
                Status = "Livre",
                AmbienteNome = ambienteNome
            });
    }

    public static void PrepararAmbienteEMesas(IServiceProvider services, string nome, int lugares, int quantidade, int numeroInicial)
    {
        var ambientes = services.GetRequiredService<IAmbienteRepository>();
        ambientes.Create(new Ambiente { Nome = nome, Capacidade = lugares * quantidade, Status = "Disponivel" });
        var id = ambientes.Read().First(a => a.Nome == nome).Id;
        var mesas = services.GetRequiredService<IMesaRepository>();
        for (int i = 0; i < quantidade; i++)
        {
            mesas.Create(new Mesa
            {
                Numero = numeroInicial + i,
                AmbienteId = id,
                QtdLugares = lugares,
                Status = "Livre",
                AmbienteNome = nome
            });
        }
    }

    public static async Task<HttpResponseMessage> PostReservaComQtd(HttpClient client, string data, int qtdPessoas, params int[] mesas)
    {
        var page = await client.GetStringAsync("/Reserva/Create");
        var token = TokenDaPagina(page);
        var campos = new List<KeyValuePair<string, string>>
        {
            new("DataHora", data),
            new("QtdPessoas", qtdPessoas.ToString()),
            new("__RequestVerificationToken", token)
        };
        campos.AddRange(mesas.Select(m => new KeyValuePair<string, string>("MesaIds", m.ToString())));
        return await client.PostAsync("/Reserva/Create", new FormUrlEncodedContent(campos));
    }

    public static async Task<HttpResponseMessage> PostEdicaoReserva(HttpClient client, int id, string data, int qtdPessoas, Dictionary<string, string>? extras = null)
    {
        var campos = new Dictionary<string, string>
        {
            ["Id"] = id.ToString(),
            ["DataHora"] = data,
            ["QtdPessoas"] = qtdPessoas.ToString()
        };
        if (extras != null)
        {
            foreach (var kv in extras)
                campos[kv.Key] = kv.Value;
        }
        return await Post(client, $"/Reserva/Edit/{id}", $"/Reserva/Edit/{id}", campos);
    }

    public static string ProximoDomingoIso()
    {
        var d = DateTime.Today.AddDays(1);
        while (d.DayOfWeek != DayOfWeek.Sunday) d = d.AddDays(1);
        return d.ToString("yyyy-MM-dd");
    }

    public static string DiaDaquiA(int dias) => DateTime.Today.AddDays(dias).ToString("yyyy-MM-dd");

    public static string HojeIso() => DateTime.Today.ToString("yyyy-MM-dd");

    public static string Localizacao(HttpResponseMessage resp) =>
        resp.Headers.Location?.ToString() ?? "";
}
