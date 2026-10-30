using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace BackendEstudoAPI;

public class Program
{
    public static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        string pastaBase = AppContext.BaseDirectory;
        string arquivoConfigMySql = Path.Combine(pastaBase, "mysql_config.txt");
        string arquivoJsonLocal = Path.Combine(pastaBase, "dados_estudo_local.json");

        string connStringPadrao = "Server=localhost;Port=3306;Database=fecap_estudo;Uid=root;Pwd=;";
        if (File.Exists(arquivoConfigMySql))
        {
            string lido = File.ReadAllText(arquivoConfigMySql).Trim();
            if (!string.IsNullOrWhiteSpace(lido))
            {
                connStringPadrao = lido;
            }
        }
        else
        {
            try { File.WriteAllText(arquivoConfigMySql, connStringPadrao); } catch { }
        }

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--mysql" && i + 1 < args.Length)
            {
                connStringPadrao = args[i + 1];
            }
        }

        BancoGerenciador banco = new BancoGerenciador(connStringPadrao, arquivoJsonLocal);
        banco.Inicializar();

        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.ClearProviders();
        builder.Services.AddCors(opcoes =>
        {
            opcoes.AddDefaultPolicy(pol =>
                pol.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader());
        });

        var app = builder.Build();
        app.UseCors();

        string porta = Environment.GetEnvironmentVariable("PORTA_BACKEND") ?? "5080";
        string urlEscuta = $"http://localhost:{porta}";

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("======================================================================");
        Console.WriteLine("  FECAP • POO — BACKEND DA PLATAFORMA DE APOIO AO ESTUDO (C#)");
        Console.WriteLine("======================================================================");
        Console.ResetColor();
        Console.WriteLine($"  URL Base da API   : {urlEscuta}");
        Console.WriteLine($"  Painel no Browser : {urlEscuta}/");
        Console.WriteLine($"  Modo de Dados     : {(banco.UsandoMySql ? "MySQL Local Conectado (fecap_estudo)" : "Fallback Local (dados_estudo_local.json)")}");
        if (!banco.UsandoMySql)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  [Aviso MySQL]     : {banco.UltimoErroMySql}");
            Console.WriteLine($"  [Dica]            : Edite 'mysql_config.txt' ao lado do .exe se a senha do seu root for diferente.");
            Console.ResetColor();
        }
        Console.WriteLine("----------------------------------------------------------------------");
        Console.WriteLine("  Rotas disponíveis para C# (HttpClient) ou JavaScript (fetch):");
        Console.WriteLine("    GET  /api/status                   -> Status da API e do MySQL");
        Console.WriteLine("    GET  /api/disciplinas              -> Lista disciplinas cadastradas");
        Console.WriteLine("    POST /api/disciplinas              -> Cadastra nova disciplina");
        Console.WriteLine("    GET  /api/usuarios                 -> Lista usuários (Admin, Responsável, Discente)");
        Console.WriteLine("    POST /api/usuarios                 -> Cadastra novo usuário");
        Console.WriteLine("    GET  /api/sessoes                  -> Lista sessões de estudo registradas");
        Console.WriteLine("    POST /api/sessoes                  -> Registra sessão (Pomodoro, Exercícios, Leitura)");
        Console.WriteLine("    GET  /api/metas                    -> Lista metas semanais definidas pelos responsáveis");
        Console.WriteLine("    POST /api/metas                    -> Cria ou atualiza meta de estudo");
        Console.WriteLine("    GET  /api/relatorio/{discente}     -> Relatório consolidado do discente");
        Console.WriteLine("======================================================================");
        Console.WriteLine("  Pressione Ctrl+C para encerrar o servidor.");
        Console.WriteLine();

        app.MapGet("/", () => Results.Content(GerarHtmlPainel(banco, urlEscuta), "text/html; charset=utf-8"));

        app.MapGet("/api/status", () => Results.Ok(banco.ObterStatus()));

        app.MapPost("/api/config/mysql", async (HttpContext ctx) =>
        {
            var body = await JsonSerializer.DeserializeAsync<ConfigMySqlDto>(ctx.Request.Body, JsonOpts.Padrao);
            if (body == null || string.IsNullOrWhiteSpace(body.ConnectionString))
                return Results.BadRequest(new { erro = "Informe connectionString." });

            try { File.WriteAllText(arquivoConfigMySql, body.ConnectionString.Trim()); } catch { }
            banco.AtualizarStringConexao(body.ConnectionString.Trim());
            return Results.Ok(banco.ObterStatus());
        });

        app.MapGet("/api/disciplinas", () => Results.Ok(banco.ListarDisciplinas()));

        app.MapPost("/api/disciplinas", async (HttpContext ctx) =>
        {
            var dto = await JsonSerializer.DeserializeAsync<DisciplinaDto>(ctx.Request.Body, JsonOpts.Padrao);
            if (dto == null || string.IsNullOrWhiteSpace(dto.Nome))
                return Results.BadRequest(new { erro = "O campo 'nome' da disciplina é obrigatório." });

            var criada = banco.InserirDisciplina(dto);
            LogConsole("POST /api/disciplinas", $"{criada.Nome} ({criada.Categoria})");
            return Results.Created($"/api/disciplinas/{criada.Id}", criada);
        });

        app.MapGet("/api/usuarios", (string perfil) => Results.Ok(banco.ListarUsuarios(perfil)));

        app.MapPost("/api/usuarios", async (HttpContext ctx) =>
        {
            var dto = await JsonSerializer.DeserializeAsync<UsuarioDto>(ctx.Request.Body, JsonOpts.Padrao);
            if (dto == null || string.IsNullOrWhiteSpace(dto.Nome) || string.IsNullOrWhiteSpace(dto.TipoPerfil))
                return Results.BadRequest(new { erro = "Campos 'nome' e 'tipoPerfil' (Administrador, Responsavel ou Discente) são obrigatórios." });

            var criado = banco.InserirUsuario(dto);
            LogConsole("POST /api/usuarios", $"[{criado.TipoPerfil}] {criado.Nome} ({criado.Email})");
            return Results.Created($"/api/usuarios/{criado.Id}", criado);
        });

        app.MapGet("/api/sessoes", (string discente) => Results.Ok(banco.ListarSessoes(discente)));

        app.MapPost("/api/sessoes", async (HttpContext ctx) =>
        {
            var dto = await JsonSerializer.DeserializeAsync<SessaoEstudoDto>(ctx.Request.Body, JsonOpts.Padrao);
            if (dto == null || string.IsNullOrWhiteSpace(dto.DiscenteNome) || dto.DuracaoMinutos <= 0)
                return Results.BadRequest(new { erro = "Informe 'discenteNome', 'disciplina' e 'duracaoMinutos' (> 0)." });

            var criada = banco.InserirSessao(dto);
            LogConsole("POST /api/sessoes", $"{criada.DiscenteNome} estudou {criada.Disciplina} ({criada.TipoModalidade}, {criada.DuracaoMinutos} min) -> +{criada.XpGanho} XP");
            return Results.Created($"/api/sessoes/{criada.Id}", criada);
        });

        app.MapGet("/api/metas", (string discente) => Results.Ok(banco.ListarMetas(discente)));

        app.MapPost("/api/metas", async (HttpContext ctx) =>
        {
            var dto = await JsonSerializer.DeserializeAsync<MetaEstudoDto>(ctx.Request.Body, JsonOpts.Padrao);
            if (dto == null || string.IsNullOrWhiteSpace(dto.DiscenteNome) || dto.MetaMinutosSemana <= 0)
                return Results.BadRequest(new { erro = "Informe 'discenteNome', 'responsavelNome' e 'metaMinutosSemana' (> 0)." });

            var criada = banco.InserirOuAtualizarMeta(dto);
            LogConsole("POST /api/metas", $"Meta para {criada.DiscenteNome} por {criada.ResponsavelNome}: {criada.MetaMinutosSemana} min/semana");
            return Results.Created($"/api/metas/{criada.Id}", criada);
        });

        app.MapGet("/api/relatorio/{discenteNome}", (string discenteNome) => Results.Ok(banco.GerarRelatorio(discenteNome)));

        app.Run(urlEscuta);
    }

    private static void LogConsole(string rota, string detalhe)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"[{DateTime.Now:HH:mm:ss}] {rota}");
        Console.ResetColor();
        Console.WriteLine($" -> {detalhe}");
    }

    private static string GerarHtmlPainel(BancoGerenciador b, string url)
    {
        var st = b.ObterStatus();
        string corBadge = b.UsandoMySql ? "#00E387" : "#ffd54f";
        string txtBadge = b.UsandoMySql ? "CONECTADO AO MYSQL LOCAL" : "MODO LOCAL (JSON) — MYSQL NÃO DETECTADO";
        return $$"""
<!DOCTYPE html>
<html lang="pt-BR">
<head>
<meta charset="UTF-8">
<title>Backend FECAP Estudo API — Painel Local</title>
<style>
  body { background: #011a14; color: #e6f2ee; font-family: 'Segoe UI', system-ui, sans-serif; margin: 0; padding: 24px; }
  .wrap { max-width: 980px; margin: 0 auto; }
  .top { background: #023327; border: 1px solid #0f4a3c; border-left: 6px solid #00E387; border-radius: 10px; padding: 18px 22px; margin-bottom: 20px; }
  h1 { margin: 0 0 6px; font-size: 22px; color: #00E387; }
  .badge { display: inline-block; padding: 4px 10px; border-radius: 999px; font-weight: 700; font-size: 12px; background: {{corBadge}}; color: #011a14; }
  .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(210px, 1fr)); gap: 12px; margin-bottom: 20px; }
  .card { background: #022a21; border: 1px solid #0f4a3c; border-radius: 8px; padding: 14px; }
  .card h3 { margin: 0 0 6px; font-size: 13px; color: #9fbdb3; text-transform: uppercase; }
  .card .v { font-size: 26px; font-weight: 700; color: #00E387; }
  a { color: #00E387; font-size: 13px; }
  button { background: #00E387; color: #022b21; border: 0; padding: 8px 14px; border-radius: 6px; font-weight: 700; cursor: pointer; }
  input { background: #01120e; color: #e6f2ee; border: 1px solid #3f7d6c; padding: 7px 10px; border-radius: 6px; width: 420px; max-width: 100%; font-family: Consolas, monospace; }
</style>
</head>
<body>
<div class="wrap">
  <div class="top">
    <h1>FECAP • Backend da Plataforma de Apoio ao Estudo (C#)</h1>
    <p style="margin:6px 0 10px;color:#9fbdb3;">Servidor ativo em <code>{{url}}</code> — Pronto para receber requisições em C# (<code>HttpClient</code>) ou JavaScript (<code>fetch</code>).</p>
    <span class="badge">{{txtBadge}}</span>
  </div>
  <div class="grid">
    <div class="card"><h3>Usuários</h3><div class="v">{{st.TotalUsuarios}}</div><a href="/api/usuarios" target="_blank">Ver JSON (/api/usuarios)</a></div>
    <div class="card"><h3>Disciplinas</h3><div class="v">{{st.TotalDisciplinas}}</div><a href="/api/disciplinas" target="_blank">Ver JSON (/api/disciplinas)</a></div>
    <div class="card"><h3>Sessões de Estudo</h3><div class="v">{{st.TotalSessoes}}</div><a href="/api/sessoes" target="_blank">Ver JSON (/api/sessoes)</a></div>
    <div class="card"><h3>Metas Semanais</h3><div class="v">{{st.TotalMetas}}</div><a href="/api/metas" target="_blank">Ver JSON (/api/metas)</a></div>
  </div>
  <div class="card" style="margin-bottom:16px;">
    <h3>Configuração de Conexão MySQL Local</h3>
    <p style="font-size:14px;color:#9fbdb3;margin:4px 0 10px;">String atual: <code>{{st.StringConexaoMascarada}}</code> {{(string.IsNullOrEmpty(st.UltimoErroMySql) ? "" : "— Motivo do fallback: " + st.UltimoErroMySql)}}</p>
    <div style="display:flex;gap:8px;flex-wrap:wrap;align-items:center;">
      <input id="conn" value="Server=localhost;Port=3306;Database=fecap_estudo;Uid=root;Pwd=;" />
      <button onclick="reconectar()">Testar e Conectar ao MySQL</button>
    </div>
    <p id="msg" style="font-size:13px;color:#ffd54f;margin:8px 0 0;"></p>
  </div>
</div>
<script>
async function reconectar() {
  const c = document.getElementById('conn').value;
  const r = await fetch('/api/config/mysql', { method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify({connectionString: c}) });
  const j = await r.json();
  document.getElementById('msg').textContent = j.usandoMySql ? 'Conectado ao MySQL com sucesso. Recarregando...' : ('Ainda em fallback: ' + j.ultimoErroMySql);
  if (j.usandoMySql) setTimeout(() => location.reload(), 900);
}
</script>
</body>
</html>
""";
    }
}

