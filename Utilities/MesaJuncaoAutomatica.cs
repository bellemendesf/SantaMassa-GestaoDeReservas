using GestaoReservas.Models;

// Junta mesas automaticamente quando a capacidade das escolhidas não cobre a quantidade de pessoas.
// Só usa mesas LIVRES do MESMO ambiente. Não altera o banco: só monta a lista de MesaIds.
public static class MesaJuncaoAutomatica
{
    // Resultado da junção: mesas finais, mesas que o sistema acrescentou e mensagem de erro (se houver).
    public sealed class Resultado
    {
        public List<int> MesaIds { get; init; } = new();
        public List<Mesa> Adicionadas { get; init; } = new();
        public string? Erro { get; init; }
        public bool Ok => string.IsNullOrEmpty(Erro);
    }

    // Completa as mesas escolhidas até cobrir qtdPessoas, usando só mesas livres do mesmo ambiente.
    public static Resultado Completar(
        IReadOnlyList<int> escolhidas,
        int qtdPessoas,
        IEnumerable<Mesa> todasMesas,
        HashSet<int> reservadasNoDia)
    {
        var porId = todasMesas.ToDictionary(m => m.Id);
        var finais = escolhidas.Where(id => id > 0).Distinct().ToList();
        var adicionadas = new List<Mesa>();

        if (qtdPessoas < 1)
            return new Resultado { MesaIds = finais, Erro = "Informe ao menos uma pessoa." };

        if (finais.Count == 0)
            return new Resultado { MesaIds = finais, Erro = "Escolha ao menos uma mesa no mapa." };

        // Confere se todas as escolhidas existem
        foreach (var id in finais)
        {
            if (!porId.ContainsKey(id))
                return new Resultado { MesaIds = finais, Erro = $"A mesa {id} não está cadastrada." };
        }

        // Mesas de ambientes diferentes não podem entrar na mesma reserva
        var ambientes = finais.Select(id => porId[id].AmbienteId).Distinct().ToList();
        if (ambientes.Count > 1)
        {
            return new Resultado
            {
                MesaIds = finais,
                Erro = "Escolha mesas do mesmo ambiente. Não é permitido misturar ambientes na mesma reserva."
            };
        }

        int ambienteId = ambientes[0];
        string ambienteNome = porId[finais[0]].AmbienteNome;
        if (string.IsNullOrWhiteSpace(ambienteNome))
            ambienteNome = "selecionado";

        int Soma() => finais.Sum(id => porId[id].QtdLugares);

        // Já cabe: não acrescenta nada (não remove mesas escolhidas).
        if (Soma() >= qtdPessoas)
            return new Resultado { MesaIds = finais, Adicionadas = adicionadas };

        // Números de referência para preferir mesas vizinhas no mesmo ambiente.
        var numerosEscolhidos = finais.Select(id => porId[id].Numero).ToList();

        while (Soma() < qtdPessoas)
        {
            int faltam = qtdPessoas - Soma();
            // SOMENTE mesas livres do mesmo ambiente (sem fallback para outros).
            var pool = porId.Values
                .Where(m => !finais.Contains(m.Id)
                    && m.AmbienteId == ambienteId
                    // Só Ocupada fecha a mesa em qualquer dia; Reservada no cadastro não bloqueia.
                    && !string.Equals(m.Status, "Ocupada", StringComparison.OrdinalIgnoreCase)
                    && !reservadasNoDia.Contains(m.Id))
                .ToList();

            if (pool.Count == 0)
            {
                return new Resultado
                {
                    MesaIds = finais,
                    Adicionadas = adicionadas,
                    Erro = $"Não há mesas livres suficientes no ambiente {ambienteNome} para {qtdPessoas} pessoas nesta data. Escolha outra data ou ambiente."
                };
            }

            // Guloso: cabe com menor sobra; senão maior capacidade; empate: número vizinho.
            var queCabem = pool.Where(m => m.QtdLugares >= faltam).ToList();
            var escolhida = (queCabem.Count > 0
                    ? queCabem.OrderBy(m => m.QtdLugares)
                    : pool.OrderByDescending(m => m.QtdLugares))
                .ThenBy(m => numerosEscolhidos.Min(n => Math.Abs(n - m.Numero)))
                .ThenBy(m => m.Numero)
                .ThenBy(m => m.Id)
                .First();

            finais.Add(escolhida.Id);
            adicionadas.Add(escolhida);
            numerosEscolhidos.Add(escolhida.Numero);
        }

        return new Resultado { MesaIds = finais, Adicionadas = adicionadas };
    }

    // True se dá para tirar alguma mesa e ainda caber todas as pessoas (mesa extra desnecessária).
    public static bool TemMesasDesnecessarias(
        IReadOnlyList<int> mesaIds,
        int qtdPessoas,
        IEnumerable<Mesa> todasMesas)
    {
        var porId = todasMesas.ToDictionary(m => m.Id);
        var ids = mesaIds.Where(id => id > 0 && porId.ContainsKey(id)).Distinct().ToList();
        if (ids.Count <= 1) return false;

        int soma = ids.Sum(id => porId[id].QtdLugares);
        if (soma < qtdPessoas) return false;

        // Se remover qualquer uma e ainda couber, há mesa a mais.
        foreach (var id in ids)
        {
            if (soma - porId[id].QtdLugares >= qtdPessoas)
                return true;
        }
        return false;
    }

    // Texto amigável: "Juntamos a mesa 3 (Interno) para caber 4 pessoas."
    public static string MensagemJuncao(IEnumerable<Mesa> adicionadas, int qtdPessoas)
    {
        var lista = adicionadas.ToList();
        if (lista.Count == 0) return "";
        if (lista.Count == 1)
        {
            var m = lista[0];
            return $"Juntamos a mesa {m.Numero} ({m.AmbienteNome}) para caber {qtdPessoas} pessoas.";
        }
        var nomes = string.Join(", ", lista.Select(m => $"mesa {m.Numero} ({m.AmbienteNome})"));
        return $"Juntamos as {nomes} para caber {qtdPessoas} pessoas.";
    }
}