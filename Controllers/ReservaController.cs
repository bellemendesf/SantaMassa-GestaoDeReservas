using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Security.Claims;
using GestaoReservas.Models;

[Authorize(AuthenticationSchemes = "ClienteScheme")]
public class ReservaController : Controller
{
    private readonly IReservaRepository repository;
    private readonly IMesaRepository mesaRepository;
    private readonly IAmbienteRepository ambienteRepository;
    private readonly IClienteRepository clienteRepository;

    private static readonly string[] NomesAmbientesMapa = ["Interno", "Externo", "Sacada"];

    // Construtor: recebe as dependências por injeção
    public ReservaController(
        IReservaRepository repository,
        IMesaRepository mesaRepository,
        IAmbienteRepository ambienteRepository,
        IClienteRepository clienteRepository)
    {
        this.repository = repository;
        this.mesaRepository = mesaRepository;
        this.ambienteRepository = ambienteRepository;
        this.clienteRepository = clienteRepository;
    }

    private int ClienteLogadoId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private void PreencherClienteLogado(Reserva model)
    {
        var cliente = clienteRepository.Read(ClienteLogadoId());
        if (cliente == null) return;
        model.Cliente = cliente;
        model.ClienteId = cliente.Id;
        foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Cliente.")).ToList())
            ModelState.Remove(key);
    }

    private IActionResult? GarantirReservaDoCliente(Reserva? reserva)
    {
        if (reserva == null) return NotFound();
        // Outra reserva: responde 404 (evita 500 do Forbid sem scheme e não expõe o registro).
        if (reserva.ClienteId != ClienteLogadoId()) return NotFound();
        return null;
    }

    private Dictionary<string, int> IdsAmbientesMapa()
    {
        var cadastrados = ambienteRepository.Read();
        return NomesAmbientesMapa.ToDictionary(
            nome => nome,
            nome => cadastrados.FirstOrDefault(a => AmbienteCorrespondeAoMapa(a.Nome, nome))?.Id ?? 0);
    }

    // Interno aceita também "Salão Interno" / "Salao Interno"; Externo e Sacada continuam por nome exato.
    private static bool AmbienteCorrespondeAoMapa(string nomeCadastro, string nomeMapa)
    {
        var n = (nomeCadastro ?? "").Trim();
        if (string.Equals(n, nomeMapa, StringComparison.OrdinalIgnoreCase)) return true;
        if (!string.Equals(nomeMapa, "Interno", StringComparison.OrdinalIgnoreCase)) return false;
        return n.Contains("Interno", StringComparison.OrdinalIgnoreCase);
    }

    // GET /Reserva/MesasDisponiveis?dataHora=2027-02-02&ambienteId=3
    [HttpGet]
    public ActionResult MesasDisponiveis([FromQuery] string? dataHora, [FromQuery] int ambienteId = 0)
    {
        var dia = ParseDiaConsulta(dataHora);
        // Datas passadas: o mapa não lista mesas para dias anteriores a hoje (hoje continua válido).
        if (dia != null && dia.Value < DateOnly.FromDateTime(DateTime.Today))
            return Json(Array.Empty<object>());
        var ocupadas = MesasReservadasNoDia(dia);

        var mesas = mesaRepository.Read();
        if (ambienteId > 0)
            mesas = mesas.Where(m => m.AmbienteId == ambienteId).ToList();

        var resultado = mesas.Select(m => new
        {
            id = m.Id,
            numero = m.Numero,
            lugares = m.QtdLugares,
            ambienteId = m.AmbienteId,
            ambiente = m.AmbienteNome,
            situacao = SituacaoMesaNoDia(m, ocupadas),
            dataConsulta = dia?.ToString("yyyy-MM-dd")
        }).ToList();

        return Json(resultado);
    }

