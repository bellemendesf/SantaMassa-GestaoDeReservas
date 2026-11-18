// Importa o [Authorize] (exigir login)
using Microsoft.AspNetCore.Authorization;

// Importa os modelos do projeto
using GestaoReservas.Models;
// Importa as classes do ASP.NET MVC (Controller, ActionResult...)
using Microsoft.AspNetCore.Mvc;
// Importa SelectList para montar listas de seleção
using Microsoft.AspNetCore.Mvc.Rendering;
// Importa as classes de acesso ao SQL Server
using Microsoft.Data.SqlClient;

// Controller responsável pelas telas de Mesa
// Gerenciar Mesas é tarefa do restaurante: exige login de funcionário (mesmo padrão dos outros controllers do restaurante)
[Authorize(AuthenticationSchemes = "FuncionarioScheme")]
public class MesaController : Controller
{
    // Dependência IMesaRepository recebida pelo construtor
    private readonly IMesaRepository repository;
    // Dependência IAmbienteRepository recebida pelo construtor
    private readonly IAmbienteRepository ambienteRepository;
    // Usado para impedir exclusão de mesa com histórico de reservas (N:N).
    private readonly IReservaRepository reservaRepository;

    // Construtor: recebe as dependências por injeção
    public MesaController(
        IMesaRepository repository,
        IAmbienteRepository ambienteRepository,
        IReservaRepository reservaRepository)
    {
        // Guarda a dependência repository recebida
        this.repository = repository;
        // Guarda a dependência ambienteRepository recebida
        this.ambienteRepository = ambienteRepository;
        this.reservaRepository = reservaRepository;
    }

    // Método auxiliar CarregarAmbientes
    private void CarregarAmbientes(int? selecionado = null)
    {
        // Envia Ambientes para a view
        ViewBag.Ambientes = new SelectList(
            ambienteRepository.Read(),
            "Id",
            "Nome",
            selecionado
        );
    }

    // Ação Index: mostra a listagem
    public ActionResult Index()
    {
        // Mostra a view com o modelo
        return View("index", repository.Read());
    }

    // Ação atendida por requisições GET (abrir a tela)
    [HttpGet]
    // Ação Create (GET)
    public ActionResult Create()
    {
        // Carrega a lista de ambientes para o select
        CarregarAmbientes();
        // Mostra a view com o modelo
        return View(new Mesa { Status = "Livre" });
    }

    // Ação atendida por requisições POST (envio do formulário)
    [HttpPost]
    // Exige o token antifalsificação do formulário
    [ValidateAntiForgeryToken]
    // Ação Create (POST): valida e grava o cadastro
    public ActionResult Create(Mesa model)
    {
        if (!StatusManualPermitido(model.Status))
            ModelState.AddModelError(nameof(model.Status), "Escolha Livre ou Ocupada.");

        // Se houver erro de validação, volta para o formulário
        if (!ModelState.IsValid)
        {
            // Carrega a lista de ambientes para o select
            CarregarAmbientes(model.AmbienteId);
            // Mostra a view com o modelo
            return View(model);
        }

        // Tenta executar; erros são tratados abaixo
        try
        {
            // Chama o repositório: Create
            repository.Create(model);
            // Redireciona para outra tela
            return RedirectToAction(nameof(Index));
        }
        // Número repetido no mesmo ambiente (mensagem visível no resumo do formulário)
        catch (InvalidOperationException ex)
        {
            // Registra a mensagem de erro para a tela
            ModelState.AddModelError("", ex.Message);
            // Carrega a lista de ambientes para o select
            CarregarAmbientes(model.AmbienteId);
            // Mostra a view com o modelo
            return View(model);
        }
        // Erro vindo do banco de dados
        catch (SqlException ex)
        {
            // Registra a mensagem de erro para a tela
            ModelState.AddModelError("", MensagemErroSqlCadastro(ex));
            // Carrega a lista de ambientes para o select
            CarregarAmbientes(model.AmbienteId);
            // Mostra a view com o modelo
            return View(model);
        }
    }