public static class JsonOpts
{
    public static readonly JsonSerializerOptions Padrao = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
}

public class ConfigMySqlDto { public string ConnectionString { get; set; } }

public class DisciplinaDto
{
    public int Id { get; set; }
    public string Nome { get; set; }
    public string Categoria { get; set; } = "Obrigatória";
    public int CargaHorariaSemanal { get; set; } = 4;
}

public class UsuarioDto
{
    public int Id { get; set; }
    public string Nome { get; set; }
    public string Email { get; set; }
    public string TipoPerfil { get; set; }
    public string MatriculaOuCargo { get; set; } = "";
    public string ResponsavelNome { get; set; } = "";
}

public class SessaoEstudoDto
{
    public int Id { get; set; }
    public string DiscenteNome { get; set; }
    public string Disciplina { get; set; }
    public string TipoModalidade { get; set; } = "Pomodoro";
    public int DuracaoMinutos { get; set; }
    public int XpGanho { get; set; }
    public string ResumoFeedback { get; set; } = "";
    public string DataRegistro { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
}

public class MetaEstudoDto
{
    public int Id { get; set; }
    public string DiscenteNome { get; set; }
    public string ResponsavelNome { get; set; }
    public int MetaMinutosSemana { get; set; }
    public int MinutosCumpridos { get; set; }
}

public class EstadoLocal
{
    public List<DisciplinaDto> Disciplinas { get; set; } = new();
    public List<UsuarioDto> Usuarios { get; set; } = new();
    public List<SessaoEstudoDto> Sessoes { get; set; } = new();
    public List<MetaEstudoDto> Metas { get; set; } = new();
}

public class StatusApiDto
{
    public bool Online { get; set; } = true;
    public bool UsandoMySql { get; set; }
    public string ModoBanco { get; set; }
    public string StringConexaoMascarada { get; set; }
    public string UltimoErroMySql { get; set; }
    public int TotalUsuarios { get; set; }
    public int TotalDisciplinas { get; set; }
    public int TotalSessoes { get; set; }
    public int TotalMetas { get; set; }
}

public class BancoGerenciador
{
    private string connStr;
    private readonly string caminhoJson;
    private EstadoLocal cache = new();
    public bool UsandoMySql { get; private set; }
    public string UltimoErroMySql { get; private set; } = "";