    private static DateOnly? ParseDiaConsulta(string? data)
    {
        if (string.IsNullOrWhiteSpace(data)) return null;
        if (DateOnly.TryParse(data, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dia))
            return dia;
        if (DateTime.TryParse(data, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            return DateOnly.FromDateTime(dt.Date);
        return null;
    }

    private static DateOnly ToDiaReserva(DateTime dataHora) => DateOnly.FromDateTime(dataHora.Date);

    private HashSet<int> MesasReservadasNoDia(DateOnly? dia, int ignorarReservaId = 0)
    {
        if (dia == null) return new HashSet<int>();
        return repository.Read()
            .Where(r => r.Id != ignorarReservaId
                && r.Status != "Cancelada"
                && ToDiaReserva(r.DataHora) == dia.Value)
            // N:N: junta todas as mesas de cada reserva do dia (tabela Reservas_Mesas)
            .SelectMany(r => r.MesaIds)
            .ToHashSet();
    }

    private static string SituacaoMesaNoDia(Mesa m, HashSet<int> reservadasNaData)
    {
        // Ocupada = indisponível de fato em qualquer dia. Status "Reservada" no cadastro NÃO bloqueia outros dias.
        if (MesaIndisponivelPermanente(m)) return "Ocupada";
        if (reservadasNaData.Contains(m.Id)) return "Reservada";
        return "Livre";
    }

    // Status reais no banco: Disponivel/Livre, Ocupada, Reservada. Só Ocupada fecha a mesa para todas as datas.
    private static bool MesaIndisponivelPermanente(Mesa mesa) =>
        string.Equals(mesa.Status, "Ocupada", StringComparison.OrdinalIgnoreCase);

    private static bool MesaLivreParaReservaNaData(Mesa mesa, HashSet<int> reservadasNaData) =>
        !MesaIndisponivelPermanente(mesa) && !reservadasNaData.Contains(mesa.Id);

    private const int MaxDiasAntecedencia = 90;

    private static bool DataReservaAnteriorAHoje(DateTime data) =>
        ToDiaReserva(data) < DateOnly.FromDateTime(DateTime.Today);

    // Valida dia de funcionamento (sem domingo), passado e teto de 90 dias. Null = ok.
    private static string? ValidarDiaReserva(DateTime data)
    {
        if (data == default) return "Informe a data da reserva.";
        if (DataReservaAnteriorAHoje(data))
            return "A data da reserva não pode ser anterior à data de hoje.";
        if (data.DayOfWeek == DayOfWeek.Sunday)
            return "O restaurante não funciona aos domingos. Escolha um dia de segunda a sábado.";
        var limite = DateOnly.FromDateTime(DateTime.Today.AddDays(MaxDiasAntecedencia));
        if (ToDiaReserva(data) > limite)
            return $"A data da reserva não pode passar de {MaxDiasAntecedencia} dias a partir de hoje.";
        return null;
    }

    // Um cliente só pode ter uma reserva ativa (não cancelada) por dia.
    private bool ClienteJaTemReservaAtivaNoDia(int clienteId, DateOnly dia, int ignorarReservaId = 0) =>
        repository.Read().Any(r =>
            r.ClienteId == clienteId
            && r.Id != ignorarReservaId
            && r.Status != "Cancelada"
            && ToDiaReserva(r.DataHora) == dia);

    // Listagem do cliente: filtro Próximas (padrão) ou Histórico via ?filtro=
    public ActionResult Index(string? filtro)
    {
        // Identifica o cliente logado
        var clienteId = ClienteLogadoId();
        // Normaliza o filtro da query string
        var modo = (filtro ?? "").Trim().ToLowerInvariant();
        // Padrão ao abrir: próximas (data >= hoje)
        if (modo != "historico" && modo != "proximas")
            modo = "proximas";

        // Dia de hoje para comparar só a data (sem horário)
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        // Todas as reservas deste cliente
        var lista = repository.Read().Where(r => r.ClienteId == clienteId);
        // Aplica o recorte temporal
        if (modo == "historico")
        {
            // Passadas: data < hoje, mais recentes primeiro
            lista = lista
                .Where(r => ToDiaReserva(r.DataHora) < hoje)
                .OrderByDescending(r => r.DataHora);
        }
        else
        {
            // Próximas: data >= hoje, mais cedo primeiro
            lista = lista
                .Where(r => ToDiaReserva(r.DataHora) >= hoje)
                .OrderBy(r => r.DataHora);
        }

        // Guarda o filtro ativo para a view destacar o botão
        ViewBag.FiltroReservas = modo;
        ViewData["Title"] = "Minhas reservas";
        // Materializa a lista filtrada
        return View(lista.ToList());
    }

    // Ação atendida por requisições GET (abrir a tela)
    [HttpGet]
    // Ação Create (GET): abre o formulário vazio
    public ActionResult Create()
    {
        ViewBag.MapaAmbienteIds = IdsAmbientesMapa();
        var model = new Reserva { QtdPessoas = 2, Status = "Pendente" };
        PreencherClienteLogado(model);
        return View(model);
    }

    // Ação atendida por requisições POST (envio do formulário)
    [HttpPost]
    // Ação Create (POST): valida e grava o cadastro
    public ActionResult Create(Reserva model)
    {
        model.Status = "Pendente";
        ModelState.Remove(nameof(model.Status));
        PreencherClienteLogado(model);
        // Junta a data do formulário com o horário da grade (a coluna data_hora já é datetime).
        var horaEscolhida = HorarioFuncionamento.Parse(model.Horario);
        if (horaEscolhida == null)
            ModelState.AddModelError(nameof(model.Horario), "Escolha um horário entre 18:30 e 23:30, de 30 em 30 minutos.");
        else if (model.DataHora != default)
        {
            // Monta data + horário e confere se está no funcionamento e, se for hoje, se ainda não passou
            model.DataHora = HorarioFuncionamento.Combinar(model.DataHora, horaEscolhida.Value);
            var erroHorario = HorarioFuncionamento.Validar(model.DataHora, DateTime.Now);
            if (erroHorario != null)
                ModelState.AddModelError(nameof(model.Horario), erroHorario);
        }
        // Data: hoje ok; sem domingo; máximo 90 dias à frente.
        var erroDia = ValidarDiaReserva(model.DataHora);
        if (erroDia != null)
            ModelState.AddModelError(nameof(model.DataHora), erroDia);
        else if (ClienteJaTemReservaAtivaNoDia(model.ClienteId, ToDiaReserva(model.DataHora)))
            ModelState.AddModelError(nameof(model.DataHora),
                "Você já possui uma reserva ativa neste dia. Cancele a anterior ou escolha outra data.");

        // N:N: o cliente pode escolher uma ou mais mesas (repetidas são ignoradas)
        model.MesaIds = (model.MesaIds ?? new List<int>()).Where(id => id > 0).Distinct().ToList();
        if (model.MesaIds.Count == 0)
            ModelState.AddModelError(nameof(model.MesaIds), "Escolha ao menos uma mesa no mapa.");

        // MAPA DE MESAS: confere no servidor se TODAS as mesas escolhidas existem e estão livres
        // e completa automaticamente se a capacidade for menor que a quantidade de pessoas.
        if (ModelState.IsValid)
        {
            var reservadasNoDia = MesasReservadasNoDia(ToDiaReserva(model.DataHora));
            foreach (var mesaId in model.MesaIds.ToList())
            {
                var mesa = mesaRepository.Read(mesaId);
                if (mesa == null)
                    ModelState.AddModelError(nameof(model.MesaIds), $"Não foi possível cadastrar. A mesa {mesaId} não está cadastrada.");
                else if (!MesaLivreParaReservaNaData(mesa, reservadasNoDia))
                    ModelState.AddModelError(nameof(model.MesaIds), $"Não foi possível cadastrar. A mesa {mesa.Numero} ({mesa.AmbienteNome}) não está livre nesta data.");
            }

            if (ModelState.IsValid)
            {
                var todasMesas = mesaRepository.Read();
                var juncao = MesaJuncaoAutomatica.Completar(
                    model.MesaIds, model.QtdPessoas, todasMesas, reservadasNoDia);
                if (!juncao.Ok)
                    ModelState.AddModelError(nameof(model.MesaIds), juncao.Erro!);
                else
                {
                    model.MesaIds = juncao.MesaIds;
                    // Recusa mesa extra desnecessária (ex.: duas mesas quando uma só já cabe).
                    if (MesaJuncaoAutomatica.TemMesasDesnecessarias(model.MesaIds, model.QtdPessoas, todasMesas))
                        ModelState.AddModelError(nameof(model.MesaIds),
                            $"A capacidade já comporta {model.QtdPessoas} pessoas. Remova mesas extras ou diminua a quantidade de mesas selecionadas.");
                    else if (juncao.Adicionadas.Count > 0)
                        TempData["AvisoJuncaoMesas"] = MesaJuncaoAutomatica.MensagemJuncao(juncao.Adicionadas, model.QtdPessoas);
                }
            }
        }
        if (!ModelState.IsValid)
        {
            ViewBag.MapaAmbienteIds = IdsAmbientesMapa();
            return View(model);
        }
        try
        {
            repository.Create(model);
            // ALTERAÇÃO: após o cadastro mostra a reserva criada, onde o status (Pendente) aparece.
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }
        // Erro vindo do banco de dados
        catch (SqlException ex)
        {
            // Variável motivo
            string motivo = ex.Number == 547 ? "A mesa informada não está cadastrada."
                : (ex.Number == 2627 || ex.Number == 2601) ? "Telefone ou e-mail já pertence a outro cliente com CPF diferente."
                : "Verifique CPF, telefone, e mesa cadastrados e a conexão com o banco.";
            // Registra a mensagem de erro para a tela
            ModelState.AddModelError("", "Não foi possível cadastrar. " + motivo);
            ViewBag.MapaAmbienteIds = IdsAmbientesMapa();
            return View(model);
        }
        // Mesa reservada por outra pessoa no mesmo instante (conferido de novo na gravação)
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(model.MesaIds), "Não foi possível cadastrar. " + ex.Message);
            ViewBag.MapaAmbienteIds = IdsAmbientesMapa();
            return View(model);
        }
    }