    // Ação atendida por requisições GET (abrir a tela)
    [HttpGet]
    // Ação Edit (GET): abre o formulário de edição
    public ActionResult Edit(int id)
    {
        // Busca o registro no banco
        var model = repository.Read(id);

        // Se não encontrou o registro...
        if (model == null)
            // Registro não existe: responde 404
            return NotFound();

        // Carrega a lista de ambientes para o select
        CarregarAmbientes(model.AmbienteId);
        // Mostra a view com o modelo
        return View(model);
    }

    // Ação atendida por requisições POST (envio do formulário)
    [HttpPost]
// Exige o token antifalsificação do formulário
[ValidateAntiForgeryToken]
// Ação Edit (POST): valida e salva as alterações
public ActionResult Edit(int id, Mesa model)
{
    // O ID original identifica a mesa no banco.
    // Ele não deve ser alterado pelo formulário.
    var mesaExistente = repository.Read(id);

    // Se não encontrou o registro...
    if (mesaExistente == null)
        // Registro não existe: responde 404
        return NotFound();

    // Ajusta Id do modelo (id da rota é o nr, não o número de exibição)
    model.Id = id;
    // Mantém o número de exibição original (o campo é somente leitura na edição).
    model.Numero = mesaExistente.Numero;
    ModelState.Remove(nameof(model.Numero));
    // Ignora a validação deste campo
    ModelState.Remove(nameof(model.Id));

    if (mesaExistente.Status == "Reservada")
        model.Status = "Reservada";
    else if (!StatusManualPermitido(model.Status))
        ModelState.AddModelError(nameof(model.Status), "Escolha Livre ou Ocupada.");

    // Se houver erro de validação, volta para o formulário
    if (!ModelState.IsValid)
    {
        // Carrega a lista de ambientes para o select
        CarregarAmbientes(model.AmbienteId);
        // Mostra a view com o modelo
        return View(model);
    }

    // Tenta executar; erros são tratados abaixo
    try
    {
        // Chama o repositório: Update
        repository.Update(model);
        // Redireciona para outra tela
        return RedirectToAction(nameof(Index));
    }
    // Número repetido no mesmo ambiente ao trocar o ambiente (ou o número)
    catch (InvalidOperationException ex)
    {
        // Registra a mensagem de erro para a tela
        ModelState.AddModelError("", ex.Message);
        // Carrega a lista de ambientes para o select
        CarregarAmbientes(model.AmbienteId);
        // Mostra a view com o modelo
        return View(model);
    }
    // Erro vindo do banco de dados
    catch (SqlException ex)
    {
        // Registra a mensagem de erro para a tela
        ModelState.AddModelError("", MensagemErroSqlEdicao(ex));
        // Carrega a lista de ambientes para o select
        CarregarAmbientes(model.AmbienteId);
        // Mostra a view com o modelo
        return View(model);
    }
}
    // Ação atendida por requisições GET (abrir a tela)
    [HttpGet]
    // Ação Details: mostra os detalhes do registro
    public ActionResult Details(int id)
    {
        // Busca o registro no banco
        var model = repository.Read(id);

        // Se não encontrou o registro...
        if (model == null)
            // Registro não existe: responde 404
            return NotFound();

        // Mostra a view com o modelo
        return View(model);
    }

    // Ação atendida por requisições GET (abrir a tela)
    [HttpGet]
    // Ação Delete: confirmação de exclusão
    public ActionResult Delete(int id)
    {
        // Busca o registro no banco
        var model = repository.Read(id);

        // Se não encontrou o registro...
        if (model == null)
            // Registro não existe: responde 404
            return NotFound();

        // Já explica o bloqueio na tela de confirmação, quando houver vínculo.
        if (MesaTemReservasVisiveis(id))
            ModelState.AddModelError("", MensagemMesaComReservas(model));

        // Mostra a view com o modelo
        return View(model);
    }