    public BancoGerenciador(string connString, string arquivoJson)
    {
        connStr = connString;
        caminhoJson = arquivoJson;
    }

    public void AtualizarStringConexao(string nova)
    {
        connStr = nova;
        Inicializar();
    }

    public void Inicializar()
    {
        CarregarOuPopularJson();
        TentarInicializarMySql();
    }

    private void CarregarOuPopularJson()
    {
        if (File.Exists(caminhoJson))
        {
            try
            {
                var lido = JsonSerializer.Deserialize<EstadoLocal>(File.ReadAllText(caminhoJson), JsonOpts.Padrao);
                if (lido != null) { cache = lido; return; }
            }
            catch { }
        }

        cache = new EstadoLocal
        {
            Disciplinas = new List<DisciplinaDto>
            {
                new() { Id = 1, Nome = "Programação Orientada a Objetos", Categoria = "Computação", CargaHorariaSemanal = 4 },
                new() { Id = 2, Nome = "Banco de Dados e Modelagem SQL", Categoria = "Dados", CargaHorariaSemanal = 4 },
                new() { Id = 3, Nome = "Projeto Interdisciplinar (PI)", Categoria = "Prática", CargaHorariaSemanal = 6 }
            },
            Usuarios = new List<UsuarioDto>
            {
                new() { Id = 1, Nome = "Profa. Helena (Coordenação)", Email = "helena@fecap.br", TipoPerfil = "Administrador", MatriculaOuCargo = "Coordenação PI" },
                new() { Id = 2, Nome = "Carlos Silva (Pai do Lucas)", Email = "carlos@email.com", TipoPerfil = "Responsavel", MatriculaOuCargo = "Responsável Legal" },
                new() { Id = 3, Nome = "Lucas Silva", Email = "lucas.silva@edu.fecap.br", TipoPerfil = "Discente", MatriculaOuCargo = "RA-2026101", ResponsavelNome = "Carlos Silva (Pai do Lucas)" },
                new() { Id = 4, Nome = "Mariana Costa", Email = "mariana.costa@edu.fecap.br", TipoPerfil = "Discente", MatriculaOuCargo = "RA-2026102", ResponsavelNome = "Carlos Silva (Pai do Lucas)" }
            },
            Metas = new List<MetaEstudoDto>
            {
                new() { Id = 1, DiscenteNome = "Lucas Silva", ResponsavelNome = "Carlos Silva (Pai do Lucas)", MetaMinutosSemana = 180, MinutosCumpridos = 75 },
                new() { Id = 2, DiscenteNome = "Mariana Costa", ResponsavelNome = "Carlos Silva (Pai do Lucas)", MetaMinutosSemana = 240, MinutosCumpridos = 90 }
            },
            Sessoes = new List<SessaoEstudoDto>
            {
                new() { Id = 1, DiscenteNome = "Lucas Silva", Disciplina = "Programação Orientada a Objetos", TipoModalidade = "Pomodoro", DuracaoMinutos = 50, XpGanho = 100, ResumoFeedback = "2 ciclos Pomodoro concluídos com foco.", DataRegistro = DateTime.Now.AddHours(-3).ToString("yyyy-MM-dd HH:mm") },
                new() { Id = 2, DiscenteNome = "Lucas Silva", Disciplina = "Banco de Dados e Modelagem SQL", TipoModalidade = "Exercicios", DuracaoMinutos = 25, XpGanho = 85, ResumoFeedback = "Resolução de 5 consultas JOIN no MySQL.", DataRegistro = DateTime.Now.AddHours(-1).ToString("yyyy-MM-dd HH:mm") },
                new() { Id = 3, DiscenteNome = "Mariana Costa", Disciplina = "Projeto Interdisciplinar (PI)", TipoModalidade = "Leitura", DuracaoMinutos = 90, XpGanho = 135, ResumoFeedback = "Leitura e modelagem UML da arquitetura da plataforma.", DataRegistro = DateTime.Now.AddHours(-2).ToString("yyyy-MM-dd HH:mm") }
            }
        };
        SalvarJson();
    }

