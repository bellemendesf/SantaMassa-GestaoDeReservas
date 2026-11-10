// Importa as classes de acesso ao SQL Server
using Microsoft.Data.SqlClient;
// Importa as classes do ASP.NET MVC (Controller, ActionResult...)
using Microsoft.AspNetCore.Mvc;
// Importa os modelos do projeto
using GestaoReservas.Models;
// Exige login de funcionário para acessar o painel do restaurante
using Microsoft.AspNetCore.Authorization;

// Painel interno: só funcionário autenticado (cookie de login)
[Authorize(AuthenticationSchemes = "FuncionarioScheme")]
// Controller responsável pelas telas de AreaRestaurante
public class AreaRestauranteController : Controller
{
    private readonly IReservaRepository repository;
    private readonly IMesaRepository mesaRepository;
    // Usado para completar o nome do cliente quando a reserva em memória não traz Cliente
    private readonly IClienteRepository clienteRepository;

    public AreaRestauranteController(
        IReservaRepository repository,
        IMesaRepository mesaRepository,
        IClienteRepository clienteRepository)
    {
        this.repository = repository;
        this.mesaRepository = mesaRepository;
        this.clienteRepository = clienteRepository;
    }

    // Ação Index: mostra a listagem
    public ActionResult Index()
    {
        // Mostra a view com o modelo
        return View();
    }

    // CORREÇÃO: organiza as ações do funcionário sem replicar o CRUD de mesas e ambientes.
    public ActionResult CadastrarFuncionario()
    {
        // Redireciona para outra tela
        return RedirectToAction("Create", "Funcionario");
    }

    // Ação GerenciarMesas
    public ActionResult GerenciarMesas()
    {
        // Redireciona para outra tela
        return RedirectToAction("Index", "Mesa");
    }

    // Ação GerenciarAmbientes
    public ActionResult GerenciarAmbientes()
    {
        // Redireciona para outra tela
        return RedirectToAction("Index", "Ambiente");
    }

