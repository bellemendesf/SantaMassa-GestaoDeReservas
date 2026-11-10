using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using GestaoReservas.Models;
using Microsoft.Extensions.DependencyInjection;

namespace T36uCapture;

public static class CaptureEntry
{
    public static async Task Main(string[] args)
    {
        var outDir = @"C:\Users\isabe\GestaoReservas\screenshots-t36u-v2\html";
        Directory.CreateDirectory(outDir);
        var www = @"C:\Users\isabe\GestaoReservas\wwwroot";

        using var app = new AplicacaoT36u();
        var sp = app.Services;

        sp.GetRequiredService<IAmbienteRepository>().Create(new Ambiente { Nome = "Interno", Capacidade = 40, Status = "Disponivel" });
        sp.GetRequiredService<IAmbienteRepository>().Create(new Ambiente { Nome = "Externo", Capacidade = 24, Status = "Disponivel" });
        sp.GetRequiredService<IMesaRepository>().Create(new Mesa { Numero = 1, AmbienteId = 1, QtdLugares = 4, Status = "Livre", AmbienteNome = "Interno" });
        sp.GetRequiredService<IMesaRepository>().Create(new Mesa { Numero = 2, AmbienteId = 1, QtdLugares = 4, Status = "Livre", AmbienteNome = "Interno" });
        sp.GetRequiredService<IMesaRepository>().Create(new Mesa { Numero = 1, AmbienteId = 2, QtdLugares = 2, Status = "Livre", AmbienteNome = "Externo" });
        sp.GetRequiredService<IFuncionarioRepository>().Create(new Funcionario
        {
            Nome = "Funcionario teste",
            Cpf = "111.222.333-44",
            EmailInstitucional = "func@santamassa.com.br",
            Senha = "Func@123"
        });

        async Task<string> GetHtml(HttpClient client, string url)
        {
            var resp = await client.GetAsync(url);
            var html = await resp.Content.ReadAsStringAsync();
            Console.WriteLine($"{(int)resp.StatusCode} {url} ({html.Length} bytes)");
            return html;
        }

        async Task PostAsync(HttpClient client, string get, string post, Dictionary<string, string> fields)
        {
            var page = await client.GetAsync(get);
            page.EnsureSuccessStatusCode();
            var html = await page.Content.ReadAsStringAsync();
            var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            if (!token.Success) throw new Exception("Token ausente: " + get);
            fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value);
            var resp = await client.PostAsync(post, new FormUrlEncodedContent(fields));
            Console.WriteLine($"POST {post} -> {(int)resp.StatusCode}");
        }

        string Rewrite(string html)
        {
            var baseTag = $"<base href=\"file:///{www.Replace("\\", "/")}/\">";
            if (html.Contains("<head>", StringComparison.OrdinalIgnoreCase))
                html = Regex.Replace(html, "<head>", "<head>" + baseTag, RegexOptions.IgnoreCase);
            else if (html.Contains("<head ", StringComparison.OrdinalIgnoreCase))
                html = Regex.Replace(html, "<head([^>]*)>", "<head$1>" + baseTag, RegexOptions.IgnoreCase);

            html = Regex.Replace(html, "href=\"~/css/", "href=\"css/", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, "src=\"~/images/", "src=\"images/", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, "href=\"/css/", "href=\"css/", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, "src=\"/images/", "src=\"images/", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, "(href=\"css/[^\"]+)\\?v=[^\"]+\"", "$1\"");
            return html;
        }

        void Save(string name, string html)
        {
            var path = Path.Combine(outDir, name + ".html");
            File.WriteAllText(path, Rewrite(html), Encoding.UTF8);
            Console.WriteLine("saved " + path);
        }

        using (var anon = app.Cliente())
        {
            Save("home", await GetHtml(anon, "/"));
            Save("login-cliente", await GetHtml(anon, "/Auth/LoginCliente"));
            Save("login-funcionario", await GetHtml(anon, "/Auth/Login"));
            Save("cadastro-cliente", await GetHtml(anon, "/Cliente/Create"));
        }

