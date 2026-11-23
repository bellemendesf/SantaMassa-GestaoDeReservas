// Validação de formulários no navegador (CPF, telefone, nome, quantidade de pessoas e e-mail institucional).
// As mensagens são sempre em português e aparecem antes do envio.
(function (global) {
    "use strict";

    // Textos que não pertencem ao formulário de reserva (vêm do modelo Cliente)
    var mensagensClienteIndevidas = [
        "Informe o telefone.",
        "Informe o nome.",
        "Informe o CPF."
    ];

    // Deixa somente os dígitos de um texto
    function soDigitos(texto) {
        return String(texto || "").replace(/\D+/g, "");
    }

    // Conta letras (incluindo acentuadas)
    function contarLetras(texto) {
        var m = String(texto || "").match(/\p{L}/gu);
        return m ? m.length : 0;
    }

    // Monta o CPF no formato 000.000.000-00
    function formatarCpf(texto) {
        var d = soDigitos(texto).slice(0, 11);
        if (d.length > 9) return d.slice(0, 3) + "." + d.slice(3, 6) + "." + d.slice(6, 9) + "-" + d.slice(9);
        if (d.length > 6) return d.slice(0, 3) + "." + d.slice(3, 6) + "." + d.slice(6);
        if (d.length > 3) return d.slice(0, 3) + "." + d.slice(3);
        return d;
    }

    // Monta o telefone no formato (00) 0000-0000 ou (00) 00000-0000
    function formatarTelefone(texto) {
        var d = soDigitos(texto).slice(0, 11);
        if (d.length === 0) return "";
        if (d.length <= 2) return "(" + d;
        if (d.length <= 6) return "(" + d.slice(0, 2) + ") " + d.slice(2);
        if (d.length <= 10) return "(" + d.slice(0, 2) + ") " + d.slice(2, 6) + "-" + d.slice(6);
        return "(" + d.slice(0, 2) + ") " + d.slice(2, 7) + "-" + d.slice(7);
    }

    // Mantém só letras, espaços, apóstrofo e hífen no nome
    function filtrarNome(texto) {
        return String(texto || "").replace(/[^\p{L} '\u2019\-]/gu, "");
    }

    // Confere o CPF (11 dígitos; pontuação é aceita)
    function erroCpf(valor) {
        if (!String(valor || "").trim()) return "Informe o CPF.";
        if (/[^0-9.\-\s]/.test(valor)) return "O CPF deve conter apenas números. Você pode digitar com ou sem pontuação (000.000.000-00).";
        var d = soDigitos(valor);
        if (d.length !== 11) return "O CPF deve ter 11 dígitos.";
        return "";
    }

    // Confere o telefone (10 ou 11 dígitos)
    function erroTelefone(valor) {
        if (!String(valor || "").trim()) return "Informe o telefone.";
        if (/[^0-9()\s\-]/.test(valor)) return "O telefone deve conter apenas números. Você pode usar parênteses, espaço e hífen.";
        var d = soDigitos(valor);
        if (d.length < 10 || d.length > 11) return "O telefone deve ter 10 ou 11 dígitos (com DDD).";
        return "";
    }

    // Confere o nome de pessoa
    function erroNome(valor) {
        var nome = String(valor || "").trim();
        if (!nome) return "Informe o nome.";
        if (nome.length > 50) return "O nome deve ter no máximo 50 caracteres.";
        if (contarLetras(nome) < 3) return "O nome deve ter pelo menos 3 letras.";
        if (!/^[\p{L}]+(?:[ '\u2019\-][\p{L}]+)*$/u.test(nome)) {
            return "O nome deve conter apenas letras (com acentos), espaços, apóstrofo e hífen. Não use números nem símbolos.";
        }
        return "";
    }

    // Confere a quantidade de pessoas (usa min/max do próprio campo)
    function erroQtd(campo) {
        var bruto = String(campo.value || "").trim();
        var min = parseInt(campo.getAttribute("min") || "1", 10);
        var max = parseInt(campo.getAttribute("max") || "30", 10);
        if (min !== min) min = 1;
        if (max !== max) max = 30;
        if (!bruto) return "Informe a quantidade de pessoas.";
        if (!/^\d+$/.test(bruto)) return "Informe entre " + min + " e " + max + " pessoas.";
        var n = parseInt(bruto, 10);
        if (n < min || n > max) return "Informe entre " + min + " e " + max + " pessoas.";
        return "";
    }

    // Login (e edição sem mudar o e-mail): @santamassa e formato HTML5 (sem pontos consecutivos no domínio)
    var trechoDominioEmail = "@santamassa";
    var sufixoCadastroEmail = "@santamassa.com.br";
    var mensagemDominioEmail = "O e-mail institucional deve conter @santamassa (ex.: nome@santamassa.com.br).";
    var mensagemFormatoEmail = "Email inválido: use apenas um ponto antes do .com, ex.: nome@santamassa.com.br";
    var mensagemSufixoCadastroEmail = "O email do funcionário deve terminar em @santamassa.com.br (ex.: nome@santamassa.com.br).";
    var emailNoFormatoHtml5 = /^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$/;

    // Na edição, o sufixo .com.br só é exigido se o e-mail institucional tiver sido alterado
    function cadastroExigeSufixoBr(campo) {
        if (!campo || typeof campo.getAttribute !== "function") return true;
        var original = campo.getAttribute("data-email-original");
        // Sem atributo (cadastro): sempre exige @santamassa.com.br
        if (original === null) return true;
        var atual = String(campo.value || "").trim().toLowerCase();
        return atual !== String(original).trim().toLowerCase();
    }

    function erroEmailInstitucional(valor, campo) {
        var email = String(valor || "").trim();
        if (!email) return "Informe o e-mail institucional.";
        var dominio = email.slice(email.lastIndexOf("@") + 1);
        var formatoInvalido = dominio.indexOf("..") >= 0 || dominio.charAt(0) === "." || dominio.charAt(dominio.length - 1) === "." || !emailNoFormatoHtml5.test(email);
        if (formatoInvalido) {
            // Recusa ..com, santamassa..com e typos do tipo @.santamassa
            if (email.toLowerCase().indexOf("santamassa") >= 0) return mensagemFormatoEmail;
            // No cadastro (ou e-mail alterado) a mensagem principal pede o sufixo .com.br
            if (cadastroExigeSufixoBr(campo)) return mensagemSufixoCadastroEmail;
            return mensagemDominioEmail;
        }
        // E-mail inalterado na edição: mantém a regra antiga (aceita @santamassa.com)
        if (!cadastroExigeSufixoBr(campo)) {
            if (email.toLowerCase().indexOf(trechoDominioEmail) < 0) return mensagemDominioEmail;
            return "";
        }
        // Cadastro ou e-mail alterado: precisa terminar em @santamassa.com.br
        if (email.toLowerCase().lastIndexOf(sufixoCadastroEmail) !== email.length - sufixoCadastroEmail.length) {
            return mensagemSufixoCadastroEmail;
        }
        return "";
    }

    // Localiza (ou cria) o span de erro do campo
    function obterSpan(campo) {
        var id = campo.id || campo.getAttribute("name") || "";
        var span = document.getElementById(id + "-erro");
        if (!span && campo.parentElement) {
            span = campo.parentElement.querySelector('[data-erro-para="' + id + '"]');
        }
        if (!span && campo.parentElement) {
            span = document.createElement("span");
            span.id = id + "-erro";
            span.className = "erro-campo";
            span.setAttribute("data-erro-para", id);
            campo.parentElement.appendChild(span);
        }
        return span;
    }

    // Mostra ou limpa a mensagem do campo (também no setCustomValidity)
    function mostrarErro(campo, mensagem) {
        campo.setCustomValidity(mensagem || "");
        var span = obterSpan(campo);
        if (!span) return;
        span.textContent = mensagem || "";
        if (mensagem) span.removeAttribute("hidden");
        else span.setAttribute("hidden", "hidden");
    }

    // Valida um campo de acordo com o tipo
    function validarCampo(campo) {
        var tipo = (campo.getAttribute("data-valida") || "").toLowerCase();
        var erro = "";
        if (tipo === "cpf") erro = erroCpf(campo.value);
        else if (tipo === "telefone") erro = erroTelefone(campo.value);
        else if (tipo === "nome") erro = erroNome(campo.value);
        else if (tipo === "qtd-pessoas") erro = erroQtd(campo);
        else if (tipo === "email-institucional") erro = erroEmailInstitucional(campo.value, campo);
        mostrarErro(campo, erro);
        return !erro;
    }

    // Aplica máscara/filtro enquanto a pessoa digita
    function aoDigitar(campo) {
        var tipo = (campo.getAttribute("data-valida") || "").toLowerCase();
        var inicio = campo.selectionStart;
        var antes = campo.value;
        if (tipo === "cpf") campo.value = formatarCpf(campo.value);
        else if (tipo === "telefone") campo.value = formatarTelefone(campo.value);
        else if (tipo === "nome") campo.value = filtrarNome(campo.value);
        else if (tipo === "qtd-pessoas") {
            // Bloqueia valor negativo; o sinal não vira um numero valido em silencio
            var brutoQtd = String(campo.value || "");
            if (brutoQtd.indexOf("-") >= 0 || brutoQtd.indexOf("+") >= 0) {
                campo.value = "";
                var minQ = campo.getAttribute("min") || "1";
                var maxQ = campo.getAttribute("max") || "30";
                mostrarErro(campo, "Informe entre " + minQ + " e " + maxQ + " pessoas.");
                return;
            }
            campo.value = brutoQtd.replace(/[^\d]/g, "");
        }
        // Mantém o cursor no fim quando a máscara muda o tamanho do texto
        if (document.activeElement === campo && typeof campo.setSelectionRange === "function") {
            var pos = campo.value.length;
            try { campo.setSelectionRange(pos, pos); } catch (e) { /* campo number pode não aceitar */ }
        }
        validarCampo(campo);
        return inicio !== undefined && antes;
    }

    // Impede digitar o sinal de menos (e outros não numéricos) na quantidade
    function aoTeclarQtd(evento) {
        var bloqueadas = ["-", "+", "e", "E", ".", ",", " "];
        if (bloqueadas.indexOf(evento.key) >= 0) {
            evento.preventDefault();
        }
    }

    // Liga os eventos de um campo
    function ligarCampo(campo) {
        if (campo.getAttribute("data-validacao-ligada") === "1") return;
        campo.setAttribute("data-validacao-ligada", "1");
        var tipo = (campo.getAttribute("data-valida") || "").toLowerCase();
        campo.addEventListener("input", function () { aoDigitar(campo); });
        campo.addEventListener("blur", function () { validarCampo(campo); });
        campo.addEventListener("invalid", function (ev) {
            // Troca o balão nativo (em inglês) pela mensagem em português
            ev.preventDefault();
            validarCampo(campo);
        });
        if (tipo === "qtd-pessoas") {
            campo.addEventListener("keydown", aoTeclarQtd);
            campo.addEventListener("paste", function (ev) {
                ev.preventDefault();
                var texto = (ev.clipboardData || global.clipboardData).getData("text") || "";
                campo.value = String(texto).replace(/[^\d]/g, "");
                validarCampo(campo);
            });
        }
        if (tipo === "cpf") campo.value = formatarCpf(campo.value);
        if (tipo === "telefone") campo.value = formatarTelefone(campo.value);
        if (tipo === "nome") campo.value = filtrarNome(campo.value);
    }

    // Valida todos os campos marcados do formulário
    function validarFormulario(form) {
        var campos = form.querySelectorAll("[data-valida]");
        var ok = true;
        var primeiro = null;
        Array.prototype.forEach.call(campos, function (campo) {
            if (campo.disabled) return;
            if (!validarCampo(campo)) {
                ok = false;
                if (!primeiro) primeiro = campo;
            }
        });
        if (primeiro && typeof primeiro.focus === "function") primeiro.focus();
        form.setAttribute("data-formulario-valido", ok ? "sim" : "nao");
        return ok;
    }

    // Remove do resumo as mensagens de telefone/nome/CPF que não são deste formulário
    function limparMensagensClienteIndevidas(raiz) {
        var area = raiz || document;
        var caixas = area.querySelectorAll(".alerta-erro, .validation-summary-errors, .detalhes-edicao-alerta-erro, .cadastro-erros");
        Array.prototype.forEach.call(caixas, function (caixa) {
            var itens = caixa.querySelectorAll("li, div, span");
            Array.prototype.forEach.call(itens, function (item) {
                var txt = (item.textContent || "").trim();
                if (mensagensClienteIndevidas.indexOf(txt) >= 0) item.parentNode.removeChild(item);
            });
            var resto = (caixa.textContent || "").replace(/\s+/g, " ").trim();
            var soIndevidas = mensagensClienteIndevidas.some(function (m) { return resto.indexOf(m) >= 0; })
                && !resto.replace(/Informe o telefone\.|Informe o nome\.|Informe o CPF\./gi, "").trim();
            if (!resto || soIndevidas) {
                caixa.setAttribute("hidden", "hidden");
                caixa.innerHTML = "";
            }
        });
    }

    // Ativa a validação em um formulário
    function iniciarFormulario(form) {
        if (!form || form.getAttribute("data-validacao-form-ok") === "1") return;
        form.setAttribute("data-validacao-form-ok", "1");
        form.setAttribute("novalidate", "novalidate");
        var campos = form.querySelectorAll("[data-valida]");
        Array.prototype.forEach.call(campos, ligarCampo);
        form.addEventListener("submit", function (ev) {
            if (!validarFormulario(form)) ev.preventDefault();
        });
    }

    // Procura os formulários da página e liga a validação
    function iniciar() {
        limparMensagensClienteIndevidas(document);
        var forms = document.querySelectorAll("form[data-validacao-form], form");
        Array.prototype.forEach.call(forms, function (form) {
            if (form.querySelector("[data-valida]")) iniciarFormulario(form);
        });
    }

    var api = {
        soDigitos: soDigitos,
        formatarCpf: formatarCpf,
        formatarTelefone: formatarTelefone,
        filtrarNome: filtrarNome,
        erroCpf: erroCpf,
        erroTelefone: erroTelefone,
        erroNome: erroNome,
        erroQtd: erroQtd,
        erroEmailInstitucional: erroEmailInstitucional,
        validarFormulario: validarFormulario,
        iniciarFormulario: iniciarFormulario,
        iniciar: iniciar,
        limparMensagensClienteIndevidas: limparMensagensClienteIndevidas
    };
    global.ValidacaoFormularios = api;

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", iniciar);
    } else {
        iniciar();
    }
})(typeof window !== "undefined" ? window : this);
