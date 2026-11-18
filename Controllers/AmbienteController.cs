// Importa o [Authorize] (exigir login)
using Microsoft.AspNetCore.Authorization;
// Importa as classes do ASP.NET MVC (Controller, ActionResult...)
using Microsoft.AspNetCore.Mvc;
// Importa as classes de acesso ao SQL Server
using Microsoft.Data.SqlClient;
// Importa os modelos do projeto
using GestaoReservas.Models;

// Controller responsável pelas telas de Ambiente
// Gerenciar Ambientes é tarefa do restaurante: exige login de funcionário (mesmo padrão dos outros controllers do restaurante)
[Authorize(AuthenticationSchemes = "FuncionarioScheme")]
public class AmbienteController : Controller
{
    // Dependência IAmbienteRepository recebida pelo construtor
    private readonly IAmbienteRepository repository;
    // Usado só para explicar por que a exclusão é bloqueada quando há mesas.
    private readonly IMesaRepository mesaRepository;

    // Construtor: recebe as dependências por injeção
    public AmbienteController(IAmbienteRepository repository, IMesaRepository mesaRepository)
    {
        // Guarda a dependência repository recebida
        this.repository = repository;
        // Guarda o repositório de mesas para contar vínculos.
        this.mesaRepository = mesaRepository;
    }

    // Quantas mesas estão cadastradas neste ambiente.
    private int ContarMesasDoAmbiente(int ambienteId) =>
        mesaRepository.Read().Count(m => m.AmbienteId == ambienteId);

    // Texto claro: por que não dá para excluir e o que fazer.
    private string MensagemExclusaoAmbiente(Ambiente model)
    {
        var qtd = ContarMesasDoAmbiente(model.Id);
        var nome = string.IsNullOrWhiteSpace(model.Nome) ? "selecionado" : model.Nome.Trim();
        if (qtd == 1)
            return $"Não é possível excluir o ambiente {nome} porque ele tem 1 mesa cadastrada. Exclua ou mova a mesa para outro ambiente antes de tentar novamente.";
        if (qtd > 1)
            return $"Não é possível excluir o ambiente {nome} porque ele tem {qtd} mesas cadastradas. Exclua ou mova as mesas para outro ambiente antes de tentar novamente.";
        return $"Não foi possível excluir o ambiente {nome}. Ele pode estar vinculado a outros registros. O ambiente não foi excluído. Verifique os cadastros relacionados e tente novamente.";
    }

    // Ação Index: mostra a listagem
    public ActionResult Index() => View("index", repository.Read());

    // Ação atendida por requisições GET (abrir a tela)
    [HttpGet]
    // Ação Create (GET): abre o formulário vazio
    public ActionResult Create() => View(new Ambiente { Status = "Disponivel" });

    // Ação atendida por requisições POST (envio do formulário)
    [HttpPost]
    // Ação Create (POST): valida e grava o cadastro
    public ActionResult Create(Ambiente model)
    {
        // Se houver erro de validação, volta para o formulário
        if (!ModelState.IsValid) return View(model);
        // Tenta executar; erros são tratados abaixo
        try
        {
            // Chama o repositório: Create
            repository.Create(model);
            // Redireciona para outra tela
            return RedirectToAction(nameof(Index));
        }
        // Erro vindo do banco de dados
        catch (SqlException)
        {
            // Registra a mensagem de erro para a tela
            ModelState.AddModelError("", "Não foi possível cadastrar. Verifique os dados, duplicidades e a conexão com o banco.");
            // Mostra a view com o modelo
            return View(model);
        }
    }

    // Ação atendida por requisições GET (abrir a tela)
    [HttpGet]
    // Ação Edit (GET): abre o formulário de edição
    public ActionResult Edit([FromRoute] int id)
    {
        // Busca o registro no banco
        var model = repository.Read(id);
        // Se não encontrou o registro...
        if (model == null) return NotFound();
        // Mostra a view com o modelo
        return View(model);
    }

    // Ação atendida por requisições POST (envio do formulário)
    [HttpPost]
    // Ação Edit (POST): valida e salva as alterações
    public ActionResult Edit([FromRoute] int id, Ambiente model)
    {
        // A rota identifica o registro; não permita trocar a identidade pelo formulário.
        model.Id = id;
        // Ignora a validação deste campo
        ModelState.Remove(nameof(model.Id));
        // Condição
        if (repository.Read(id) == null) return NotFound();
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
            ModelState.AddModelError("", "Não foi possível salvar. Verifique os dados, duplicidades e vínculos existentes.");
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
        if (model == null) return NotFound();
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
        if (model == null) return NotFound();
        // Já avisa na confirmação quando o ambiente tem mesas.
        if (ContarMesasDoAmbiente(id) > 0)
            ModelState.AddModelError("", MensagemExclusaoAmbiente(model));
        // Mostra a view com o modelo
        return View(model);
    }

    // Ação POST que responde pela rota Delete
    [HttpPost, ActionName("Delete")]
    // Ação que executa a exclusão confirmada
    public ActionResult DeleteConfirmed([FromRoute] int id)
    {
        // Busca o registro no banco
        var model = repository.Read(id);
        // Se não encontrou o registro...
        if (model == null) return NotFound();
        // Mesma regra de sempre: ambiente com mesa não é excluído (evita erro 500 de FK).
        if (ContarMesasDoAmbiente(id) > 0)
        {
            ModelState.AddModelError("", MensagemExclusaoAmbiente(model));
            return View("Delete", model);
        }
        // Tenta executar; erros são tratados abaixo
        try
        {
            // Chama o repositório: Delete
            repository.Delete(id);
            // Redireciona para outra tela
            return RedirectToAction(nameof(Index));
        }
        // Erro vindo do banco de dados (ex.: vínculo que a contagem não viu)
        catch (SqlException)
        {
            // Registra a mensagem de erro para a tela
            ModelState.AddModelError("", MensagemExclusaoAmbiente(model));
            // Mostra a view com o modelo
            return View("Delete", model);
        }
    }
}