        using (var cliente = app.Cliente())
        {
            await PostAsync(cliente, "/Cliente/Create", "/Cliente/Create", new Dictionary<string, string>
            {
                ["Nome"] = "Cliente de teste",
                ["Cpf"] = "12345678901",
                ["Telefone"] = "17999999999",
                ["EmailPessoal"] = "teste@email.com",
                ["Senha"] = "Cliente@123"
            });
            await PostAsync(cliente, "/Auth/LoginCliente", "/Auth/LoginCliente", new Dictionary<string, string>
            {
                ["cpf"] = "12345678901",
                ["senha"] = "Cliente@123"
            });

            sp.GetRequiredService<IReservaRepository>().Create(new Reserva
            {
                ClienteId = 1,
                Cliente = new Cliente { Id = 1, Nome = "Cliente de teste", Cpf = "12345678901" },
                DataHora = new DateTime(2027, 1, 15),
                QtdPessoas = 2,
                Status = "Pendente",
                MesaIds = new List<int> { 1 }
            });
            sp.GetRequiredService<IReservaRepository>().Create(new Reserva
            {
                ClienteId = 1,
                Cliente = new Cliente { Id = 1, Nome = "Cliente de teste", Cpf = "12345678901" },
                DataHora = new DateTime(2026, 10, 10),
                QtdPessoas = 2,
                Status = "Confirmada",
                MesaIds = new List<int> { 2 }
            });

            Save("cliente-minhas-reservas", await GetHtml(cliente, "/Reserva"));
            Save("cliente-minhas-reservas-historico", await GetHtml(cliente, "/Reserva?filtro=historico"));
            var createHtml = await GetHtml(cliente, "/Reserva/Create");
            createHtml = createHtml
                .Replace("data-chave=\"Interno\" data-ambiente-id=\"1\" hidden", "data-chave=\"Interno\" data-ambiente-id=\"1\"")
                .Replace("aria-hidden=\"true\"", "aria-hidden=\"false\"", StringComparison.OrdinalIgnoreCase);
            createHtml = createHtml.Replace(
                "<div class=\"mapa-listagem selecao-mesas\" aria-label=\"Mesas cadastradas no Interno\"></div>",
                "<div class=\"mapa-listagem selecao-mesas\" aria-label=\"Mesas cadastradas no Interno\">" +
                "<button type=\"button\" class=\"mesa-opcao livre selecionada\">Mesa 1<small>4 lugares · Livre</small></button>" +
                "<button type=\"button\" class=\"mesa-opcao livre\">Mesa 2<small>4 lugares · Livre</small></button>" +
                "</div>");
            createHtml = Regex.Replace(createHtml,
                "(class=\"mapa-mesa[^\"]*\" data-numero=\"1\")",
                "class=\"mapa-mesa livre selecionada\" data-numero=\"1\"");
            createHtml = Regex.Replace(createHtml,
                "(class=\"mapa-mesa[^\"]*\" data-numero=\"2\")",
                "class=\"mapa-mesa livre\" data-numero=\"2\"");
            Save("cliente-create-mapa", createHtml);
            Save("cliente-edit", await GetHtml(cliente, "/Reserva/Edit/1"));
            Save("cliente-details", await GetHtml(cliente, "/Reserva/Details/1"));
        }

        using (var func = app.Cliente())
        {
            await PostAsync(func, "/Auth/Login", "/Auth/Login", new Dictionary<string, string>
            {
                ["Email"] = "func@santamassa.com.br",
                ["senha"] = "Func@123"
            });
            Save("restaurante-inicio", await GetHtml(func, "/AreaRestaurante"));
            Save("restaurante-reservas", await GetHtml(func, "/AreaRestaurante/Reservas"));
            Save("restaurante-mesas", await GetHtml(func, "/Mesa"));
            Save("restaurante-mesas-create", await GetHtml(func, "/Mesa/Create"));
            Save("restaurante-mesas-edit", await GetHtml(func, "/Mesa/Edit/1"));
            Save("restaurante-ambientes", await GetHtml(func, "/Ambiente"));
            Save("restaurante-clientes", await GetHtml(func, "/Cliente"));
            Save("restaurante-funcionarios", await GetHtml(func, "/Funcionario"));
            Save("restaurante-funcionarios-create", await GetHtml(func, "/Funcionario/Create"));
            Save("restaurante-funcionarios-edit", await GetHtml(func, "/Funcionario/Edit/1"));
            Save("restaurante-reserva-details", await GetHtml(func, "/AreaRestaurante/Details/1"));
            Save("restaurante-reserva-edit", await GetHtml(func, "/AreaRestaurante/Edit/1"));
        }

        Console.WriteLine("HTML dump OK em " + outDir);
    }
}