    // Ação Reservas — filtros opcionais por nome do cliente e status (C#, sem SQL extra)
    public ActionResult Reservas(string? nome, string? status)
    {
        // CORREÇÃO: reutiliza a View existente; links Edit levam ao fluxo do restaurante.
        // Texto de busca (nome) e status escolhido no formulário GET
        var buscaNome = (nome ?? "").Trim();
        var buscaStatus = (status ?? "").Trim();

        // Lista base do repositório
        var baseLista = repository.Read().ToList();
        // Garante Cliente.Nome preenchido (SQL já traz; memória às vezes só tem ClienteId)
        foreach (var r in baseLista)
        {
            if (!string.IsNullOrWhiteSpace(r.Cliente?.Nome)) continue;
            var cli = clienteRepository.Read(r.ClienteId);
            if (cli != null) r.Cliente = cli;
        }

        // Filtro por nome do cliente (contém; ignora maiúsculas e acentos)
        IEnumerable<Reserva> filtrada = baseLista;
        if (!string.IsNullOrWhiteSpace(buscaNome))
        {
            filtrada = filtrada.Where(r =>
                NomeContem(r.Cliente?.Nome, buscaNome));
        }

        // Filtro por status (Todos = vazio)
        if (!string.IsNullOrWhiteSpace(buscaStatus) &&
            !string.Equals(buscaStatus, "Todos", StringComparison.OrdinalIgnoreCase))
        {
            filtrada = filtrada.Where(r =>
                string.Equals(r.Status, buscaStatus, StringComparison.OrdinalIgnoreCase));
        }

        // Ordena Pendente primeiro; dentro de cada grupo mantém a ordem original (estável).
        var lista = filtrada
            .Select((r, i) => new { Reserva = r, OrdemOriginal = i })
            .OrderBy(x => string.Equals(x.Reserva.Status, "Pendente", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(x => x.OrdemOriginal)
            .Select(x => x.Reserva)
            .ToList();

        // Mantém os valores preenchidos no formulário após filtrar
        ViewBag.FiltroNome = buscaNome;
        ViewBag.FiltroStatus = string.IsNullOrWhiteSpace(buscaStatus) ? "Todos" : buscaStatus;
        // Mostra a view com a lista já filtrada/ordenada
        return View("~/Views/Reserva/Index.cshtml", lista);
    }

    // Compara nomes ignorando maiúsculas e acentos (ex.: José ≈ jose)
    private static bool NomeContem(string? nome, string busca)
    {
        // Sem nome cadastrado → não casa
        if (string.IsNullOrWhiteSpace(nome)) return false;
        // IndexOf com IgnoreCase + IgnoreNonSpace
        return System.Globalization.CultureInfo.InvariantCulture.CompareInfo.IndexOf(
            nome,
            busca,
            System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
    }

    // Ação atendida por requisições GET (abrir a tela)
    [HttpGet]
    // Ação Edit (GET): abre o formulário de edição
    public ActionResult Edit([FromRoute] int id)
    {
        // Busca o registro no banco
        var reserva = repository.Read(id);
        // Se não encontrou o registro...
        if (reserva == null) return NotFound();
        // Condição
        if (reserva.Status == "Cancelada") return BadRequest("Reserva cancelada não pode ser alterada.");

        // Mostra a view com o modelo
        return View("~/Views/Reserva/Edit.cshtml", reserva);
    }

    // Ação atendida por requisições POST (envio do formulário)
    [HttpPost]
    // Ação Edit (POST): valida e salva as alterações
    public ActionResult Edit([FromRoute] int id, string status)
    {
        // Variável reservaId
        int reservaId = id;
        // Busca o registro no banco
        var reserva = repository.Read(reservaId);
        // Se não encontrou o registro...
        if (reserva == null) return NotFound();
        // Condição
        if (reserva.Status == "Cancelada") return BadRequest("Reserva cancelada não pode ser alterada.");

        // CORREÇÃO: apesar de a View enviar outros campos, funcionário só atualiza Status.
        if (status != "Pendente" &&
            status != "Confirmada" &&
            status != "Cancelada")
        {
            // Registra a mensagem de erro para a tela
            ModelState.AddModelError("Status", "Informe Pendente, Confirmada ou Cancelada.");
            // Mostra a view com o modelo
            return View("~/Views/Reserva/Edit.cshtml", reserva);
        }

        // Tenta executar; erros são tratados abaixo
        try
        {
            repository.UpdateStatus(reservaId, status);
            MesaReservaSync.AplicarStatusReserva(repository, mesaRepository, reserva, status);
        }
        // Erro vindo do banco de dados
        catch (SqlException)
        {
            // Registra a mensagem de erro para a tela
            ModelState.AddModelError("", "Não foi possível salvar o status. Verifique a conexão com o banco.");
            // Mostra a view com o modelo
            return View("~/Views/Reserva/Edit.cshtml", reserva);
        }
        // Redireciona para outra tela
        return RedirectToAction("Reservas");
    }

    // Ação atendida por requisições GET (abrir a tela)
    [HttpGet]
    // Ação Details: mostra os detalhes do registro
    public ActionResult Details(int id)
    {
        // Busca o registro no banco
        var reserva = repository.Read(id);
        // Se não encontrou o registro...
        if (reserva == null) return NotFound();
        // Mostra a view com o modelo
        return View("~/Views/Reserva/Details.cshtml", reserva);
    }

    // Ação atendida por requisições GET (abrir a tela)
    [HttpGet]
    // Ação Delete: confirmação de exclusão
    public ActionResult Delete(int id)
    {
        // CORREÇÃO: funcionário não possui ação de excluir reserva.
        return StatusCode(403);
    }

    // Ação atendida por requisições POST (envio do formulário)
    [HttpPost]
    // Ação Delete: confirmação de exclusão
    public ActionResult Delete(Reserva model)
    {
        // CORREÇÃO: impede exclusão no fluxo específico do restaurante.
        return StatusCode(403);
    }

    // Ação atendida por requisições GET (abrir a tela)
    [HttpGet]
    // Ação Create (GET)
    public ActionResult Create()
    {
        // CORREÇÃO: criar reserva não é uma operação do funcionário.
        return StatusCode(403);
    }
}
