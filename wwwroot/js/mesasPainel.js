// Painel de mesas: filtros de ambiente e status só no navegador.
// Os filtros ficam no sessionStorage para sobreviver a Editar/Detalhes/Excluir e ao voltar.
(function () {
    // Chave única desta tela no sessionStorage do navegador.
    var chaveFiltros = "gestaoReservas.mesas.filtros";

    // Espera o HTML da listagem estar pronto.
    document.addEventListener("DOMContentLoaded", iniciarPainel);
    // Recupera os filtros também no voltar do navegador (página pode vir do cache).
    window.addEventListener("pageshow", function () {
        iniciarPainel();
    });

    // Liga os filtros uma vez e reaplica o que estava salvo.
    function iniciarPainel() {
        // Campo de filtro por ambiente.
        var filtroAmbiente = document.getElementById("filtro-ambiente");
        // Campo de filtro por status.
        var filtroStatus = document.getElementById("filtro-status");
        // Cartões de mesa renderizados na página.
        var cards = document.querySelectorAll(".mesa-card");
        // Aviso quando o filtro não encontra nenhuma mesa.
        var avisoVazio = document.getElementById("mesas-filtro-vazio");

        // Sem os selects da listagem, esta não é a aba de mesas.
        if (!filtroAmbiente && !filtroStatus) {
            return;
        }

        // Confere se o valor salvo ainda existe no <select> (o cadastro pode ter mudado).
        function opcaoExiste(select, valor) {
            if (!select) return false;
            // Vazio significa "todos" e sempre é válido.
            if (!valor) return true;
            return Array.prototype.some.call(select.options, function (opcao) {
                return opcao.value === valor;
            });
        }

        // Grava ambiente e status atuais para reaplicar ao voltar.
        function salvarFiltros() {
            try {
                sessionStorage.setItem(chaveFiltros, JSON.stringify({
                    ambiente: filtroAmbiente ? filtroAmbiente.value : "",
                    status: filtroStatus ? filtroStatus.value : ""
                }));
            } catch (e) {
                // sessionStorage pode estar bloqueado; o filtro da tela atual segue funcionando.
            }
        }

        // Restaura o que o usuário tinha escolhido antes de sair da listagem.
        function restaurarFiltros() {
            try {
                var bruto = sessionStorage.getItem(chaveFiltros);
                if (!bruto) return;
                var salvo = JSON.parse(bruto);
                var ambiente = (salvo && salvo.ambiente) ? String(salvo.ambiente) : "";
                var status = (salvo && salvo.status) ? String(salvo.status) : "";
                if (filtroAmbiente && opcaoExiste(filtroAmbiente, ambiente)) {
                    filtroAmbiente.value = ambiente;
                }
                if (filtroStatus && opcaoExiste(filtroStatus, status)) {
                    filtroStatus.value = status;
                }
            } catch (e) {
                // JSON inválido ou storage indisponível: segue com os filtros padrão.
            }
        }

        // Aplica os filtros escolhidos e esconde os cartões que não batem.
        function aplicarFiltros() {
            // Valor atual do ambiente (vazio = todos).
            var ambiente = filtroAmbiente ? filtroAmbiente.value : "";
            // Valor atual do status (vazio = todos).
            var status = filtroStatus ? filtroStatus.value : "";
            // Quantos cartões ficaram visíveis.
            var visiveis = 0;

            // Percorre cada mesa da grade.
            cards.forEach(function (card) {
                // Ambiente gravado no cartão.
                var cardAmbiente = card.getAttribute("data-ambiente") || "";
                // Status gravado no cartão.
                var cardStatus = card.getAttribute("data-status") || "";
                // Combina os dois critérios (e lógico).
                var bateAmbiente = !ambiente || cardAmbiente === ambiente;
                var bateStatus = !status || cardStatus === status;
                var mostrar = bateAmbiente && bateStatus;
                // hidden é o jeito nativo de ocultar o cartão.
                card.hidden = !mostrar;
                // Soma só os que continuam na tela.
                if (mostrar) {
                    visiveis += 1;
                }
            });

            // Mostra o aviso apenas quando o filtro zera a lista.
            if (avisoVazio) {
                avisoVazio.hidden = visiveis > 0;
            }
        }

        // Recupera os filtros salvos (voltar, cancelar, pós-salvar ou F5 na listagem).
        restaurarFiltros();
        aplicarFiltros();

        // Evita ligar os eventos mais de uma vez (DOMContentLoaded + pageshow).
        if (document.documentElement.getAttribute("data-mesas-painel-ok") === "1") {
            return;
        }
        document.documentElement.setAttribute("data-mesas-painel-ok", "1");

        // Atualiza a grade e guarda a escolha ao trocar o ambiente.
        if (filtroAmbiente) {
            filtroAmbiente.addEventListener("change", function () {
                salvarFiltros();
                aplicarFiltros();
            });
        }
        // Atualiza a grade e guarda a escolha ao trocar o status.
        if (filtroStatus) {
            filtroStatus.addEventListener("change", function () {
                salvarFiltros();
                aplicarFiltros();
            });
        }
    }
})();
