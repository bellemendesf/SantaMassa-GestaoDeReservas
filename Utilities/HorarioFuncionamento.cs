using System.Globalization;

// Grade de horários da reserva: 18:30 às 23:30, de 30 em 30 minutos.
// Um único lugar para a view e para a validação do servidor.
// A coluna data_hora já é datetime: o horário entra junto com a data, sem mudar o banco.
public static class HorarioFuncionamento
{
    // Abertura do restaurante (primeiro horário que o cliente pode escolher)
    public static readonly TimeOnly Abertura = new(18, 30);

    // Encerramento (último horário que o cliente pode escolher)
    public static readonly TimeOnly Encerramento = new(23, 30);

    // Intervalo entre as opções do select
    public static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(30);

    // Lista única das opções (18:30, 19:00, ..., 23:30)
    public static IReadOnlyList<TimeOnly> Horarios { get; } = MontarLista();

    // Monta a lista a partir da abertura até o encerramento
    private static IReadOnlyList<TimeOnly> MontarLista()
    {
        var lista = new List<TimeOnly>();
        var hora = Abertura;
        while (true)
        {
            lista.Add(hora);
            // Chegou no último horário da grade
            if (hora >= Encerramento) break;
            var proxima = hora.Add(Intervalo);
            // TimeOnly.Add dá a volta à meia-noite; se isso acontecer, paramos
            if (proxima <= hora) break;
            hora = proxima;
        }
        return lista;
    }

    // True se o horário está na grade oficial
    public static bool EhHorarioDaGrade(TimeOnly horario) => Horarios.Contains(horario);

    // Lê o texto do select (HH:mm). Null se estiver vazio ou fora da grade.
    public static TimeOnly? Parse(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        if (TimeOnly.TryParse(texto, CultureInfo.InvariantCulture, out var hora) && EhHorarioDaGrade(hora))
            return hora;
        return null;
    }

    // Junta a data escolhida com o horário da grade (a data do input type=date vem à meia-noite)
    public static DateTime Combinar(DateTime data, TimeOnly horario) => data.Date.Add(horario.ToTimeSpan());

    // True se a reserva tem um horário da grade (reservas antigas costumam ficar à meia-noite)
    public static bool TemHorarioDefinido(DateTime dataHora)
        => EhHorarioDaGrade(TimeOnly.FromTimeSpan(dataHora.TimeOfDay));

    // Texto para telas: "dd/MM/yyyy às 19:00" ou só a data se não houver horário
    public static string Formatar(DateTime dataHora)
    {
        var dia = dataHora.ToString("dd/MM/yyyy");
        if (!TemHorarioDefinido(dataHora)) return dia;
        return dia + " às " + dataHora.ToString("HH:mm");
    }

    // Null = ok. Confere a grade e, se a reserva for hoje, que o horário ainda não passou.
    public static string? Validar(DateTime dataHora, DateTime agora)
    {
        var hora = TimeOnly.FromTimeSpan(dataHora.TimeOfDay);
        if (!EhHorarioDaGrade(hora))
            return "Escolha um horário entre 18:30 e 23:30, de 30 em 30 minutos.";
        if (dataHora.Date == agora.Date && dataHora <= agora)
            return "Esse horário já passou. Escolha outro horário desta noite.";
        return null;
    }
}
