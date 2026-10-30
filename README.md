# FecapStudy

Plataforma de apoio ao estudo desenvolvida para o Projeto Integrador da FECAP.
O aluno registra suas sessões de estudo, ganha XP e acompanha a meta semanal;
o responsável define a meta e recebe alertas; o administrador organiza as trilhas
de disciplinas. Os dados são gravados em um banco MySQL por meio de uma API em C#.

## Tecnologias

- **JavaScript** (núcleo orientado a objetos)
- **HTML5 + CSS3** (interface gráfica)
- **C# + MySQL** (backend `BackendEstudoAPI.exe`, porta 5080)

## Funcionalidades

- Registro de sessões em três modalidades: **Pomodoro**, **Exercícios** e **Leitura**
- Cálculo automático de XP para cada modalidade
- Meta semanal com barra de progresso (de 30 a 3000 minutos)
- Registro por horário ("estudei das 14h às 16h")
- Alertas para o responsável por e-mail ou SMS (simulados)
- Trilha de disciplinas e cronograma semanal
- Gravação e consulta das sessões no banco de dados

## Conceitos de POO aplicados

| Conceito | Onde aparece |
|---|---|
| Abstração e encapsulamento | `Disciplina` e `MetaEstudo`, com campos privados (`#`) |
| Classe abstrata e herança | `Usuario` → `Discente`, `Responsavel`, `Administrador` e `SessaoEstudo` → `SessaoPomodoro`, `SessaoExercicios`, `SessaoLeitura` |
| Polimorfismo dinâmico | `calcularXp()`, `gerarFeedback()` e `gerarPainelResumo()` sobrescritos em cada classe |
| Sobrecarga | `Discente.registrarEstudo()` aceita 3 formas de chamada |
| Interfaces | `INotificador` e `IExportavelJson` |
| Associação | `Responsavel` → `Discente` |
| Agregação | `TrilhaEstudo` ◇ `Disciplina` |
| Composição | `PlanoEstudo` ◆ `ItemCronograma` |
| Dependência | `ServicoAlertaResponsavel` e `ClienteBackendEstudo` |

## Estrutura

```
index.html    tela do sistema
estilo.css    estilo da tela
classes.js    classes do núcleo orientado a objetos
app.js        eventos da tela e comunicação com o backend
```

## Como executar

1. Inicie o MySQL (por exemplo, pelo XAMPP).
2. Execute `iniciar_backend.bat` na pasta do backend. O banco `fecap_estudo`
   é criado automaticamente.
3. Abra o arquivo `index.html` no navegador.

> Sem o MySQL ligado, o backend salva os dados em um arquivo JSON local,
> e o sistema continua funcionando normalmente.

## Rotas da API utilizadas

| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/status` | Verifica a conexão com o servidor |
| POST | `/api/sessoes` | Grava uma sessão de estudo |
| GET | `/api/sessoes` | Lista as sessões gravadas |
| POST | `/api/metas` | Salva a meta semanal |
| GET | `/api/relatorio/{nome}` | Relatório do discente |

## Autores

Daniel Bandini Grande
