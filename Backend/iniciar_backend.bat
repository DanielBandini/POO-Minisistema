@echo off
chcp 65001 > nul
title FECAP - POO - Backend Local (Plataforma de Apoio ao Estudo)
color 0A
cls
echo ======================================================================
echo   FECAP - PROGRAMACAO ORIENTADA A OBJETOS - AULA 9 (PROJETO PI)
echo   Inicializador do Backend Local em C# + MySQL
echo ======================================================================
echo.
echo   Iniciando servidor na porta 5080 (http://localhost:5080)...
echo   [Dica] Para configurar a senha do MySQL local, edite mysql_config.txt
echo.

if exist "%~dp0BackendEstudoAPI.exe" (
    "%~dp0BackendEstudoAPI.exe" %*
) else (
    echo Compilando e executando a partir do codigo-fonte C#...
    dotnet run --project "%~dp0Source\BackendEstudoAPI.csproj"
)

echo.
pause
