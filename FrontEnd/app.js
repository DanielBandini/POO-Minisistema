

//Estado do sistema ───────────────────────────────────────────
const sistema = {
  disciplinas: [
    new Disciplina('ADS210', 'Estruturas de Dados', 'Computação', 4),
    new Disciplina('MAT120', 'Cálculo I', 'Matemática', 3),
    new Disciplina('PI0002', 'Projeto Integrador', 'Projetos', 2),
  ],
  discente: new Discente('Gabriel Souza', 'gabriel@edu.fecap.br', 'gabi123', '24026078'),
  responsavel: new Responsavel('Ana Souza', 'ana.souza@email.com', 'ana123', 'Mãe'),
  admin: new Administrador('Profa. Juliana Mendes', 'juliana@fecap.br', 'ju123', 'Computação'),
  trilha: new TrilhaEstudo('Trilha Base de Programação'),
  plano: new PlanoEstudo(),
  caixaSaida: [],
  modo: 'Pomodoro',
};

const { disciplinas, discente, responsavel, admin, trilha, plano } = sistema;
responsavel.vincularDiscente(discente);
const meta = responsavel.definirMeta(discente, 500);
disciplinas.forEach((d) => trilha.adicionarDisciplina(d));
[['Segunda', 100], ['Quarta', 100], ['Sexta', 100], ['Sábado', 150]].forEach(([dia, min]) => plano.adicionarItem(dia, min));

const notificadores = { email: new NotificadorEmail(sistema.caixaSaida), sms: new NotificadorSms(sistema.caixaSaida) };
const servico = new ServicoAlertaResponsavel();
const backend = new ClienteBackendEstudo();

//Ajudantes ───────────────────────────────────────────────────
const $ = (id) => document.getElementById(id);

const esc = (texto) => String(texto).replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

function avisar(texto) {
  $('avisos').textContent = texto;
}

// Campos extras de cada modalidade
const camposExtras = {
  Pomodoro: [['ciclos', 'Ciclos completos', 2]],
  Exercicios: [['questoes', 'Total de questões', 10], ['acertos', 'Acertos', 8]],
  Leitura: [['paginas', 'Páginas lidas', 15], ['resumo', 'Fez resumo? (1 = sim)', 1]],
};

//esenho ─────────────────────────────────────────────────────
function desenharLateral() {
  // Polimorfismo: o mesmo map chama métodos sobrescritos em cada classe
  $('perfis').innerHTML = [discente, responsavel, admin].map((u) => `
    <article>
      <h3>${esc(u.obterTipoPerfil())}</h3>
      <p>${esc(u.nome)}</p>
      <small>${esc(u.gerarPainelResumo())}</small>
    </article>`).join('');

  $('xp').textContent = `${discente.xp} XP`;
  $('barra').value = meta.calcularPercentual();
  $('textoMeta').textContent = `${meta.feito}/${meta.alvo} min · ${meta.calcularPercentual()}%` +
    (meta.estaCumprida() ? ' · cumprida!' : ` · faltam ${meta.faltam} min`);
}

function desenharExtras() {
  $('extras').innerHTML = camposExtras[sistema.modo].map(([id, rotulo, valor]) => `
    <label>${rotulo} <input type="number" id="${id}" value="${valor}"></label>`).join('');
}

function desenharHistorico() {
  const itens = discente.historico.reverse();
  $('historico').innerHTML = itens.length
    ? itens.map((s) => `<li>${esc(s.gerarFeedback())}</li>`).join('')
    : '<li><small>Nada registrado ainda.</small></li>';
}

function desenharCaixaSaida() {
  $('caixaSaida').innerHTML = sistema.caixaSaida.slice().reverse()
    .map((m) => `<li><b>${esc(m.canal)}</b> → ${esc(m.destino)}<br>${esc(m.texto)}</li>`).join('');
}

function desenharTrilhaEPlano() {
  $('tituloTrilha').textContent = `${trilha.titulo} (${trilha.horasTotais}h)`;
  $('trilha').innerHTML = trilha.disciplinas.map((d) => `<li>${esc(d.exibirResumo())}</li>`).join('');
  $('plano').innerHTML = plano.resumo().map((i) => `<li>${esc(i.dia)}: ${i.minutos} min</li>`).join('') +
    `<li><b>Total: ${plano.totalMinutos} min</b></li>`;
}

