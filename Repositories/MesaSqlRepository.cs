// Importa os modelos do projeto
using GestaoReservas.Models;
// Importa as classes de acesso ao SQL Server
using Microsoft.Data.SqlClient;

// Repositório que grava e lê Mesa no SQL Server
public class MesaSqlRepository : DataConnection, IMesaRepository
{
    // Construtor: repassa a configuração para a classe base
    public MesaSqlRepository(IConfiguration configuration) : base(configuration) { }

   // Insere um novo registro no banco
   public void Create(Mesa mesa)
{
    // Abre a conexão com o banco
    conn.Open();
    // Transação: gera o nr e impede número repetido no mesmo ambiente
    using var transacao = conn.BeginTransaction();
    // Tenta executar; erros são tratados abaixo
    try
    {
        // Recusa número de exibição já usado neste ambiente
        if (NumeroExisteNoAmbiente(mesa.Numero, mesa.AmbienteId, null, transacao))
        {
            throw new InvalidOperationException(
                $"Já existe a mesa {mesa.Numero} neste ambiente.");
        }

        // nr é a chave interna (não o número exibido). Gera o próximo valor livre, como em Ambiente.
        using var cmd = new SqlCommand(@"
            DECLARE @NovoNr int = (SELECT ISNULL(MAX(nr), 0) + 1 FROM mesas WITH (UPDLOCK, HOLDLOCK));
            INSERT INTO mesas
                (nr, numero, qtd_lugares, status, codigo_ambientes)
            VALUES
                (@NovoNr, @Numero, @QtdLugares, @Status, @AmbienteId);
            SELECT @NovoNr;", conn, transacao);

        // Preenche o parâmetro @Numero (número digitado pelo usuário)
        cmd.Parameters.AddWithValue("@Numero", mesa.Numero);
        // Preenche o parâmetro @QtdLugares (evita SQL injection)
        cmd.Parameters.AddWithValue("@QtdLugares", mesa.QtdLugares);
        // Preenche o parâmetro @Status (evita SQL injection)
        cmd.Parameters.AddWithValue("@Status",
            mesa.Status == "Livre" ? "Disponivel" : mesa.Status);
        // Preenche o parâmetro @AmbienteId (evita SQL injection)
        cmd.Parameters.AddWithValue("@AmbienteId", mesa.AmbienteId);

        // Guarda o nr gerado no modelo (é o id das rotas e do mapa)
        mesa.Id = Convert.ToInt32(cmd.ExecuteScalar());

        // Confirma a transação
        transacao.Commit();
    }
    catch
    {
        // Desfaz a transação em caso de erro
        transacao.Rollback();
        throw;
    }
    // Sempre executa, com ou sem erro
    finally
    {
        // Fecha a conexão com o banco
        conn.Close();
    }
}

// Confere se o número de exibição já pertence a outra mesa do mesmo ambiente.
// ignorarNr: na edição, a própria mesa (chave nr) é ignorada.
private bool NumeroExisteNoAmbiente(int numero, int ambienteId, int? ignorarNr, SqlTransaction transacao)
{
    using var verificar = new SqlCommand(@"
        SELECT COUNT(*)
        FROM mesas WITH (UPDLOCK, HOLDLOCK)
        WHERE numero = @Numero
          AND codigo_ambientes = @AmbienteId
          AND (@IgnorarNr IS NULL OR nr <> @IgnorarNr);", conn, transacao);
    verificar.Parameters.AddWithValue("@Numero", numero);
    verificar.Parameters.AddWithValue("@AmbienteId", ambienteId);
    verificar.Parameters.AddWithValue("@IgnorarNr", (object?)ignorarNr ?? DBNull.Value);
    return (int)verificar.ExecuteScalar()! > 0;
}

// Lê todos os registros do banco
public List<Mesa> Read()
{
    // Lista que receberá os Mesa
    var mesas = new List<Mesa>();

    // Cria o comando SQL (descartado ao final)
    using var cmd = new SqlCommand();
    // Associa o comando à conexão
    cmd.Connection = conn;

    // Texto SQL que será executado
    cmd.CommandText = @"
        SELECT
            m.nr,
            m.numero,
            m.qtd_lugares,
            CASE
                WHEN m.status = 'Disponivel' THEN 'Livre'
                ELSE m.status
            END AS status,
            COALESCE(m.codigo_ambientes, 0),
            COALESCE(a.nome, 'Sem ambiente')
        FROM mesas m
        LEFT JOIN ambientes a
            ON m.codigo_ambientes = a.codigo
        ORDER BY m.numero;";

    // Abre a conexão com o banco
    conn.Open();

    // Tenta executar; erros são tratados abaixo
    try
    {
        // Executa a consulta e abre o leitor de resultados
        using SqlDataReader reader = cmd.ExecuteReader();

        // Percorre cada linha retornada
        while (reader.Read())
        {
            // Adiciona um Mesa à lista
            mesas.Add(new Mesa
            {
                // Lê a coluna 0 para Id
                Id = reader.GetInt32(0),
                // Lê a coluna 1 para Numero
                Numero = reader.GetInt32(1),
                // Lê a coluna 2 para QtdLugares
                QtdLugares = reader.GetInt32(2),
                // Lê a coluna 3 para Status
                Status = reader.GetString(3),
                // Lê a coluna 4 para AmbienteId
                AmbienteId = reader.GetInt32(4),
                // Lê a coluna 5 para AmbienteNome
                AmbienteNome = reader.GetString(5)
            });
        }
    }
    // Sempre executa, com ou sem erro
    finally
    {
        // Fecha a conexão com o banco
        conn.Close();
    }

    // Devolve o resultado
    return mesas;
}

   // Lê um registro pelo id (null se não existir)
   public Mesa? Read(int id)
{
    // mesa começa nulo (não encontrado)
    Mesa? mesa = null;

    // Cria o comando SQL (descartado ao final)
    using var cmd = new SqlCommand();
    // Associa o comando à conexão
    cmd.Connection = conn;

    // Texto SQL que será executado
    cmd.CommandText = @"
        SELECT
            nr,
            numero,
            qtd_lugares,
            CASE
                WHEN status = 'Disponivel' THEN 'Livre'
                ELSE status
            END,
            COALESCE(codigo_ambientes, 0)
        FROM mesas
        WHERE nr = @Id;";

    // Preenche o parâmetro @Id (evita SQL injection)
    cmd.Parameters.AddWithValue("@Id", id);

    // Abre a conexão com o banco
    conn.Open();

    // Tenta executar; erros são tratados abaixo
    try
    {
        // Executa a consulta e abre o leitor de resultados
        using SqlDataReader reader = cmd.ExecuteReader();

        // Se encontrou a linha, monta o objeto
        if (reader.Read())
        {
            // Monta o objeto Mesa com os dados da linha
            mesa = new Mesa
            {
                // Lê a coluna 0 para Id
                Id = reader.GetInt32(0),
                // Lê a coluna 1 para Numero
                Numero = reader.GetInt32(1),
                // Lê a coluna 2 para QtdLugares
                QtdLugares = reader.GetInt32(2),
                // Lê a coluna 3 para Status
                Status = reader.GetString(3),
                // Lê a coluna 4 para AmbienteId
                AmbienteId = reader.GetInt32(4)
            };
        }
    }
    // Sempre executa, com ou sem erro
    finally
    {
        // Fecha a conexão com o banco
        conn.Close();
    }

    // Devolve o resultado
    return mesa;
}

  // Atualiza um registro existente no banco
  public void Update(Mesa mesa)
{
    // Abre a conexão com o banco
    conn.Open();
    // Transação: a checagem de número repetido ignora a própria mesa (nr)
    using var transacao = conn.BeginTransaction();
    // Tenta executar; erros são tratados abaixo
    try
    {
        // Recusa número de exibição já usado por outra mesa neste ambiente
        if (NumeroExisteNoAmbiente(mesa.Numero, mesa.AmbienteId, mesa.Id, transacao))
        {
            throw new InvalidOperationException(
                $"Já existe a mesa {mesa.Numero} neste ambiente.");
        }

        // Atualiza pelo nr (id da rota), preservando o número de exibição em numero
        using var cmd = new SqlCommand(@"
            UPDATE mesas
            SET numero = @Numero,
                qtd_lugares = @QtdLugares,
                status = @Status,
                codigo_ambientes = @AmbienteId
            WHERE nr = @Id;", conn, transacao);

        // Preenche o parâmetro @Numero (evita SQL injection)
        cmd.Parameters.AddWithValue("@Numero", mesa.Numero);
        // Preenche o parâmetro @QtdLugares (evita SQL injection)
        cmd.Parameters.AddWithValue("@QtdLugares", mesa.QtdLugares);
        // Preenche o parâmetro @Status (evita SQL injection)
        cmd.Parameters.AddWithValue("@Status",
            mesa.Status == "Livre" ? "Disponivel" : mesa.Status);
        // Preenche o parâmetro @AmbienteId (evita SQL injection)
        cmd.Parameters.AddWithValue("@AmbienteId", mesa.AmbienteId);
        // Preenche o parâmetro @Id (chave nr da mesa)
        cmd.Parameters.AddWithValue("@Id", mesa.Id);

        // Executa o comando (INSERT/UPDATE/DELETE)
        cmd.ExecuteNonQuery();

        // Confirma a transação
        transacao.Commit();
    }
    catch
    {
        // Desfaz a transação em caso de erro
        transacao.Rollback();
        throw;
    }
    // Sempre executa, com ou sem erro
    finally
    {
        // Fecha a conexão com o banco
        conn.Close();
    }
}
   // Remove (ou cancela) um registro no banco
   public void Delete(int id)
{
    // Abre uma conexão própria para a transação
    using var transactionConnection = new SqlConnection(
        conn.ConnectionString);

    // Abre a conexão da transação
    transactionConnection.Open();

    // Inicia a transação
    using var transaction = transactionConnection.BeginTransaction();

    // Tenta executar; erros são tratados abaixo
    try
    {
        // Qualquer vínculo em Reservas_Mesas (inclusive canceladas) impede a exclusão — preserva o histórico.
        using (var verificar = new SqlCommand(@"
            SELECT COUNT(*)
            FROM Reservas_Mesas rm
            WHERE rm.nr_mesas = @Id;", transactionConnection, transaction))
        {
            verificar.Parameters.AddWithValue("@Id", id);
            int vinculos = (int)verificar.ExecuteScalar()!;
            if (vinculos > 0)
            {
                throw new InvalidOperationException(
                    "Não é possível excluir esta mesa porque existem reservas vinculadas (mesmo canceladas). Se quiser indisponibilizá-la, altere o status para Ocupada.");
            }
        }

        // Sem vínculos: pode excluir a mesa.
        using (var excluirMesa = new SqlCommand(
            "DELETE FROM mesas WHERE nr = @Id;",
            transactionConnection, transaction))
        {
            // Preenche o parâmetro @Id
            excluirMesa.Parameters.AddWithValue("@Id", id);
            // Executa o comando excluirMesa
            excluirMesa.ExecuteNonQuery();
        }

        // Confirma a transação
        transaction.Commit();
    }
    // Qualquer erro
    catch
    {
        // Desfaz a transação em caso de erro
        transaction.Rollback();
        // Repassa o erro
        throw;
    }
} }