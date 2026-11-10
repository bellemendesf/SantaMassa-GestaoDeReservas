using GestaoReservas.Models;

// Antes: ao confirmar, a mesa virava "Reservada" no cadastro (bloqueio global).
// Agora a disponibilidade é por DATA (Reservas_Mesas). Este método só limpa
// status "Reservada" legado quando a reserva deixa de estar confirmada.
public static class MesaReservaSync
{
    public static void AplicarStatusReserva(
        IReservaRepository reservas,
        IMesaRepository mesas,
        Reserva reserva,
        string statusReserva)
    {
        // Confirmada: não marca a mesa como Reservada globalmente (outros dias continuam livres).
        if (statusReserva == "Confirmada")
            return;

        if (statusReserva is not ("Pendente" or "Cancelada"))
            return;

        foreach (var mesaId in reserva.MesaIds.Distinct())
        {
            var mesa = mesas.Read(mesaId);
            if (mesa == null) continue;
            // Só desfaz status legado "Reservada"; Ocupada (indisponível de fato) não é alterada.
            if (mesa.Status != "Reservada") continue;

            var outraConfirmada = reservas.Read().Any(r =>
                r.Id != reserva.Id &&
                r.Status == "Confirmada" &&
                r.MesaIds.Contains(mesaId));
            if (outraConfirmada) continue;

            mesa.Status = "Livre";
            mesas.Update(mesa);
        }
    }
}