function desenharTudo() {
  desenharLateral();
  desenharHistorico();
  desenharCaixaSaida();
  desenharTrilhaEPlano();
}

function desenharTabela(lista) {
  if (!lista.length) return '<p>Nenhum registro no banco.</p>';
  const colunas = Object.keys(lista[0]);
  return `<table>
    <tr>${colunas.map((c) => `<th>${esc(c)}</th>`).join('')}</tr>
    ${lista.map((linha) => `<tr>${colunas.map((c) => `<td>${esc(linha[c] ?? '')}</td>`).join('')}</tr>`).join('')}
  </table>`;
}

//Registro de sessões ─────────────────────────────────────────
async function concluir(sessao) {
  meta.registrarAvanco(sessao.minutos);
  desenharTudo();
  avisar(sessao.gerarFeedback());
  try {
    await backend.salvarSessao(discente, sessao);
    avisar(`${sessao.gerarFeedback()} · salvo no banco ✔`);
  } catch {
    avisar(`${sessao.gerarFeedback()} · servidor offline, ficou só na tela`);
  }
}

function minutosValidos(minutos) {
  if (Number.isInteger(minutos) && minutos >= 1 && minutos <= 480) return true;
  avisar('A sessão precisa ter de 1 a 480 minutos.');
  return false;
}

$('modos').addEventListener('change', (evento) => {
  sistema.modo = evento.target.value; // valor do rádio escolhido
  desenharExtras();
});

$('formSessao').addEventListener('submit', (evento) => {
  evento.preventDefault(); // não recarrega a página
  const minutos = Number($('minutos').value);
  if (!minutosValidos(minutos)) return;

  const extras = {};
  for (const [id] of camposExtras[sistema.modo]) extras[id] = Number($(id).value);
  extras.resumo = extras.resumo === 1;

  try {
    const disciplina = disciplinas[$('disciplina').value];
    const sessao = SessaoEstudo.criar(sistema.modo, disciplina, minutos, extras);
    concluir(discente.registrarEstudo(sessao)); // sobrecarga: 1 argumento
  } catch (erro) {
    avisar(erro.message);
  }
});

$('formHorario').addEventListener('submit', (evento) => {
  evento.preventDefault();
  const inicio = Number($('inicio').value);
  const fim = Number($('fim').value);
  if (!minutosValidos((fim - inicio) * 60)) return;
  const disciplina = disciplinas[$('disciplina').value];
  concluir(discente.registrarEstudo(disciplina, inicio, fim, Number($('paginas').value))); // sobrecarga: 4 argumentos
});

//Alertas ─────────────────────────────────────────────────────
$('alertar').addEventListener('click', () => {
  servico.avaliarDiscente(discente, meta, responsavel, notificadores[$('canal').value]);
  desenharCaixaSaida();
});

//Banco de dados ──────────────────────────────────────────────
$('verSessoes').addEventListener('click', async () => {
  try {
    const lista = await backend.sessoes();
    $('resultado').innerHTML = desenharTabela(Array.isArray(lista) ? lista : []);
  } catch {
    $('resultado').innerHTML = '<p>Servidor offline. Execute o iniciar_backend.bat.</p>';
  }
});

$('verRelatorio').addEventListener('click', async () => {
  try {
    const relatorio = await backend.relatorio(discente.nome);
    $('resultado').innerHTML = `<pre>${esc(JSON.stringify(relatorio, null, 2))}</pre>`;
  } catch {
    $('resultado').innerHTML = '<p>Servidor offline. Execute o iniciar_backend.bat.</p>';
  }
});

//Início ──────────────────────────────────────────────────────
$('disciplina').innerHTML = disciplinas.map((d, i) => `<option value="${i}">${esc(d.codigo)} · ${esc(d.nome)}</option>`).join('');
desenharExtras();
desenharTudo();

backend.status()
  .then(() => ($('servidor').textContent = '● servidor conectado'))
  .catch(() => ($('servidor').textContent = '○ servidor offline'));