    // Ação atendida por requisições POST (envio do formulário)
    [HttpPost]
    // Define o nome da rota desta ação
    [ActionName("Delete")]
    // Exige o token antifalsificação do formulário
    [ValidateAntiForgeryToken]
    // Ação que executa a exclusão confirmada
    public ActionResult DeleteConfirmed(int id)
    {
        // Busca o registro no banco
        var model = repository.Read(id);

        // Se não encontrou o registro...
        if (model == null)
            // Registro não existe: responde 404
            return NotFound();

        // Tenta executar; erros são tratados abaixo
        // Também confere no app (memória/SQL): qualquer reserva vinculada bloqueia a exclusão.
        if (reservaRepository.Read().Any(r => r.MesaIds.Contains(id)))
        {
            ModelState.AddModelError("", MensagemMesaComReservas(model));
            CarregarAmbientes(model.AmbienteId);
            return View("Delete", model);
        }

        try
        {
            // Chama o repositório: Delete
            repository.Delete(id);
            // Redireciona para outra tela
            return RedirectToAction(nameof(Index));
        }
        // Erro de regra (ex.: mesa com reservas)
        catch (InvalidOperationException)
{
    // Usa o texto claro do painel (a regra no repositório permanece a mesma).
    ModelState.AddModelError("", MensagemMesaComReservas(model));
    // Mostra a view com o modelo
    return View("Delete", model);
}
// Erro vindo do banco (ex.: restrição de chave estrangeira), sem estourar 500.
catch (SqlException)
{
    // Mesma regra: a mesa não é excluída; só explica o motivo.
    ModelState.AddModelError("", MensagemMesaComReservas(model));
    // Mostra a view com o modelo
    return View("Delete", model);
}
    }


    // Monta a mensagem clara quando a mesa tem reservas vinculadas (incluindo canceladas).
    private string MensagemMesaComReservas(Mesa model)
    {
        // Conta no app as reservas visíveis ligadas a esta mesa.
        var qtd = reservaRepository.Read().Count(r => r.MesaIds != null && r.MesaIds.Contains(model.Id));
        // Número da mesa para o funcionário reconhecer o cadastro.
        var numero = model.Numero;
        // Se a consulta não trouxer canceladas, ainda assim explica o motivo real.
        if (qtd <= 0)
        {
            return $"Não é possível excluir a mesa {numero} porque ela possui reservas vinculadas (incluindo canceladas, que ficam no histórico). A mesa não foi excluída. Se quiser indisponibilizá-la, edite a mesa e altere o status para Ocupada.";
        }
        // Mensagem no singular ou no plural, com a quantidade encontrada.
        var trecho = qtd == 1 ? "1 reserva vinculada" : $"{qtd} reservas vinculadas";
        return $"Não é possível excluir a mesa {numero} porque existem {trecho} (incluindo canceladas, que ficam no histórico). A mesa não foi excluída. Se quiser indisponibilizá-la, edite a mesa e altere o status para Ocupada.";
    }

    // Indica se alguma reserva (da listagem do repositório) aponta para a mesa.
    private bool MesaTemReservasVisiveis(int id) =>
        reservaRepository.Read().Any(r => r.MesaIds != null && r.MesaIds.Contains(id));

    // Traduz erros comuns do SQL no cadastro para mensagens claras na tela.
    private static string MensagemErroSqlCadastro(SqlException ex) =>
        ex.Number switch
        {
            // 2627/2601: chave ou índice único (pk_mesas)
            2627 or 2601 =>
                "Já existe uma mesa com esse código. Atualize a página e tente cadastrar novamente.",
            // 547: CHECK de status ou FK do ambiente
            547 =>
                "Não foi possível cadastrar a mesa. O status ou o ambiente selecionado não é válido.",
            _ =>
                "Não foi possível cadastrar a mesa. Verifique os dados e o ambiente selecionado."
        };

    // Traduz erros comuns do SQL na edição para mensagens claras na tela.
    private static string MensagemErroSqlEdicao(SqlException ex) =>
        ex.Number switch
        {
            2627 or 2601 =>
                "Já existe uma mesa com esse código. Verifique o número e o ambiente e tente novamente.",
            547 =>
                "Não foi possível salvar. O status ou o ambiente selecionado não é válido.",
            _ =>
                "Não foi possível salvar. Verifique os dados, o ambiente e os vínculos com reservas."
        };

    private static bool StatusManualPermitido(string? status) =>
        status == "Livre" || status == "Ocupada";
}