    [HttpGet]
    public IActionResult Edit([FromRoute] int id)
    {
        // Busca o registro no banco
        var reserva = repository.Read(id);
        var acesso = GarantirReservaDoCliente(reserva);
        if (acesso != null) return acesso;
        if (reserva!.Status == "Cancelada") return BadRequest("Reserva cancelada não pode ser editada.");
        return View(reserva);
    }

    [HttpPost]
    public IActionResult Edit([FromRoute] int id, Reserva model)
    {
        var original = repository.Read(id);
        var acesso = GarantirReservaDoCliente(original);
        if (acesso != null) return acesso;
        if (original!.Status == "Cancelada") return BadRequest("Reserva cancelada não pode ser editada.");

        // Ajusta Id do modelo
        model.Id = id;
        // Ajusta Cliente do modelo
        model.Cliente = original.Cliente;
        // Ajusta ClienteId do modelo
        model.ClienteId = original.ClienteId;
        // Ajusta Status do modelo
        model.Status = original.Status;
        // Parte das mesas originais: o cliente não remove mesas; o sistema pode JUNTAR outras se faltar lugar.
        model.MesaIds = original.MesaIds.ToList();
        model.MesasDescricao = original.MesasDescricao.ToList();
        // O cliente não altera o horário na edição: preserva o horário original (também nas reservas antigas sem horário).
        var dataInformada = model.DataHora == default ? original.DataHora.Date : model.DataHora.Date;
        model.DataHora = dataInformada.Add(original.DataHora.TimeOfDay);
        // Esses dados não fazem parte da edição pelo cliente.
        foreach (var key in ModelState.Keys.Where(k =>
            // Atribui k
            k == "Id" || k == "Cliente" || k.StartsWith("Cliente.") ||
            // Atribui k
            k == "ClienteId" || k == "Status" || k == "Horario" || k.StartsWith("MesaIds")).ToList())
            // Ignora a validação deste campo
            ModelState.Remove(key);
        // Se a data mudou, aplica as mesmas regras de dia (passado/domingo/90 dias) e 1 reserva ativa/dia.
        var dataMudou = model.DataHora != default && ToDiaReserva(model.DataHora) != ToDiaReserva(original.DataHora);
        if (model.DataHora == default)
            ModelState.AddModelError(nameof(model.DataHora), "Informe a data da reserva.");
        else if (dataMudou)
        {
            var erroDiaEdit = ValidarDiaReserva(model.DataHora);
            if (erroDiaEdit != null)
                ModelState.AddModelError(nameof(model.DataHora), erroDiaEdit);
            else if (ClienteJaTemReservaAtivaNoDia(model.ClienteId, ToDiaReserva(model.DataHora), id))
                ModelState.AddModelError(nameof(model.DataHora),
                    "Você já possui uma reserva ativa neste dia. Cancele a anterior ou escolha outra data.");
        }

        // Cliente não pode diminuir a quantidade de pessoas (só aumentar ou manter).
        if (model.QtdPessoas < original.QtdPessoas)
            ModelState.AddModelError(nameof(model.QtdPessoas),
                "Não é possível diminuir a quantidade de pessoas. Se precisar de menos lugares, cancele a reserva e faça uma nova.");

        // Confere se as mesas atuais estão livres no dia e completa capacidade se o cliente aumentou a quantidade.
        if (ModelState.IsValid)
        {
            var reservadasNoDia = MesasReservadasNoDia(ToDiaReserva(model.DataHora), id);
            foreach (var mesaId in model.MesaIds.ToList())
            {
                // Busca a mesa da reserva no banco
                var mesa = mesaRepository.Read(mesaId);
                // Mesa ocupada/reservada por outra reserva no mesmo dia: mostra o erro
                if (mesa != null && !MesaLivreParaReservaNaData(mesa, reservadasNoDia))
                    ModelState.AddModelError(nameof(model.MesaIds), $"Não foi possível salvar. A mesa {mesa.Numero} ({mesa.AmbienteNome}) não está livre nesta data.");
            }

            if (ModelState.IsValid)
            {
                // Na edição: só completa se faltar lugar (aumentou pessoas).
                // Diminuir quantidade já foi bloqueado acima; mesas originais nunca são removidas aqui.
                var juncao = MesaJuncaoAutomatica.Completar(
                    model.MesaIds, model.QtdPessoas, mesaRepository.Read(), reservadasNoDia);
                if (!juncao.Ok)
                    ModelState.AddModelError(nameof(model.MesaIds), juncao.Erro!);
                else
                {
                    model.MesaIds = juncao.MesaIds;
                    if (juncao.Adicionadas.Count > 0)
                        TempData["AvisoJuncaoMesas"] = MesaJuncaoAutomatica.MensagemJuncao(juncao.Adicionadas, model.QtdPessoas);
                }
            }
        }
        // Se houver erro de validação, volta para o formulário
        if (!ModelState.IsValid) return View(model);
        // Tenta executar; erros são tratados abaixo
        try
        {
            // Chama o repositório: Update
            repository.Update(model);
            // Redireciona para outra tela
            return RedirectToAction(nameof(Index));
        }
        // Erro vindo do banco de dados
        catch (SqlException)
        {
            // Registra a mensagem de erro para a tela
            ModelState.AddModelError("", "Não foi possível salvar. Verifique a mesa informada e a conexão com o banco.");
            // Mostra a view com o modelo
            return View(model);
        }
        // Mesa juntada no momento em que outra reserva a pegou
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(model.MesaIds), "Não foi possível salvar. " + ex.Message);
            return View(model);
        }
    }

    // Ação atendida por requisições GET (abrir a tela)
    [HttpGet]
    // Ação Details: mostra os detalhes do registro
    public IActionResult Details(int id)
    {
        // Busca o registro no banco
        var model = repository.Read(id);
        var acesso = GarantirReservaDoCliente(model);
        if (acesso != null) return acesso;
        return View(model);
    }

    [HttpGet]
    public IActionResult Delete(int id)
    {
        var model = repository.Read(id);
        var acesso = GarantirReservaDoCliente(model);
        if (acesso != null) return acesso;
        return View(model);
    }

    [HttpPost, ActionName("Delete")]
    public IActionResult DeleteConfirmed([FromRoute] int id)
    {
        var model = repository.Read(id);
        var acesso = GarantirReservaDoCliente(model);
        if (acesso != null) return acesso;
        // Tenta executar; erros são tratados abaixo
        try
        {
            repository.Delete(id);
            MesaReservaSync.AplicarStatusReserva(repository, mesaRepository, model, "Cancelada");
            return RedirectToAction(nameof(Index));
        }
        // Erro vindo do banco de dados
        catch (SqlException)
        {
            // Registra a mensagem de erro para a tela
            ModelState.AddModelError("", "Não foi possível cancelar. Verifique a conexão com o banco.");
            // Mostra a view com o modelo
            return View("Delete", model);
        }
    }
}