    private void SalvarJson()
    {
        try { File.WriteAllText(caminhoJson, JsonSerializer.Serialize(cache, JsonOpts.Padrao)); } catch { }
    }

    private void TentarInicializarMySql()
    {
        try
        {
            var builder = new MySqlConnectionStringBuilder(connStr);
            string nomeBanco = string.IsNullOrWhiteSpace(builder.Database) ? "fecap_estudo" : builder.Database;
            builder.Database = "";
            builder.ConnectionTimeout = 2;

            using (var connSemDb = new MySqlConnection(builder.ConnectionString))
            {
                connSemDb.Open();
                using var cmdDb = new MySqlCommand($"CREATE DATABASE IF NOT EXISTS `{nomeBanco}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;", connSemDb);
                cmdDb.ExecuteNonQuery();
            }

            builder.Database = nomeBanco;
            connStr = builder.ConnectionString;

            using var conn = new MySqlConnection(connStr);
            conn.Open();

            string sqlTabelas = @"
                CREATE TABLE IF NOT EXISTS disciplinas (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    nome VARCHAR(120) NOT NULL,
                    categoria VARCHAR(60) NOT NULL,
                    carga_horaria_semanal INT NOT NULL DEFAULT 4
                );
                CREATE TABLE IF NOT EXISTS usuarios (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    nome VARCHAR(120) NOT NULL,
                    email VARCHAR(120) NOT NULL,
                    tipo_perfil VARCHAR(40) NOT NULL,
                    matricula_ou_cargo VARCHAR(80) DEFAULT '',
                    responsavel_nome VARCHAR(120) DEFAULT ''
                );
                CREATE TABLE IF NOT EXISTS metas_estudo (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    discente_nome VARCHAR(120) NOT NULL,
                    responsavel_nome VARCHAR(120) NOT NULL,
                    meta_minutos_semana INT NOT NULL,
                    minutos_cumpridos INT NOT NULL DEFAULT 0
                );
                CREATE TABLE IF NOT EXISTS sessoes_estudo (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    discente_nome VARCHAR(120) NOT NULL,
                    disciplina VARCHAR(120) NOT NULL,
                    tipo_modalidade VARCHAR(50) NOT NULL,
                    duracao_minutos INT NOT NULL,
                    xp_ganho INT NOT NULL,
                    resumo_feedback VARCHAR(255) DEFAULT '',
                    data_registro VARCHAR(30) NOT NULL
                );";
            using (var cmdTab = new MySqlCommand(sqlTabelas, conn))
            {
                cmdTab.ExecuteNonQuery();
            }

            using var cmdCount = new MySqlCommand("SELECT COUNT(*) FROM usuarios;", conn);
            long qtd = Convert.ToInt64(cmdCount.ExecuteScalar());
            if (qtd == 0)
            {
                foreach (var d in cache.Disciplinas) InserirDisciplinaMySql(conn, d);
                foreach (var u in cache.Usuarios) InserirUsuarioMySql(conn, u);
                foreach (var m in cache.Metas) InserirMetaMySql(conn, m);
                foreach (var s in cache.Sessoes) InserirSessaoMySql(conn, s);
            }

            UsandoMySql = true;
            UltimoErroMySql = "";
        }
        catch (Exception ex)
        {
            UsandoMySql = false;
            UltimoErroMySql = ex.Message;
        }
    }

