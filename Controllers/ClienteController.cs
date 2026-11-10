using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Data.SqlClient;
using System.Security.Claims;
using GestaoReservas.Models;

public class ClienteController : Controller
{
    // Esquema de cookie do funcionário
    private const string EsquemaFuncionario = "FuncionarioScheme";
    // Esquema de cookie do cliente
    private const string EsquemaCliente = "ClienteScheme";
    // Dependência IClienteRepository recebida pelo construtor
    private readonly IClienteRepository repository;
    // Dependência IReservaRepository: usada para bloquear exclusão com reservas
    private readonly IReservaRepository reservaRepository;

    // Construtor: recebe as dependências por injeção
    public ClienteController(IClienteRepository repository, IReservaRepository reservaRepository)
    {
        // Guarda a dependência repository recebida
        this.repository = repository;
        // Guarda a dependência reservaRepository recebida
        this.reservaRepository = reservaRepository;
    }

    [Authorize(AuthenticationSchemes = "FuncionarioScheme")]
    public ActionResult Index() => View("index", repository.Read());

    [AllowAnonymous]
    [HttpGet]
    public ActionResult Create() => View(new Cliente());

    [AllowAnonymous]
    [HttpPost]
    public async Task<ActionResult> Create(Cliente model)
    {
        // Verifica se o campo foi preenchido
        if (string.IsNullOrWhiteSpace(model.EmailPessoal))
            // Registra a mensagem de erro para a tela
            ModelState.AddModelError(nameof(model.EmailPessoal), "Informe o e-mail pessoal.");
        // Verifica se o campo foi preenchido
        if (string.IsNullOrWhiteSpace(model.Senha))
            // Registra a mensagem de erro para a tela
            ModelState.AddModelError(nameof(model.Senha), "Informe a senha.");
        // Impede cadastro com CPF já usado por outro cliente
        if (CpfJaCadastrado(model.Cpf, 0))
            // Mensagem clara para o formulário
            ModelState.AddModelError(nameof(model.Cpf), "Já existe um cliente cadastrado com este CPF.");
        // Se houver erro de validação, volta para o formulário
        if (!ModelState.IsValid) return View(model);
        // Tenta executar; erros são tratados abaixo
        try
        {
            // Chama o repositório: Create
            repository.Create(model);
            // Se o repositório não preencheu o Id, busca o cadastro recém-criado pelo CPF
            if (model.Id == 0)
            {
                // Localiza o cliente recém-gravado
                var criado = repository.Read().FirstOrDefault(c => CpfIguais(c.Cpf, model.Cpf));
                // Copia o Id encontrado para o modelo
                if (criado != null) model.Id = criado.Id;
            }
            // Funcionário que cadastra um cliente pela área do restaurante permanece logado e volta à listagem
            if (User?.HasClaim(ClaimTypes.Role, "Funcionario") == true)
                // Redireciona para a listagem de clientes
                return RedirectToAction(nameof(Index));
            // Cadastro público: autentica o novo cliente e segue para Nova reserva
            await HttpContext.SignOutAsync(EsquemaFuncionario);
            // Monta a identidade do cliente recém-cadastrado
            await HttpContext.SignInAsync(
                EsquemaCliente,
                new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, model.Id.ToString()),
                    new Claim(ClaimTypes.Name, model.Nome ?? ""),
                    new Claim(ClaimTypes.Role, "Cliente")
                ], EsquemaCliente)),
                new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5) });
            // Segue direto para a tela de Nova reserva
            return RedirectToAction("Create", "Reserva");
        }
        // Erro vindo do banco de dados
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(model);
        }
        catch (SqlException)
        {
            // Registra a mensagem de erro para a tela
            ModelState.AddModelError("", "Não foi possível cadastrar. Verifique os dados, duplicidades e a conexão com o banco.");
            // Mostra a view com o modelo
            return View(model);
        }
    }

    [Authorize(AuthenticationSchemes = "FuncionarioScheme")]
    [HttpGet]
    public ActionResult Edit([FromRoute] int id)
    {
        // Busca o registro no banco
        var model = repository.Read(id);
        // Se não encontrou o registro...
        if (model == null) return NotFound();
        // Mostra a view com o modelo
        return View(model);
    }

    [Authorize(AuthenticationSchemes = "FuncionarioScheme")]
    [HttpPost]
    public ActionResult Edit([FromRoute] int id, Cliente model)
    {
        // A rota identifica o registro; não permita trocar a identidade pelo formulário.
        model.Id = id;
        // Ignora a validação deste campo
        ModelState.Remove(nameof(model.Id));
        // Condição
        if (repository.Read(id) == null) return NotFound();
        // Verifica se o campo foi preenchido
        if (string.IsNullOrWhiteSpace(model.EmailPessoal))
            // Registra a mensagem de erro para a tela
            ModelState.AddModelError(nameof(model.EmailPessoal), "Informe o e-mail pessoal.");
        // Impede trocar o CPF para um que já pertence a outro cliente
        if (CpfJaCadastrado(model.Cpf, id))
            // Mensagem clara para o formulário
            ModelState.AddModelError(nameof(model.Cpf), "Já existe um cliente cadastrado com este CPF.");
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

    [Authorize(AuthenticationSchemes = "FuncionarioScheme")]
    [HttpGet]
    public ActionResult Details(int id)
    {
        // Busca o registro no banco
        var model = repository.Read(id);
        // Se não encontrou o registro...
        if (model == null) return NotFound();
        // Mostra a view com o modelo
        return View(model);
    }

    [Authorize(AuthenticationSchemes = "FuncionarioScheme")]
    [HttpGet]
    public ActionResult Delete(int id)
    {
        // Busca o registro no banco
        var model = repository.Read(id);
        // Se não encontrou o registro...
        if (model == null) return NotFound();
        // Avisa na tela de confirmação quando já existem reservas
        if (ClienteTemReservas(id))
            // Mensagem clara para o formulário
            ModelState.AddModelError("", "Não é possível excluir este cliente porque existem reservas vinculadas (mesmo canceladas).");
        // Mostra a view com o modelo
        return View(model);
    }

    [Authorize(AuthenticationSchemes = "FuncionarioScheme")]
    [HttpPost, ActionName("Delete")]
    public ActionResult DeleteConfirmed([FromRoute] int id)
    {
        // Busca o registro no banco
        var model = repository.Read(id);
        // Se não encontrou o registro...
        if (model == null) return NotFound();
        // Bloqueia a exclusão no app quando o cliente tem qualquer reserva (incluindo canceladas)
        if (ClienteTemReservas(id))
        {
            // Mensagem clara para o formulário
            ModelState.AddModelError("", "Não é possível excluir este cliente porque existem reservas vinculadas (mesmo canceladas).");
            // Mantém o cadastro e volta à tela de exclusão
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
        // Erro vindo do banco de dados
        catch (SqlException)
        {
            // Registra a mensagem de erro para a tela
            ModelState.AddModelError("", "Não foi possível excluir. O cadastro pode estar vinculado a outros registros; verifique também a conexão.");
            // Mostra a view com o modelo
            return View("Delete", model);
        }
    }

    // Indica se o CPF já pertence a outro cliente (idAtual = 0 no cadastro)
    private bool CpfJaCadastrado(string? cpf, int idAtual)
    {
        // Sem CPF não há o que comparar aqui (a validação [Required] cuida do vazio)
        if (string.IsNullOrWhiteSpace(cpf)) return false;
        // Percorre os clientes já cadastrados
        return repository.Read().Any(c => c.Id != idAtual && CpfIguais(c.Cpf, cpf));
    }

    // Compara CPFs só pelos dígitos, para aceitar com ou sem pontuação
    private static bool CpfIguais(string? a, string? b)
    {
        // Extrai apenas os números do primeiro valor
        var da = DigitosCpf(a);
        // Extrai apenas os números do segundo valor
        var db = DigitosCpf(b);
        // Sem dígitos não considera iguais
        if (da.Length == 0 || db.Length == 0) return false;
        // Compara as sequências numéricas
        return string.Equals(da, db, StringComparison.Ordinal);
    }

    // Deixa somente os dígitos do CPF
    private static string DigitosCpf(string? valor)
    {
        // Trata nulo como vazio
        if (string.IsNullOrWhiteSpace(valor)) return "";
        // Filtra só caracteres numéricos
        return new string(valor.Where(char.IsDigit).ToArray());
    }

    // Verifica se o cliente possui alguma reserva (ativa ou cancelada)
    private bool ClienteTemReservas(int clienteId)
    {
        // Qualquer reserva vinculada ao cliente impede a exclusão
        return reservaRepository.Read().Any(r => r.ClienteId == clienteId);
    }
}
