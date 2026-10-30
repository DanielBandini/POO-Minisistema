
const INotificador = { nome: 'INotificador', metodos: ['obterCanal', 'enviarAlerta'] };
const IExportavelJson = { nome: 'IExportavelJson', metodos: ['paraJson'] };

// "Assina" a interface: se faltar algum método, o erro aparece ao abrir a página.
function assinarInterface(Classe, contrato) {
  for (const metodo of contrato.metodos) {
    if (typeof Classe.prototype[metodo] !== 'function') {
      throw new Error(`${Classe.name} não implementa ${contrato.nome}.${metodo}()`);
    }
  }
  Classe.prototype[contrato.nome] = true; 
}

function cumpre(objeto, contrato) {
  return objeto !== null && objeto[contrato.nome] === true;
}

//Disciplina ─────────────────────────────────────────
class Disciplina {
  #codigo; #nome; #categoria; #horas;

  constructor(codigo, nome, categoria, horas) {
    this.#codigo = codigo;
    this.#nome = nome;
    this.#categoria = categoria;
    this.#horas = horas > 0 ? horas : 2;
  }

  get codigo() { return this.#codigo; }
  get nome() { return this.#nome; }
  get categoria() { return this.#categoria; }
  get horas() { return this.#horas; }

  calcularCargaEmMinutos() { return this.#horas * 60; }

  exibirResumo() {
    return `${this.#nome} · ${this.#horas}h/sem · ${this.#categoria}`;
  }

  paraJson() {
    return JSON.stringify({ codigo: this.#codigo, nome: this.#nome, categoria: this.#categoria, cargaHorariaSemanal: this.#horas });
  }
}
assinarInterface(Disciplina, IExportavelJson);

//MetaEstudo ─────────────────────────────────────────
class MetaEstudo {
  #alvo = 0;
  #feito = 0;

  constructor(alvo) { this.redefinirMeta(alvo); }

  get alvo() { return this.#alvo; }
  get feito() { return this.#feito; }
  get faltam() { return Math.max(0, this.#alvo - this.#feito); }

  registrarAvanco(minutos) {
    const aceito = Number.isInteger(minutos) && minutos >= 1 && minutos <= 480;
    if (aceito) this.#feito += minutos;
    return aceito;
  }

  redefinirMeta(alvo) {
    this.#alvo = Math.min(3000, Math.max(30, alvo));
  }

  calcularPercentual() {
    return Math.min(100, Math.round((this.#feito / this.#alvo) * 1000) / 10);
  }

  estaCumprida() { return this.#feito >= this.#alvo; }

  paraJson() {
    return JSON.stringify({ metaMinutosSemana: this.#alvo, minutosCumpridos: this.#feito, percentual: this.calcularPercentual() });
  }
}
assinarInterface(MetaEstudo, IExportavelJson);

//Usuários ───────────────────────────────────────────
class Usuario {
  #nome; #email; #senha;

  constructor(nome, email, senha) {
    if (new.target === Usuario) throw new Error('Usuario é uma classe abstrata.');
    for (const metodo of ['obterTipoPerfil', 'gerarPainelResumo']) {
      if (this[metodo] === Usuario.prototype[metodo]) {
        throw new Error(`${new.target.name} esqueceu de implementar ${metodo}()`);
      }
    }
    this.#nome = nome;
    this.#email = email;
    this.#senha = senha;
  }

  get nome() { return this.#nome; }
  get email() { return this.#email; }

  autenticar(tentativa) { return tentativa === this.#senha; }

  obterTipoPerfil() { throw new Error('abstrato'); }
  gerarPainelResumo() { throw new Error('abstrato'); }
}

class Administrador extends Usuario {
  #departamento;
  constructor(nome, email, senha, departamento) {
    super(nome, email, senha);
    this.#departamento = departamento;
  }
  obterTipoPerfil() { return 'Administrador'; }
  gerarPainelResumo() { return `Departamento de ${this.#departamento}`; }
}

class Responsavel extends Usuario {
  #relacao;
  #acompanhados = new Set(); 
  #metas = new Map();

  constructor(nome, email, senha, relacao) {
    super(nome, email, senha);
    this.#relacao = relacao;
  }

  //Associação: o discente existe por conta própria
  vincularDiscente(discente) {
    const antes = this.#acompanhados.size;
    this.#acompanhados.add(discente);
    return this.#acompanhados.size > antes;
  }

  definirMeta(discente, minutos) {
    if (!this.#metas.has(discente)) this.#metas.set(discente, new MetaEstudo(minutos));
    else this.#metas.get(discente).redefinirMeta(minutos);
    return this.#metas.get(discente);
  }

  metaDe(discente) { return this.#metas.get(discente); }
  get discentes() { return [...this.#acompanhados]; }

  obterTipoPerfil() { return 'Responsável'; }
  gerarPainelResumo() { return `${this.#relacao} de ${this.discentes.map((d) => d.nome).join(', ')}`; }
}

class Discente extends Usuario {
  #ra;
  #xp = 0;
  #historico = [];

  constructor(nome, email, senha, ra) {
    super(nome, email, senha);
    this.#ra = ra;
  }

  get ra() { return this.#ra; }
  get xp() { return this.#xp; }
  get historico() { return [...this.#historico]; }


  registrarEstudo(...args) {
    let sessao;
    switch (args.length) {
      case 1: // (sessao)
        sessao = args[0];
        break;
      case 2: { // (disciplina, minutos) -> Pomodoro
        const [disciplina, minutos] = args;
        sessao = SessaoEstudo.criar('Pomodoro', disciplina, minutos, { ciclos: Math.max(1, Math.floor(minutos / 25)) });
        break;
      }
      case 4: { // (disciplina, horaInicio, horaFim, paginas) -> Leitura com resumo
        const [disciplina, inicio, fim, paginas] = args;
        sessao = SessaoEstudo.criar('Leitura', disciplina, (fim - inicio) * 60, { paginas, resumo: true });
        break;
      }
      default:
        throw new Error('registrarEstudo aceita 1, 2 ou 4 argumentos.');
    }
    this.#historico.push(sessao);
    this.#xp += sessao.calcularXp();
    return sessao;
  }

  obterTipoPerfil() { return 'Discente'; }
  gerarPainelResumo() { return `RA ${this.#ra} · ${this.#xp} XP · ${this.#historico.length} sessões`; }
}

// Sessões ────────────────────────────────────────────
class SessaoEstudo {
  #disciplina; #minutos;

  constructor(disciplina, minutos) {
    if (new.target === SessaoEstudo) throw new Error('SessaoEstudo é uma classe abstrata.');
    this.#disciplina = disciplina;
    this.#minutos = minutos;
  }

  get disciplina() { return this.#disciplina; }
  get minutos() { return this.#minutos; }

  obterModalidade() { throw new Error('abstrato'); }
  calcularXp() { throw new Error('abstrato'); }

  gerarFeedback() {
    return `${this.obterModalidade()} de ${this.#minutos} min em ${this.#disciplina.nome}: +${this.calcularXp()} XP`;
  }

  paraJson() {
    return JSON.stringify({
      disciplina: this.#disciplina.nome,
      tipoModalidade: this.obterModalidade(),
      duracaoMinutos: this.#minutos,
      xpGanho: this.calcularXp(),
    });
  }

  // Fábrica: cria a subclasse certa a partir do nome da modalidade
  static criar(tipo, disciplina, minutos, extras = {}) {
    switch (tipo) {
      case 'Pomodoro': return new SessaoPomodoro(disciplina, minutos, extras.ciclos ?? 0);
      case 'Exercicios': return new SessaoExercicios(disciplina, minutos, extras.questoes ?? 0, extras.acertos ?? 0);
      case 'Leitura': return new SessaoLeitura(disciplina, minutos, extras.paginas ?? 0, extras.resumo ?? false);
      default: throw new Error('Modalidade desconhecida: ' + tipo);
    }
  }
}
assinarInterface(SessaoEstudo, IExportavelJson);

class SessaoPomodoro extends SessaoEstudo {
  #ciclos;
  constructor(disciplina, minutos, ciclos) { super(disciplina, minutos); this.#ciclos = ciclos; }
  obterModalidade() { return 'Pomodoro'; }
  calcularXp() { return this.minutos * 2 + this.#ciclos * 15; }
  gerarFeedback() { return `${super.gerarFeedback()} (${this.#ciclos} 🍅)`; }
}

class SessaoExercicios extends SessaoEstudo {
  #questoes; #acertos;
  constructor(disciplina, minutos, questoes, acertos) {
    super(disciplina, minutos);
    if (acertos > questoes) throw new Error('Acertos não podem passar do total de questões.');
    this.#questoes = questoes;
    this.#acertos = acertos;
  }
  obterModalidade() { return 'Exercicios'; }
  calcularXp() { return this.minutos + this.#acertos * 10; }
  gerarFeedback() { return `${super.gerarFeedback()} (${this.#acertos}/${this.#questoes} certas)`; }
}

class SessaoLeitura extends SessaoEstudo {
  #paginas; #resumo;
  constructor(disciplina, minutos, paginas, resumo) { super(disciplina, minutos); this.#paginas = paginas; this.#resumo = resumo; }
  obterModalidade() { return 'Leitura'; }
  calcularXp() { return this.minutos + this.#paginas * 3 + (this.#resumo ? 25 : 0); }
}

//Notificadores ──────────────────────────────────────
class NotificadorEmail {
  #caixa;
  constructor(caixaDeSaida) { this.#caixa = caixaDeSaida; } // um array onde os e-mails ficam guardados
  obterCanal() { return 'E-mail'; }
  enviarAlerta(destino, texto) {
    this.#caixa.push({ canal: this.obterCanal(), destino, texto });
    return true;
  }
}
assinarInterface(NotificadorEmail, INotificador);

class NotificadorSms {
  #caixa;
  constructor(caixaDeSaida) { this.#caixa = caixaDeSaida; }
  obterCanal() { return 'SMS'; }
  enviarAlerta(destino, texto) {
    this.#caixa.push({ canal: this.obterCanal(), destino, texto: texto.slice(0, 160) });
    return true;
  }
}
assinarInterface(NotificadorSms, INotificador);

// Trilha (agregação) ─────────────────────────────────
class TrilhaEstudo {
  #titulo; #disciplinas = new Set();
  constructor(titulo) { this.#titulo = titulo; }
  get titulo() { return this.#titulo; }
  get disciplinas() { return [...this.#disciplinas]; }
  adicionarDisciplina(disciplina) { this.#disciplinas.add(disciplina); }
  get horasTotais() { return this.disciplinas.reduce((soma, d) => soma + d.horas, 0); }
}

//Plano (composição) ─────────────────────────────────
class ItemCronograma {
  #dia; #minutos;
  constructor(dia, minutos) { this.#dia = dia; this.#minutos = minutos; }
  get dia() { return this.#dia; }
  get minutos() { return this.#minutos; }
}

class PlanoEstudo {
  #itens = [];
  adicionarItem(dia, minutos) { this.#itens.push(new ItemCronograma(dia, minutos)); }
  get totalMinutos() { return this.#itens.reduce((soma, i) => soma + i.minutos, 0); }
  // Só devolve cópias simples, nunca os objetos internos
  resumo() { return this.#itens.map((i) => ({ dia: i.dia, minutos: i.minutos })); }
}

//Serviço (dependência) ──────────────────────────────
class ServicoAlertaResponsavel {
  avaliarDiscente(discente, meta, responsavel, notificador) {
    if (!cumpre(notificador, INotificador)) throw new Error('Notificador não cumpre INotificador.');
    const texto = meta.estaCumprida()
      ? `${discente.nome} bateu a meta da semana! 🎉`
      : `${discente.nome}: ${meta.calcularPercentual()}% da meta, faltam ${meta.faltam} min.`;
    return notificador.enviarAlerta(responsavel.email, texto);
  }
}

//Cliente do backend ────────────────────────────────
class ClienteBackendEstudo {
  constructor(base = 'http://localhost:5080') { this.base = base; }

  async chamar(rota, corpo = null) {
    const opcoes = corpo
      ? { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(corpo) }
      : { method: 'GET' };
    const resposta = await fetch(this.base + rota, opcoes);
    if (!resposta.ok) throw new Error(`O servidor respondeu ${resposta.status}`);
    return resposta.json();
  }

  status() { return this.chamar('/api/status'); }
  sessoes() { return this.chamar('/api/sessoes'); }
  relatorio(nome) { return this.chamar(`/api/relatorio/${encodeURIComponent(nome)}`); }
  salvarSessao(discente, sessao) { return this.chamar('/api/sessoes', { discenteNome: discente.nome, ...JSON.parse(sessao.paraJson()) }); }
  salvarMeta(discente, responsavel, meta) {
    return this.chamar('/api/metas', { discenteNome: discente.nome, responsavelNome: responsavel.nome, ...JSON.parse(meta.paraJson()) });
  }
}