    public StatusApiDto ObterStatus()
    {
        var usuarios = ListarUsuarios(null);
        var disciplinas = ListarDisciplinas();
        var sessoes = ListarSessoes(null);
        var metas = ListarMetas(null);
        return new StatusApiDto
        {
            Online = true,
            UsandoMySql = UsandoMySql,
            ModoBanco = UsandoMySql ? "MySQL (fecap_estudo)" : "JSON Local (Fallback Automático)",
            StringConexaoMascarada = connStr,
            UltimoErroMySql = UltimoErroMySql,
            TotalUsuarios = usuarios.Count,
            TotalDisciplinas = disciplinas.Count,
            TotalSessoes = sessoes.Count,
            TotalMetas = metas.Count
        };
    }

    public List<DisciplinaDto> ListarDisciplinas()
    {
        if (UsandoMySql)
        {
            try
            {
                var lista = new List<DisciplinaDto>();
                using var conn = new MySqlConnection(connStr);
                conn.Open();
                using var cmd = new MySqlCommand("SELECT id, nome, categoria, carga_horaria_semanal FROM disciplinas ORDER BY id;", conn);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    lista.Add(new DisciplinaDto
                    {
                        Id = r.GetInt32(0),
                        Nome = r.GetString(1),
                        Categoria = r.GetString(2),
                        CargaHorariaSemanal = r.GetInt32(3)
                    });
                }
                return lista;
            }
            catch (Exception ex) { UsandoMySql = false; UltimoErroMySql = ex.Message; }
        }
        return cache.Disciplinas;
    }

