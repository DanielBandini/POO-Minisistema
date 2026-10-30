-- ============================================================================
-- FECAP • POO • Aula 9 — Script SQL da Plataforma de Apoio ao Estudo (MySQL)
-- Observação: O próprio BackendEstudoAPI.exe executa estes comandos
-- automaticamente ao iniciar caso o serviço do MySQL esteja ativo.
-- ============================================================================

CREATE DATABASE IF NOT EXISTS fecap_estudo
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE fecap_estudo;

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
    tipo_perfil VARCHAR(40) NOT NULL COMMENT 'Administrador, Responsavel ou Discente',
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
    tipo_modalidade VARCHAR(50) NOT NULL COMMENT 'Pomodoro, Exercicios ou Leitura',
    duracao_minutos INT NOT NULL,
    xp_ganho INT NOT NULL,
    resumo_feedback VARCHAR(255) DEFAULT '',
    data_registro VARCHAR(30) NOT NULL
);

-- Dados iniciais de demonstração
INSERT INTO disciplinas (nome, categoria, carga_horaria_semanal) VALUES
('Programação Orientada a Objetos', 'Computação', 4),
('Banco de Dados e Modelagem SQL', 'Dados', 4),
('Projeto Interdisciplinar (PI)', 'Prática', 6);

INSERT INTO usuarios (nome, email, tipo_perfil, matricula_ou_cargo, responsavel_nome) VALUES
('Profa. Helena (Coordenação)', 'helena@fecap.br', 'Administrador', 'Coordenação PI', ''),
('Carlos Silva (Pai do Lucas)', 'carlos@email.com', 'Responsavel', 'Responsável Legal', ''),
('Lucas Silva', 'lucas.silva@edu.fecap.br', 'Discente', 'RA-2026101', 'Carlos Silva (Pai do Lucas)'),
('Mariana Costa', 'mariana.costa@edu.fecap.br', 'Discente', 'RA-2026102', 'Carlos Silva (Pai do Lucas)');

INSERT INTO metas_estudo (discente_nome, responsavel_nome, meta_minutos_semana, minutos_cumpridos) VALUES
('Lucas Silva', 'Carlos Silva (Pai do Lucas)', 180, 75),
('Mariana Costa', 'Carlos Silva (Pai do Lucas)', 240, 90);
