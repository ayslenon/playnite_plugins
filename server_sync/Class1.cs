using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace SyncServerPlaynite
{
    // Wrapper exigido pela sua API Backend para o envio
    public class SyncPayload
    {
        public List<GameDTO> games { get; set; } = new List<GameDTO>();
    }

    // Estruturas de resposta retornadas pelo seu servidor
    public class SyncResponse
    {
        public int processed { get; set; }
        public int skipped { get; set; }
        public List<SyncResultItem> results { get; set; } = new List<SyncResultItem>();
    }

    public class SyncResultItem
    {
        public string id { get; set; }
        public string title { get; set; }
        public string action { get; set; }
    }

    public class GameDTO
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string InstallDirectory { get; set; }
        public bool? IsInstalled { get; set; }
        public int Playtime { get; set; }
        public List<string> Genres { get; set; } = new List<string>();
        public List<string> Platforms { get; set; } = new List<string>();
        public string Source { get; set; }
        public string CompletionStatus { get; set; }
        public bool Favorite { get; set; }
        public List<string> Categories { get; set; } = new List<string>();
        public List<string> Tags { get; set; } = new List<string>();
        public List<string> Features { get; set; } = new List<string>();
        public string CoverImage { get; set; }
        public string BackgroundImage { get; set; }
        public bool Hidden { get; set; }
        public int PlayCount { get; set; }
        public string Added { get; set; }
        public string Modified { get; set; }
        public string Notes { get; set; }
    }

    public class SyncServerPlugin : GenericPlugin
    {
        public override Guid Id { get; } = Guid.Parse("a1b2c3d4-e5f6-7890-1234-56789abcdef0");
        
        // URL do Servidor
        private const string ServerUrl = "https://localhost:8000/api/sync/playnite";
        private static readonly HttpClient httpClient = new HttpClient();

        public SyncServerPlugin(IPlayniteAPI api) : base(api)
        {
            Properties = new GenericPluginProperties
            {
                HasSettings = false
            };
        }

        // 1. MENU DO BOTÃO DIREITO NO JOGO
        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            return new List<GameMenuItem>
            {
                new GameMenuItem
                {
                    Description = "[Uplink] Este Jogo",
                    MenuSection = "Sincronizar Dados",
                    Action = (a) => ConfirmarEEnviar(a.Games)
                }
            };
        }

        // 2. MENU PRINCIPAL (CANTO SUPERIOR ESQUERDO)
        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            return new List<MainMenuItem>
            {
                new MainMenuItem
                {
                    Description = "[Uplink] Todos os Jogos",
                    MenuSection = "@Sincronizar Dados",
                    Action = (a) => ConfirmarEEnviar(PlayniteApi.Database.Games.ToList())
                },
                new MainMenuItem
                {
                    Description = "[Uplink] Jogos Filtrados",
                    MenuSection = "@Sincronizar Dados",
                    Action = (a) => ConfirmarEEnviar(PlayniteApi.MainView.FilteredGames.ToList())
                },
                new MainMenuItem
                {
                    Description = "[Uplink] Jogos Selecionados",
                    MenuSection = "@Sincronizar Dados",
                    Action = (a) => ConfirmarEEnviar(PlayniteApi.MainView.SelectedGames.ToList())
                }
            };
        }

        private void ConfirmarEEnviar(List<Game> jogos)
        {
            if (jogos == null || jogos.Count == 0)
            {
                PlayniteApi.Dialogs.ShowErrorMessage("Nenhum jogo selecionado para envio.", "Sync Dashboard");
                return;
            }

            var opSim = new MessageBoxOption("Sim (Incluir Instalação)");
            var opNao = new MessageBoxOption("Não (Apenas Metadados)");
            var opCancelar = new MessageBoxOption("Cancelar", true, false);

            var escolha = PlayniteApi.Dialogs.ShowMessage(
                $"Você está prestes a enviar {jogos.Count} jogo(s) para a Dashboard.\n\n" +
                "Deseja incluir os PARÂMETROS DE INSTALAÇÃO (Status do HD e diretório)?\n\n" +
                "• [SIM] Envia dados completos incluindo estado do HD.\n" +
                "• [NÃO] Envia apenas metadados (gêneros, horas jogadas, notas, etc.).",
                "Confirmar Sincronização",
                MessageBoxImage.Question,
                new List<MessageBoxOption> { opSim, opNao, opCancelar }
            );

            if (escolha == opCancelar || escolha == null) return;

            bool incluirInstalacao = (escolha == opSim);

            var opLogDetalhado = new MessageBoxOption("Sim (Exibir JSON)");
            var opLogSimples = new MessageBoxOption("Não (Log Resumido)");
            var opCancelarLog = new MessageBoxOption("Cancelar", true, false);

            var escolhaLog = PlayniteApi.Dialogs.ShowMessage(
                "Deseja exibir o log detalhado do envio?\n\n" +
                "• [SIM] Exibe no terminal o JSON completo enviado ao servidor.\n" +
                "• [NÃO] Exibe apenas informações resumidas do batch.",
                "Nível de Log",
                MessageBoxImage.Question,
                new List<MessageBoxOption> { opLogDetalhado, opLogSimples, opCancelarLog }
            );

            if (escolhaLog == opCancelarLog || escolhaLog == null) return;

            bool logDetalhado = (escolhaLog == opLogDetalhado);

            // Dispara o envio assíncrono para não travar a interface do Playnite
            Task.Run(() => ProcessarEnvioBatchAsync(jogos, incluirInstalacao, logDetalhado));
        }

        private async Task ProcessarEnvioBatchAsync(List<Game> jogos, bool incluirInstalacao, bool logDetalhado)
        {
            string arquivoResultadoTerminal = null;

            try
            {
                var dtos = jogos.Select(j => CriarDtoCustomizado(j, incluirInstalacao)).ToList();
                
                var payload = new SyncPayload { games = dtos };
                string jsonBody = Serialization.ToJson(payload, true);

                // Abre o terminal imediatamente. O relatório final será escrito nele
                // quando a resposta do servidor chegar.
                arquivoResultadoTerminal = IniciarLogTerminal(
                    jsonBody, jogos.Count, incluirInstalacao, logDetalhado);

                // Envio HTTP POST
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await httpClient.PostAsync(ServerUrl, content);

                string responseText = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // Faz a leitura da resposta JSON do backend
                    var syncResult = Serialization.FromJson<SyncResponse>(responseText);

                    // Relatório completo fica no terminal.
                    string relatorioTerminal = GerarRelatorioTerminal(syncResult, dtos, responseText, logDetalhado);
                    File.WriteAllText(arquivoResultadoTerminal, relatorioTerminal, Encoding.UTF8);

                    // Popup do Playnite fica propositalmente curto.
                    string mensagemSucesso = GerarResumoPopup(syncResult, jogos.Count);

                    PlayniteApi.Dialogs.ShowMessage(
                        mensagemSucesso,
                        "Resultado da Sincronização",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                else
                {
                    File.WriteAllText(
                        arquivoResultadoTerminal,
                        GerarErroTerminal((int)response.StatusCode, responseText),
                        Encoding.UTF8);

                    PlayniteApi.Dialogs.ShowErrorMessage(
                        $"Erro no servidor (Status {(int)response.StatusCode}):\n{responseText}",
                        "Erro de Sincronização"
                    );
                }
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(arquivoResultadoTerminal))
                {
                    try
                    {
                        File.WriteAllText(
                            arquivoResultadoTerminal,
                            GerarExcecaoTerminal(ex),
                            Encoding.UTF8);
                    }
                    catch
                    {
                        // Se o próprio log falhar, preserva o erro original no popup.
                    }
                }

                PlayniteApi.Dialogs.ShowErrorMessage(
                    $"Falha de conexão com o servidor:\n{ex.Message}",
                    "Erro de Conexão"
                );
            }
        }

        private GameDTO CriarDtoCustomizado(Game jogo, bool incluirInstalacao)
        {
            var dto = new GameDTO
            {
                Id = jogo.Id.ToString(),
                Name = jogo.Name,
                InstallDirectory = jogo.InstallDirectory,
                IsInstalled = incluirInstalacao ? (bool?)jogo.IsInstalled : null,
                Playtime = Convert.ToInt32(jogo.Playtime),
                Favorite = jogo.Favorite,
                CoverImage = CriarLinkRelativoPlaynite(jogo.Id.ToString(), jogo.CoverImage),
                BackgroundImage = CriarLinkRelativoPlaynite(jogo.Id.ToString(), jogo.BackgroundImage),
                Hidden = jogo.Hidden,
                PlayCount = Convert.ToInt32(jogo.PlayCount),
                Added = jogo.Added?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Modified = jogo.Modified?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Notes = jogo.Notes
            };

            if (jogo.GenreIds != null)
                dto.Genres = PlayniteApi.Database.Genres.Where(g => jogo.GenreIds.Contains(g.Id)).Select(g => g.Name).ToList();

            if (jogo.PlatformIds != null)
                dto.Platforms = PlayniteApi.Database.Platforms.Where(p => jogo.PlatformIds.Contains(p.Id)).Select(p => p.Name).ToList();

            if (jogo.CategoryIds != null)
                dto.Categories = PlayniteApi.Database.Categories.Where(c => jogo.CategoryIds.Contains(c.Id)).Select(c => c.Name).ToList();

            if (jogo.TagIds != null)
                dto.Tags = PlayniteApi.Database.Tags.Where(t => jogo.TagIds.Contains(t.Id)).Select(t => t.Name).ToList();

            if (jogo.FeatureIds != null)
                dto.Features = PlayniteApi.Database.Features.Where(f => jogo.FeatureIds.Contains(f.Id)).Select(f => f.Name).ToList();

            if (jogo.SourceId != Guid.Empty)
            {
                var src = PlayniteApi.Database.Sources.FirstOrDefault(s => s.Id == jogo.SourceId);
                dto.Source = src?.Name;
            }

            if (jogo.CompletionStatusId != Guid.Empty)
            {
                var st = PlayniteApi.Database.CompletionStatuses.FirstOrDefault(s => s.Id == jogo.CompletionStatusId);
                dto.CompletionStatus = st?.Name;
            }

            return dto;
        }

        private static string CriarLinkRelativoPlaynite(string playniteId, string caminho)
        {
            if (string.IsNullOrWhiteSpace(playniteId) || string.IsNullOrWhiteSpace(caminho))
                return null;

            string normalizado = caminho.Trim().Replace('\\', '/').TrimEnd('/');
            int ultimoSeparador = normalizado.LastIndexOf('/');
            string nomeArquivo = ultimoSeparador >= 0
                ? normalizado.Substring(ultimoSeparador + 1)
                : normalizado;

            if (string.IsNullOrWhiteSpace(nomeArquivo) || nomeArquivo == "." || nomeArquivo == "..")
                return null;

            return $"/api/covers/playnite/{playniteId}/{nomeArquivo}";
        }

        private string GerarResumoPopup(SyncResponse syncResult, int totalEnviado)
        {
            int processados = syncResult?.processed ?? 0;
            int pulados = syncResult?.skipped ?? 0;

            return "Sincronização concluída!\n\n" +
                   $"• Enviados: {totalEnviado}\n" +
                   $"• Processados: {processados}\n" +
                   $"• Pulados: {pulados}\n\n" +
                   "Consulte o terminal para o relatório detalhado.";
        }

        private string GerarRelatorioTerminal(
            SyncResponse syncResult,
            List<GameDTO> jogosEnviados,
            string responseJson,
            bool logDetalhado)
        {
            int processados = syncResult?.processed ?? 0;
            int puladosBackend = syncResult?.skipped ?? 0;
            var resultados = syncResult?.results ?? new List<SyncResultItem>();
            var enviados = jogosEnviados ?? new List<GameDTO>();

            // O backend só devolve em results os jogos que foram processados.
            // Portanto, os pulados não têm uma action própria: identificamos quais
            // jogos enviados NÃO aparecem na resposta, comparando pelo ID do Playnite.
            var idsRetornados = new HashSet<string>(
                resultados
                    .Where(r => !string.IsNullOrWhiteSpace(r?.id))
                    .Select(r => r.id),
                StringComparer.OrdinalIgnoreCase);

            var jogosPulados = enviados
                .Where(j => !string.IsNullOrWhiteSpace(j?.Id) && !idsRetornados.Contains(j.Id))
                .ToList();

            // Se houver algum item processado sem ID na resposta, deixa explícito no log.
            int resultadosSemId = resultados.Count(r => string.IsNullOrWhiteSpace(r?.id));

            var relatorio = new StringBuilder();
            relatorio.AppendLine();
            relatorio.AppendLine("[RESPOSTA RECEBIDA]");
            relatorio.AppendLine("---------------------------------------------------------");
            relatorio.AppendLine("HTTP 200 - sincronização concluída.");

            if (logDetalhado)
            {
                relatorio.AppendLine();
                relatorio.AppendLine("[JSON DE RESPOSTA DO SERVIDOR]");
                relatorio.AppendLine("---------------------------------------------------------");
                relatorio.AppendLine(responseJson);
            }

            relatorio.AppendLine();
            relatorio.AppendLine("[RELATORIO]");
            relatorio.AppendLine();

            // No log resumido mostramos só o contador. No detalhado, mostramos
            // cada item efetivamente retornado pelo backend e sua action.
            if (logDetalhado)
            {
                relatorio.AppendLine($"PROCESSADOS ({resultados.Count})");
                relatorio.AppendLine("---------------------------------------------------------");

                if (resultados.Count == 0)
                {
                    relatorio.AppendLine("(nenhum)");
                }
                else
                {
                    foreach (var item in resultados)
                    {
                        string nome = item?.title ?? item?.id ?? "(sem título)";
                        string acao = string.IsNullOrWhiteSpace(item?.action)
                            ? "sem action"
                            : item.action;

                        relatorio.AppendLine($"• {nome} [{acao}]");
                    }
                }

                relatorio.AppendLine();
            }
            else
            {
                relatorio.AppendLine($"PROCESSADOS: {processados}");
                relatorio.AppendLine();
            }

            relatorio.AppendLine($"PULADOS / NAO RETORNADOS ({jogosPulados.Count})");
            relatorio.AppendLine("---------------------------------------------------------");

            if (jogosPulados.Count == 0)
            {
                relatorio.AppendLine("(nenhum identificado pela comparacao de IDs)");
            }
            else
            {
                foreach (var jogo in jogosPulados)
                {
                    string nome = jogo?.Name ?? jogo?.Id ?? "(sem título)";
                    relatorio.AppendLine($"• {nome}");
                    relatorio.AppendLine($"  ID: {jogo?.Id ?? "(sem id)"}");
                    relatorio.AppendLine($"  Source: {jogo?.Source ?? "(sem source)"}");
                    relatorio.AppendLine($"  Instalado: {(jogo?.IsInstalled.HasValue == true ? jogo.IsInstalled.Value.ToString() : "(nao enviado)")}");
                    relatorio.AppendLine($"  Diretorio: {jogo?.InstallDirectory ?? "(vazio)"}");
                    relatorio.AppendLine();
                }
            }

            // Diagnostico extra: se o backend diz que houve skips mas todos os IDs
            // enviados apareceram em results, então a API está contabilizando skip
            // por uma regra interna que não é representada na lista results.
            if (puladosBackend > 0 && jogosPulados.Count == 0)
            {
                relatorio.AppendLine("[DIAGNOSTICO]");
                relatorio.AppendLine("O backend informou jogo(s) pulado(s), mas nenhum ID enviado ficou ausente de results.");
                relatorio.AppendLine("Neste caso o motivo do skip so pode ser descoberto no backend.");
                relatorio.AppendLine("Execute com LOG DETALHADO para conferir o JSON bruto da resposta.");
                relatorio.AppendLine();
            }

            relatorio.AppendLine();
            relatorio.AppendLine("=========================================================");
            relatorio.AppendLine(" RESUMO");
            relatorio.AppendLine($" Enviados:    {enviados.Count}");
            relatorio.AppendLine($" Processados: {processados}");
            relatorio.AppendLine($" Pulados:     {puladosBackend}");
            relatorio.AppendLine("---------------------------------------------------------");
            relatorio.AppendLine($" Identificados como pulados por ID: {jogosPulados.Count}");
            relatorio.AppendLine("=========================================================");

            // Agora a comparação correta é entre o contador skipped do backend e
            // os IDs enviados que não vieram em results.
            if (puladosBackend != jogosPulados.Count)
            {
                relatorio.AppendLine();
                relatorio.AppendLine("[AVISO]");
                relatorio.AppendLine(
                    $"O backend informou {puladosBackend} pulado(s), mas a comparação " +
                    $"dos IDs encontrou {jogosPulados.Count}.");
                relatorio.AppendLine(
                    "Isso indica que a resposta do backend não contém um ID para cada " +
                    "jogo processado, ou usa uma regra de contagem diferente.");
            }

            if (resultadosSemId > 0)
            {
                relatorio.AppendLine();
                relatorio.AppendLine($"[AVISO] {resultadosSemId} resultado(s) vieram sem campo id.");
            }

            return relatorio.ToString();
        }

        private string GerarErroTerminal(int statusCode, string responseText)
        {
            var log = new StringBuilder();
            log.AppendLine();
            log.AppendLine("[ERRO DO SERVIDOR]");
            log.AppendLine("---------------------------------------------------------");
            log.AppendLine($"HTTP {statusCode}");
            log.AppendLine(responseText);
            log.AppendLine();
            log.AppendLine("=========================================================");
            log.AppendLine(" SINCRONIZACAO FALHOU");
            log.AppendLine("=========================================================");
            return log.ToString();
        }


        private string GerarExcecaoTerminal(Exception ex)
        {
            var log = new StringBuilder();
            log.AppendLine();
            log.AppendLine("[ERRO DE CONEXAO]");
            log.AppendLine("---------------------------------------------------------");
            log.AppendLine(ex.Message);
            log.AppendLine();
            log.AppendLine("=========================================================");
            log.AppendLine(" SINCRONIZACAO FALHOU");
            log.AppendLine("=========================================================");
            return log.ToString();
        }

        private string IniciarLogTerminal(string json, int total, bool comInstalacao, bool logDetalhado)
        {
            string idSessao = Guid.NewGuid().ToString("N");
            string pastaTemp = Path.Combine(Path.GetTempPath(), "playnite_sync");
            Directory.CreateDirectory(pastaTemp);

            string arquivoInicial = Path.Combine(pastaTemp, $"sync_{idSessao}_inicio.txt");
            string arquivoResultado = Path.Combine(pastaTemp, $"sync_{idSessao}_resultado.txt");
            string arquivoBat = Path.Combine(pastaTemp, $"sync_{idSessao}.bat");

            var logInicial = new StringBuilder();
            logInicial.AppendLine("=========================================================");
            logInicial.AppendLine(" PLAYNITE SYNC");
            logInicial.AppendLine($" DESTINO: {ServerUrl}");
            logInicial.AppendLine($" TOTAL ENVIADO: {total}");
            logInicial.AppendLine($" INCLUI STATUS DE INSTALACAO: {comInstalacao}");
            logInicial.AppendLine($" LOG DETALHADO: {logDetalhado}");
            logInicial.AppendLine("=========================================================");
            logInicial.AppendLine();
            logInicial.AppendLine("Preparando jogos...");

            if (logDetalhado)
            {
                logInicial.AppendLine();
                logInicial.AppendLine("[JSON ENVIADO]");
                logInicial.AppendLine("---------------------------------------------------------");
                logInicial.AppendLine(json);
            }
            else
            {
                logInicial.AppendLine("JSON de envio omitido (log resumido).");
            }

            logInicial.AppendLine();
            logInicial.AppendLine("Enviando para o servidor...");
            logInicial.AppendLine("Aguardando resposta...");

            File.WriteAllText(arquivoInicial, logInicial.ToString(), Encoding.UTF8);

            var bat = new StringBuilder();
            bat.AppendLine("@echo off");
            bat.AppendLine("chcp 65001 > nul");
            bat.AppendLine($"title Playnite Sync - Batch ({total} jogos)");
            bat.AppendLine($"type \"{arquivoInicial}\"");
            bat.AppendLine(":aguardar_resultado");
            bat.AppendLine($"if not exist \"{arquivoResultado}\" (");
            bat.AppendLine("  timeout /t 1 /nobreak > nul");
            bat.AppendLine("  goto aguardar_resultado");
            bat.AppendLine(")");
            bat.AppendLine($"type \"{arquivoResultado}\"");
            bat.AppendLine("echo.");
            bat.AppendLine("echo Pressione qualquer tecla para fechar...");
            bat.AppendLine("pause > nul");
            bat.AppendLine($"del /q \"{arquivoInicial}\" > nul 2>&1");
            bat.AppendLine($"del /q \"{arquivoResultado}\" > nul 2>&1");
            bat.AppendLine($"del /q \"{arquivoBat}\" > nul 2>&1");

            File.WriteAllText(arquivoBat, bat.ToString(), Encoding.UTF8);

            Process.Start(new ProcessStartInfo
            {
                FileName = arquivoBat,
                UseShellExecute = true
            });

            return arquivoResultado;
        }

    }
}