    public DisciplinaDto InserirDisciplina(DisciplinaDto dto)
    {
        if (UsandoMySql)
        {
            try
            {
                using var conn = new MySqlConnection(connStr);
                conn.Open();
                dto.Id = InserirDisciplinaMySql(conn, dto);
                return dto;
            }
            catch (Exception ex) { UsandoMySql = false; UltimoErroMySql = ex.Message; }
        }
        dto.Id = cache.Disciplinas.Count == 0 ? 1 : cache.Disciplinas.Max(x => x.Id) + 1;
        cache.Disciplinas.Add(dto);
        SalvarJson();
        return dto;
    }

    private int InserirDisciplinaMySql(MySqlConnection conn, DisciplinaDto d)
    {
        using var cmd = new MySqlCommand("INSERT INTO disciplinas (nome, categoria, carga_horaria_semanal) VALUES (@n, @c, @ch); SELECT LAST_INSERT_ID();", conn);
        cmd.Parameters.AddWithValue("@n", d.Nome ?? "");
        cmd.Parameters.AddWithValue("@c", d.Categoria ?? "Obrigatória");
        cmd.Parameters.AddWithValue("@ch", d.CargaHorariaSemanal > 0 ? d.CargaHorariaSemanal : 4);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public List<UsuarioDto> ListarUsuarios(string perfil)
    {
        List<UsuarioDto> lista = cache.Usuarios;
        if (UsandoMySql)
        {
            try
            {
                lista = new List<UsuarioDto>();
                using var conn = new MySqlConnection(connStr);
                conn.Open();
                using var cmd = new MySqlCommand("SELECT id, nome, email, tipo_perfil, matricula_ou_cargo, responsavel_nome FROM usuarios ORDER BY id;", conn);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    lista.Add(new UsuarioDto
                    {
                        Id = r.GetInt32(0),
                        Nome = r.GetString(1),
                        Email = r.GetString(2),
                        TipoPerfil = r.GetString(3),
                        MatriculaOuCargo = r.GetString(4),
                        ResponsavelNome = r.GetString(5)
                    });
                }
            }
            catch (Exception ex) { UsandoMySql = false; UltimoErroMySql = ex.Message; lista = cache.Usuarios; }
        }
        if (!string.IsNullOrWhiteSpace(perfil))
        {
            lista = lista.Where(u => string.Equals(u.TipoPerfil, perfil, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        return lista;
    }

    public UsuarioDto InserirUsuario(UsuarioDto dto)
    {
        dto.Email ??= "sem-email@fecap.br";
        dto.MatriculaOuCargo ??= "";
        dto.ResponsavelNome ??= "";

        if (UsandoMySql)
        {
            try
            {
                using var conn = new MySqlConnection(connStr);
                conn.Open();
                dto.Id = InserirUsuarioMySql(conn, dto);
                return dto;
            }
            catch (Exception ex) { UsandoMySql = false; UltimoErroMySql = ex.Message; }
        }
        dto.Id = cache.Usuarios.Count == 0 ? 1 : cache.Usuarios.Max(x => x.Id) + 1;
        cache.Usuarios.Add(dto);
        SalvarJson();
        return dto;
    }

    private int InserirUsuarioMySql(MySqlConnection conn, UsuarioDto u)
    {
        using var cmd = new MySqlCommand("INSERT INTO usuarios (nome, email, tipo_perfil, matricula_ou_cargo, responsavel_nome) VALUES (@n, @e, @t, @m, @r); SELECT LAST_INSERT_ID();", conn);
        cmd.Parameters.AddWithValue("@n", u.Nome ?? "");
        cmd.Parameters.AddWithValue("@e", u.Email ?? "");
        cmd.Parameters.AddWithValue("@t", u.TipoPerfil ?? "Discente");
        cmd.Parameters.AddWithValue("@m", u.MatriculaOuCargo ?? "");
        cmd.Parameters.AddWithValue("@r", u.ResponsavelNome ?? "");
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public List<SessaoEstudoDto> ListarSessoes(string discente)
    {
        List<SessaoEstudoDto> lista = cache.Sessoes;
        if (UsandoMySql)
        {
            try
            {
                lista = new List<SessaoEstudoDto>();
                using var conn = new MySqlConnection(connStr);
                conn.Open();
                using var cmd = new MySqlCommand("SELECT id, discente_nome, disciplina, tipo_modalidade, duracao_minutos, xp_ganho, resumo_feedback, data_registro FROM sessoes_estudo ORDER BY id DESC;", conn);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    lista.Add(new SessaoEstudoDto
                    {
                        Id = r.GetInt32(0),
                        DiscenteNome = r.GetString(1),
                        Disciplina = r.GetString(2),
                        TipoModalidade = r.GetString(3),
                        DuracaoMinutos = r.GetInt32(4),
                        XpGanho = r.GetInt32(5),
                        ResumoFeedback = r.GetString(6),
                        DataRegistro = r.GetString(7)
                    });
                }
            }
            catch (Exception ex) { UsandoMySql = false; UltimoErroMySql = ex.Message; lista = cache.Sessoes; }
        }
        if (!string.IsNullOrWhiteSpace(discente))
        {
            lista = lista.Where(s => s.DiscenteNome.Contains(discente, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        return lista;
    }

    public SessaoEstudoDto InserirSessao(SessaoEstudoDto dto)
    {
        dto.Disciplina ??= "Geral";
        dto.TipoModalidade ??= "Pomodoro";
        if (dto.XpGanho <= 0) dto.XpGanho = dto.DuracaoMinutos * 2;
        if (string.IsNullOrWhiteSpace(dto.DataRegistro)) dto.DataRegistro = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        dto.ResumoFeedback ??= $"Sessão de {dto.TipoModalidade} registrada com sucesso.";

        if (UsandoMySql)
        {
            try
            {
                using var conn = new MySqlConnection(connStr);
                conn.Open();
                dto.Id = InserirSessaoMySql(conn, dto);
                using var cmdUpMeta = new MySqlCommand("UPDATE metas_estudo SET minutos_cumpridos = minutos_cumpridos + @min WHERE discente_nome = @disc;", conn);
                cmdUpMeta.Parameters.AddWithValue("@min", dto.DuracaoMinutos);
                cmdUpMeta.Parameters.AddWithValue("@disc", dto.DiscenteNome);
                cmdUpMeta.ExecuteNonQuery();
                return dto;
            }
            catch (Exception ex) { UsandoMySql = false; UltimoErroMySql = ex.Message; }
        }

        dto.Id = cache.Sessoes.Count == 0 ? 1 : cache.Sessoes.Max(x => x.Id) + 1;
        cache.Sessoes.Add(dto);
        var meta = cache.Metas.FirstOrDefault(m => string.Equals(m.DiscenteNome, dto.DiscenteNome, StringComparison.OrdinalIgnoreCase));
        if (meta != null) meta.MinutosCumpridos += dto.DuracaoMinutos;
        SalvarJson();
        return dto;
    }

    private int InserirSessaoMySql(MySqlConnection conn, SessaoEstudoDto s)
    {
        using var cmd = new MySqlCommand("INSERT INTO sessoes_estudo (discente_nome, disciplina, tipo_modalidade, duracao_minutos, xp_ganho, resumo_feedback, data_registro) VALUES (@dn, @di, @tm, @du, @xp, @rf, @dr); SELECT LAST_INSERT_ID();", conn);
        cmd.Parameters.AddWithValue("@dn", s.DiscenteNome ?? "");
        cmd.Parameters.AddWithValue("@di", s.Disciplina ?? "");
        cmd.Parameters.AddWithValue("@tm", s.TipoModalidade ?? "Pomodoro");
        cmd.Parameters.AddWithValue("@du", s.DuracaoMinutos);
        cmd.Parameters.AddWithValue("@xp", s.XpGanho);
        cmd.Parameters.AddWithValue("@rf", s.ResumoFeedback ?? "");
        cmd.Parameters.AddWithValue("@dr", s.DataRegistro ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public List<MetaEstudoDto> ListarMetas(string discente)
    {
        List<MetaEstudoDto> lista = cache.Metas;
        if (UsandoMySql)
        {
            try
            {
                lista = new List<MetaEstudoDto>();
                using var conn = new MySqlConnection(connStr);
                conn.Open();
                using var cmd = new MySqlCommand("SELECT id, discente_nome, responsavel_nome, meta_minutos_semana, minutos_cumpridos FROM metas_estudo ORDER BY id;", conn);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    lista.Add(new MetaEstudoDto
                    {
                        Id = r.GetInt32(0),
                        DiscenteNome = r.GetString(1),
                        ResponsavelNome = r.GetString(2),
                        MetaMinutosSemana = r.GetInt32(3),
                        MinutosCumpridos = r.GetInt32(4)
                    });
                }
            }
            catch (Exception ex) { UsandoMySql = false; UltimoErroMySql = ex.Message; lista = cache.Metas; }
        }
        if (!string.IsNullOrWhiteSpace(discente))
        {
            lista = lista.Where(m => m.DiscenteNome.Contains(discente, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        return lista;
    }

    public MetaEstudoDto InserirOuAtualizarMeta(MetaEstudoDto dto)
    {
        dto.ResponsavelNome ??= "Responsável";
        if (UsandoMySql)
        {
            try
            {
                using var conn = new MySqlConnection(connStr);
                conn.Open();
                dto.Id = InserirMetaMySql(conn, dto);
                return dto;
            }
            catch (Exception ex) { UsandoMySql = false; UltimoErroMySql = ex.Message; }
        }
        var existente = cache.Metas.FirstOrDefault(m => string.Equals(m.DiscenteNome, dto.DiscenteNome, StringComparison.OrdinalIgnoreCase));
        if (existente != null)
        {
            existente.MetaMinutosSemana = dto.MetaMinutosSemana;
            existente.ResponsavelNome = dto.ResponsavelNome;
            SalvarJson();
            return existente;
        }
        dto.Id = cache.Metas.Count == 0 ? 1 : cache.Metas.Max(x => x.Id) + 1;
        cache.Metas.Add(dto);
        SalvarJson();
        return dto;
    }

    private int InserirMetaMySql(MySqlConnection conn, MetaEstudoDto m)
    {
        using var cmd = new MySqlCommand("INSERT INTO metas_estudo (discente_nome, responsavel_nome, meta_minutos_semana, minutos_cumpridos) VALUES (@dn, @rn, @mm, @mc); SELECT LAST_INSERT_ID();", conn);
        cmd.Parameters.AddWithValue("@dn", m.DiscenteNome ?? "");
        cmd.Parameters.AddWithValue("@rn", m.ResponsavelNome ?? "");
        cmd.Parameters.AddWithValue("@mm", m.MetaMinutosSemana);
        cmd.Parameters.AddWithValue("@mc", m.MinutosCumpridos);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public object GerarRelatorio(string discenteNome)
    {
        var sessoes = ListarSessoes(discenteNome);
        var metas = ListarMetas(discenteNome);
        int totalMinutos = sessoes.Sum(s => s.DuracaoMinutos);
        int totalXp = sessoes.Sum(s => s.XpGanho);
        int metaSemanal = metas.FirstOrDefault()?.MetaMinutosSemana ?? 180;
        double percentualMeta = metaSemanal > 0 ? Math.Round(Math.Min(100.0, (totalMinutos * 100.0) / metaSemanal), 1) : 100.0;

        return new
        {
            discente = discenteNome,
            totalSessoes = sessoes.Count,
            totalMinutosEstudados = totalMinutos,
            totalXpAcumulado = totalXp,
            metaMinutosSemana = metaSemanal,
            percentualMetaConcluida = percentualMeta,
            statusAcompanhamento = percentualMeta >= 100 ? "Meta semanal concluída integralmente." : (percentualMeta >= 50 ? "Progresso dentro do ritmo esperado." : "Atenção: progresso abaixo de 50% da meta semanal."),
            sessoesRecentes = sessoes.Take(10).ToList()
        };
    }
}
