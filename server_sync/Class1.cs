using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
                },
                new GameMenuItem
                {
                    Description = "[Downlink] Este Jogo",
                    MenuSection = "Sincronizar Dados",
                    Action = (a) => ConfirmarEImportar(a.Games)
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
                },
                new MainMenuItem
                {
                    Description = "[Downlink] Todos os Jogos",
                    MenuSection = "@Sincronizar Dados",
                    Action = (a) => ConfirmarEImportar(PlayniteApi.Database.Games.ToList())
                },
                new MainMenuItem
                {
                    Description = "[Downlink] Jogos Filtrados",
                    MenuSection = "@Sincronizar Dados",
                    Action = (a) => ConfirmarEImportar(PlayniteApi.MainView.FilteredGames.ToList())
                },
                new MainMenuItem
                {
                    Description = "[Downlink] Jogos Selecionados",
                    MenuSection = "@Sincronizar Dados",
                    Action = (a) => ConfirmarEImportar(PlayniteApi.MainView.SelectedGames.ToList())
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
            var opSemLog = new MessageBoxOption("Nenhum (Sem terminal)");
            var opCancelarLog = new MessageBoxOption("Cancelar", true, false);

            var escolhaLog = PlayniteApi.Dialogs.ShowMessage(
                "Escolha como acompanhar o envio:\n\n" +
                "• [SIM] Exibe no terminal o JSON completo enviado ao servidor.\n" +
                "• [NÃO] Exibe no terminal apenas o resumo do batch.\n" +
                "• [NENHUM] Não abre terminal; informa o resultado no Playnite.",
                "Nível de Log",
                MessageBoxImage.Question,
                new List<MessageBoxOption>
                {
                    opLogDetalhado, opLogSimples, opSemLog, opCancelarLog
                }
            );

            if (escolhaLog == opCancelarLog || escolhaLog == null) return;

            bool logDetalhado = (escolhaLog == opLogDetalhado);
            bool abrirTerminal = (escolhaLog != opSemLog);

            // Dispara o envio assíncrono para não travar a interface do Playnite
            Task.Run(() => ProcessarEnvioBatchAsync(
                jogos, incluirInstalacao, abrirTerminal, logDetalhado));
        }

        private async Task ProcessarEnvioBatchAsync(
            List<Game> jogos, bool incluirInstalacao,
            bool abrirTerminal, bool logDetalhado)
        {
            string arquivoResultadoTerminal = null;

            try
            {
                var dtos = jogos.Select(j => CriarDtoCustomizado(j, incluirInstalacao)).ToList();
                
                var payload = new SyncPayload { games = dtos };
                string jsonBody = Serialization.ToJson(payload, true);

                if (abrirTerminal)
                {
                    // Abre o terminal imediatamente. O relatório final será escrito
                    // nele quando a resposta do servidor chegar.
                    arquivoResultadoTerminal = IniciarLogTerminal(
                        jsonBody, jogos.Count, incluirInstalacao, logDetalhado);
                }

                // Envio HTTP POST
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await httpClient.PostAsync(ServerUrl, content);

                string responseText = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // Faz a leitura da resposta JSON do backend
                    var syncResult = Serialization.FromJson<SyncResponse>(responseText);

                    if (abrirTerminal)
                    {
                        string relatorioTerminal = GerarRelatorioTerminal(
                            syncResult, dtos, responseText, logDetalhado);
                        File.WriteAllText(
                            arquivoResultadoTerminal, relatorioTerminal, Encoding.UTF8);
                    }

                    // Popup do Playnite fica propositalmente curto.
                    string mensagemSucesso = GerarResumoPopup(
                        syncResult, jogos.Count, abrirTerminal);

                    PlayniteApi.Dialogs.ShowMessage(
                        mensagemSucesso,
                        "Resultado da Sincronização",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                else
                {
                    if (abrirTerminal)
                    {
                        File.WriteAllText(
                            arquivoResultadoTerminal,
                            GerarErroTerminal((int)response.StatusCode, responseText),
                            Encoding.UTF8);
                    }

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

            // Use explicitamente o overload antigo (char[]) porque o Playnite carrega
            // extensoes no .NET Framework 4.6.2. O overload TrimEnd(char), escolhido
            // por compiladores/runtimes mais novos, causa MissingMethodException.
            string normalizado = caminho.Trim().Replace('\\', '/').TrimEnd(new[] { '/' });
            int ultimoSeparador = normalizado.LastIndexOf('/');
            string nomeArquivo = ultimoSeparador >= 0
                ? normalizado.Substring(ultimoSeparador + 1)
                : normalizado;

            if (string.IsNullOrWhiteSpace(nomeArquivo) || nomeArquivo == "." || nomeArquivo == "..")
                return null;

            return $"/api/covers/playnite/{playniteId}/{nomeArquivo}";
        }

        private string GerarResumoPopup(
            SyncResponse syncResult, int totalEnviado, bool terminalAberto)
        {
            int processados = syncResult?.processed ?? 0;
            int pulados = syncResult?.skipped ?? 0;

            return "Sincronização concluída!\n\n" +
                   $"• Enviados: {totalEnviado}\n" +
                   $"• Processados: {processados}\n" +
                   $"• Pulados: {pulados}" +
                   (terminalAberto
                       ? "\n\nConsulte o terminal para o relatório."
                       : string.Empty);
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

        private const string PullServerUrl = "https://localhost:8000/api/sync/playnite/pull";
        private const string MustTestTag = "[Servidor] Precisa Testar";
        private const string InterestTagPrefix = "[Vontade de Jogar]";
        private const string ReplayTagPrefix = "[Vontade de Rejogar]";
        private static readonly HttpClient pullHttpClient = new HttpClient();

        public class PullRequest
        {
            public List<string> playnite_ids { get; set; } = new List<string>();
        }

        public class PullResponse
        {
            public int requested { get; set; }
            public int found { get; set; }
            public int not_found { get; set; }
            public List<PullGameDTO> games { get; set; } = new List<PullGameDTO>();
            public List<string> missing_playnite_ids { get; set; } = new List<string>();
        }

        public class PullGameDTO
        {
            public string playnite_id { get; set; }
            public List<string> genres { get; set; } = new List<string>();
            public string gameplay_status { get; set; }
            public bool favorite { get; set; }
            public bool must_test { get; set; }
            public int interest_rating { get; set; }
            public int? replay_score { get; set; }
            public decimal? score { get; set; }
            public string notes { get; set; }
            public string coop_players { get; set; }
            public List<string> coop_type { get; set; }
            public string coop_screen_type { get; set; }
            public List<string> multiplayer_mode { get; set; }
            public string input_recommendation { get; set; }
            public string updated_at { get; set; }
        }

        private class PullApplyResult
        {
            public bool Changed { get; set; }
            public List<string> ChangedFields { get; } = new List<string>();
            public List<string> Warnings { get; } = new List<string>();
        }

        private class PullGameReport
        {
            public string Name { get; set; }
            public PullApplyResult Result { get; set; }
        }

        private void ConfirmarEImportar(List<Game> jogos)
        {
            if (jogos == null || jogos.Count == 0)
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "Nenhum jogo selecionado para downlink.", "Sync Dashboard");
                return;
            }

            var detalhado = new MessageBoxOption("Sim (Exibir alterações)");
            var resumido = new MessageBoxOption("Não (Resumo)");
            var semLog = new MessageBoxOption("Nenhum (Sem terminal)");
            var cancelar = new MessageBoxOption("Cancelar", true, false);
            var escolha = PlayniteApi.Dialogs.ShowMessage(
                $"Você está prestes a consultar {jogos.Count} jogo(s) no servidor.\n\n" +
                "Escolha como acompanhar o downlink:\n\n" +
                "• [SIM] Exibe as alterações detalhadas no terminal.\n" +
                "• [NÃO] Exibe um resumo no terminal.\n" +
                "• [NENHUM] Não abre terminal; informa o resultado no Playnite.",
                "Confirmar Downlink",
                MessageBoxImage.Question,
                new List<MessageBoxOption> { detalhado, resumido, semLog, cancelar });

            if (escolha == cancelar || escolha == null)
                return;

            Task.Run(() => ProcessarImportacaoAsync(
                jogos, escolha != semLog, escolha == detalhado));
        }

        private async Task ProcessarImportacaoAsync(
            List<Game> jogos, bool abrirTerminal, bool logDetalhado)
        {
            string arquivoResultadoTerminal = null;
            try
            {
                var validos = (jogos ?? new List<Game>())
                    .Where(j => j != null && j.Id != Guid.Empty).ToList();
                var ids = validos.Select(j => j.Id.ToString())
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var payload = new PullRequest { playnite_ids = ids };
                string jsonBody = Serialization.ToJson(payload, true);
                if (abrirTerminal)
                {
                    arquivoResultadoTerminal = IniciarImportLogTerminal(
                        jsonBody, ids.Count, logDetalhado);
                }

                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                var response = await pullHttpClient.PostAsync(PullServerUrl, content);
                string responseText = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    if (abrirTerminal)
                    {
                        File.WriteAllText(arquivoResultadoTerminal,
                            GerarErroTerminal((int)response.StatusCode, responseText),
                            Encoding.UTF8);
                    }
                    PlayniteApi.Dialogs.ShowErrorMessage(
                        $"Erro no servidor (Status {(int)response.StatusCode}):\n{responseText}",
                        "Erro de Downlink");
                    return;
                }

                var result = Serialization.FromJson<PullResponse>(responseText)
                    ?? new PullResponse();
                var byId = validos.ToDictionary(
                    j => j.Id.ToString(), StringComparer.OrdinalIgnoreCase);
                var reports = new List<PullGameReport>();

                using (PlayniteApi.Database.BufferedUpdate())
                {
                    foreach (var remote in result.games ?? new List<PullGameDTO>())
                    {
                        if (remote == null || string.IsNullOrWhiteSpace(remote.playnite_id) ||
                            !byId.TryGetValue(remote.playnite_id, out var jogo))
                            continue;

                        var applied = AplicarDadosServidor(jogo, remote);
                        if (applied.Changed)
                            PlayniteApi.Database.Games.Update(jogo);
                        reports.Add(new PullGameReport { Name = jogo.Name, Result = applied });
                    }
                }

                int alterados = reports.Count(r => r.Result.Changed);
                int naoAlterados = reports.Count(r => !r.Result.Changed);
                int naoEncontrados = result.missing_playnite_ids?.Count ?? 0;
                if (abrirTerminal)
                {
                    File.WriteAllText(arquivoResultadoTerminal,
                        GerarRelatorioImportacao(
                            result, reports, responseText, logDetalhado),
                        Encoding.UTF8);
                }

                PlayniteApi.Dialogs.ShowMessage(
                    "Downlink concluído!\n\n" +
                    $"• Alterados: {alterados}\n" +
                    $"• Não alterados: {naoAlterados}\n" +
                    $"• Não encontrados: {naoEncontrados}" +
                    (abrirTerminal
                        ? "\n\nConsulte o terminal para o relatório."
                        : string.Empty),
                    "Resultado do Downlink", MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(arquivoResultadoTerminal))
                {
                    try
                    {
                        File.WriteAllText(arquivoResultadoTerminal,
                            GerarExcecaoTerminal(ex), Encoding.UTF8);
                    }
                    catch { }
                }
                PlayniteApi.Dialogs.ShowErrorMessage(
                    $"Falha de conexão com o servidor:\n{ex.Message}",
                    "Erro de Conexão");
            }
        }

        private PullApplyResult AplicarDadosServidor(Game jogo, PullGameDTO dados)
        {
            var result = new PullApplyResult();
            if (AplicarGeneros(jogo, dados.genres ?? new List<string>()))
                result.ChangedFields.Add("gêneros");
            if (AplicarStatus(jogo, dados.gameplay_status, result.Warnings))
                result.ChangedFields.Add("status");
            if (jogo.Favorite != dados.favorite)
            {
                jogo.Favorite = dados.favorite;
                result.ChangedFields.Add("favorito");
            }
            if (AplicarUserScore(jogo, dados, result.Warnings))
                result.ChangedFields.Add("nota de usuário");
            if (AplicarTagsGerenciadas(jogo, dados, result.Warnings))
                result.ChangedFields.Add("tags gerenciadas");
            if (AplicarTagsServidor(jogo, dados))
                result.ChangedFields.Add("cooperação e input");
            // A presença dos novos campos identifica servidores que suportam notas no pull.
            if (dados.coop_players != null && jogo.Notes != dados.notes)
            {
                jogo.Notes = dados.notes;
                result.ChangedFields.Add("observações");
            }

            result.Changed = result.ChangedFields.Count > 0;

            return result;
        }

        private bool AplicarTagsServidor(Game jogo, PullGameDTO dados)
        {
            // Um servidor antigo não envia estes campos; preserve as tags existentes.
            if (dados.coop_type == null || dados.coop_players == null ||
                dados.coop_screen_type == null || dados.input_recommendation == null)
                return false;

            var nomes = dados.coop_type.Count == 0
                ? new List<string> { "[Servidor] Coop: Nenhum" }
                : dados.coop_type.Select(v => $"[Servidor] Coop: {v}").ToList();
            nomes.Add($"[Servidor] Jogadores: {dados.coop_players}");
            nomes.Add($"[Servidor] Tela: {dados.coop_screen_type}");
            nomes.Add($"[Servidor] Input: {dados.input_recommendation}");
            // Servidores antigos não enviam modos: preserve as tags existentes.
            if (dados.multiplayer_mode != null)
            {
                nomes.AddRange(dados.multiplayer_mode.Count == 0
                    ? new List<string> { "[Servidor] Modo: Nenhum" }
                    : dados.multiplayer_mode.Select(v => $"[Servidor] Modo: {v}"));
            }
            var desired = nomes.Select(ObterOuCriarTag).Select(t => t.Id).Distinct().ToList();
            var ids = new List<Guid>(jogo.TagIds ?? new List<Guid>());
            string[] prefixos = { "[Servidor] Coop:", "[Servidor] Jogadores:",
                "[Servidor] Tela:", "[Servidor] Input:" };
            if (dados.multiplayer_mode != null)
                prefixos = prefixos.Concat(new[] { "[Servidor] Modo:" }).ToArray();
            var antigos = PlayniteApi.Database.Tags.Where(t => ids.Contains(t.Id) &&
                prefixos.Any(p => (t.Name ?? "").StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                .Select(t => t.Id).ToList();
            if (antigos.Count == desired.Count && antigos.All(desired.Contains))
                return false;
            ids.RemoveAll(id => antigos.Contains(id));
            ids.AddRange(desired.Where(id => !ids.Contains(id)));
            jogo.TagIds = ids;
            return true;
        }

        private bool AplicarGeneros(Game jogo, List<string> nomes)
        {
            var desired = new List<Guid>();
            foreach (string raw in nomes)
            {
                string nome = (raw ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(nome))
                    continue;
                var genero = PlayniteApi.Database.Genres.FirstOrDefault(g =>
                    string.Equals(g.Name, nome, StringComparison.OrdinalIgnoreCase));
                if (genero == null)
                {
                    genero = new Genre(nome);
                    PlayniteApi.Database.Genres.Add(genero);
                }
                if (!desired.Contains(genero.Id))
                    desired.Add(genero.Id);
            }

            var current = jogo.GenreIds ?? new List<Guid>();
            if (current.Count == desired.Count && current.All(desired.Contains))
                return false;
            jogo.GenreIds = desired;
            return true;
        }

        private bool AplicarStatus(Game jogo, string status, List<string> warnings)
        {
            var aliases = AliasesStatus(status);
            var target = PlayniteApi.Database.CompletionStatuses.FirstOrDefault(s =>
                aliases.Any(alias => string.Equals(
                    s.Name?.Trim(), alias, StringComparison.OrdinalIgnoreCase)));
            if (target == null)
            {
                warnings.Add($"status '{status}' não encontrado no Playnite");
                return false;
            }
            if (jogo.CompletionStatusId == target.Id)
                return false;
            jogo.CompletionStatusId = target.Id;
            return true;
        }

        private bool AplicarUserScore(Game jogo, PullGameDTO dados, List<string> warnings)
        {
            // A nota pessoal vem em escala 0–10. replay_score representa
            // apenas a vontade de rejogar e é aplicado como tag gerenciada.
            if (!dados.score.HasValue)
                return false;

            if (!TentarConverterScore(dados.score, out int desired))
            {
                warnings.Add($"score inválido: '{dados.score}'");
                return false;
            }

            if (jogo.UserScore == desired)
                return false;

            jogo.UserScore = desired;
            return true;
        }

        private static bool TentarConverterScore(decimal? score, out int userScore)
        {
            userScore = 0;
            if (!score.HasValue || score.Value < 0 || score.Value > 10)
                return false;

            userScore = (int)Math.Round(
                score.Value * 10m, MidpointRounding.AwayFromZero);
            return true;
        }

        private bool AplicarTagsGerenciadas(
            Game jogo, PullGameDTO dados, List<string> warnings)
        {
            if (dados.interest_rating < 1 || dados.interest_rating > 5)
            {
                warnings.Add("interest_rating fora da escala 1-5");
                return false;
            }

            var currentIds = new List<Guid>(jogo.TagIds ?? new List<Guid>());
            var currentTags = PlayniteApi.Database.Tags
                .Where(tag => currentIds.Contains(tag.Id)).ToList();
            var interestTag = ObterOuCriarTag(
                $"{InterestTagPrefix} {dados.interest_rating}");
            var mustTestTag = dados.must_test ? ObterOuCriarTag(MustTestTag) : null;
            bool replayValido = !dados.replay_score.HasValue ||
                (dados.replay_score.Value >= 1 && dados.replay_score.Value <= 5);
            if (!replayValido)
                warnings.Add("replay_score fora da escala 1-5");
            var replayTag = replayValido && dados.replay_score.HasValue
                ? ObterOuCriarTag($"{ReplayTagPrefix} {dados.replay_score.Value}")
                : null;
            bool changed = false;

            foreach (var tag in currentTags)
            {
                bool oldInterest = EhTagDeVontadeGerenciada(tag.Name);
                bool oldReplay = EhTagDeReplayGerenciada(tag.Name);
                bool mustTest = string.Equals(
                    tag.Name, MustTestTag, StringComparison.OrdinalIgnoreCase);
                bool desiredInterest = tag.Id == interestTag.Id;
                bool desiredReplay = replayTag != null && tag.Id == replayTag.Id;
                bool desiredMustTest = mustTestTag != null && tag.Id == mustTestTag.Id;
                if ((oldInterest && !desiredInterest) ||
                    (oldReplay && replayValido && !desiredReplay) ||
                    (mustTest && !desiredMustTest))
                {
                    currentIds.Remove(tag.Id);
                    changed = true;
                }
            }

            if (!currentIds.Contains(interestTag.Id))
            {
                currentIds.Add(interestTag.Id);
                changed = true;
            }
            if (dados.must_test && !currentIds.Contains(mustTestTag.Id))
            {
                currentIds.Add(mustTestTag.Id);
                changed = true;
            }
            if (replayTag != null && !currentIds.Contains(replayTag.Id))
            {
                currentIds.Add(replayTag.Id);
                changed = true;
            }
            if (changed)
                jogo.TagIds = currentIds;
            return changed;
        }

        private Tag ObterOuCriarTag(string nome)
        {
            var tag = PlayniteApi.Database.Tags.FirstOrDefault(t =>
                string.Equals(t.Name, nome, StringComparison.OrdinalIgnoreCase));
            if (tag != null)
                return tag;
            tag = new Tag(nome);
            PlayniteApi.Database.Tags.Add(tag);
            return tag;
        }

        private static bool EhTagDeVontadeGerenciada(string nome)
        {
            if (string.IsNullOrWhiteSpace(nome) ||
                !nome.StartsWith(InterestTagPrefix, StringComparison.OrdinalIgnoreCase))
                return false;
            string raw = nome.Substring(InterestTagPrefix.Length).Trim();
            return int.TryParse(raw, out int value) && value >= 1 && value <= 5;
        }

        private static bool EhTagDeReplayGerenciada(string nome)
        {
            if (string.IsNullOrWhiteSpace(nome) ||
                !nome.StartsWith(ReplayTagPrefix, StringComparison.OrdinalIgnoreCase))
                return false;
            string raw = nome.Substring(ReplayTagPrefix.Length).Trim();
            return int.TryParse(raw, out int value) && value >= 1 && value <= 5;
        }

        private static string[] AliasesStatus(string status)
        {
            switch ((status ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "backlog":
                    return new[] { "Backlog", "Not Played", "NotPlayed", "Plan to Play" };
                case "jogando":
                case "playing":
                    return new[] { "Jogando", "Playing" };
                case "finalizado":
                case "completed":
                    return new[] { "Finalizado", "Completed" };
                case "abandonado":
                case "abandoned":
                    return new[] { "Abandonado", "Abandoned" };
                default:
                    return new[] { status ?? string.Empty };
            }
        }

        private string GerarRelatorioImportacao(
            PullResponse result, List<PullGameReport> reports,
            string responseJson, bool logDetalhado)
        {
            var log = new StringBuilder();
            log.AppendLine();
            log.AppendLine("[RESPOSTA RECEBIDA]");
            log.AppendLine("---------------------------------------------------------");
            log.AppendLine("HTTP 200 - consulta concluída.");
            if (logDetalhado)
            {
                log.AppendLine();
                log.AppendLine("[JSON DE RESPOSTA DO SERVIDOR]");
                log.AppendLine("---------------------------------------------------------");
                log.AppendLine(responseJson);
            }

            log.AppendLine();
            log.AppendLine("[JOGOS CONSULTADOS]");
            log.AppendLine("---------------------------------------------------------");
            foreach (var report in reports)
            {
                log.AppendLine($"• {report.Name} " +
                    (report.Result.Changed ? "[ALTERADO]" : "[NÃO ALTERADO]"));
                if (logDetalhado && report.Result.ChangedFields.Count > 0)
                    log.AppendLine($"  Campos: {string.Join(", ", report.Result.ChangedFields)}");
                foreach (string warning in report.Result.Warnings)
                    log.AppendLine($"  Aviso: {warning}");
            }

            var missing = result.missing_playnite_ids ?? new List<string>();
            if (missing.Count > 0)
            {
                log.AppendLine();
                log.AppendLine($"[NÃO ENCONTRADOS ({missing.Count})]");
                log.AppendLine("---------------------------------------------------------");
                foreach (string id in missing)
                    log.AppendLine($"• {id}");
            }

            log.AppendLine();
            log.AppendLine("=========================================================");
            log.AppendLine(" RESUMO");
            log.AppendLine($" Alterados:       {reports.Count(r => r.Result.Changed)}");
            log.AppendLine($" Não alterados:   {reports.Count(r => !r.Result.Changed)}");
            log.AppendLine($" Não encontrados: {missing.Count}");
            log.AppendLine("=========================================================");
            return log.ToString();
        }

        private string IniciarImportLogTerminal(string json, int total, bool detalhado)
        {
            string id = Guid.NewGuid().ToString("N");
            string pasta = Path.Combine(Path.GetTempPath(), "playnite_sync");
            Directory.CreateDirectory(pasta);
            string inicio = Path.Combine(pasta, $"pull_{id}_inicio.txt");
            string resultado = Path.Combine(pasta, $"pull_{id}_resultado.txt");
            string batPath = Path.Combine(pasta, $"pull_{id}.bat");

            var inicial = new StringBuilder();
            inicial.AppendLine("=========================================================");
            inicial.AppendLine(" PLAYNITE PULL");
            inicial.AppendLine($" DESTINO: {PullServerUrl}");
            inicial.AppendLine($" TOTAL CONSULTADO: {total}");
            inicial.AppendLine($" LOG DETALHADO: {detalhado}");
            inicial.AppendLine("=========================================================");
            inicial.AppendLine();
            inicial.AppendLine("Consultando jogos no servidor...");
            if (detalhado)
            {
                inicial.AppendLine();
                inicial.AppendLine("[JSON ENVIADO]");
                inicial.AppendLine("---------------------------------------------------------");
                inicial.AppendLine(json);
            }
            File.WriteAllText(inicio, inicial.ToString(), Encoding.UTF8);

            var bat = new StringBuilder();
            bat.AppendLine("@echo off");
            bat.AppendLine("chcp 65001 > nul");
            bat.AppendLine($"title Playnite Pull - {total} jogos");
            bat.AppendLine($"type \"{inicio}\"");
            bat.AppendLine(":aguardar_resultado");
            bat.AppendLine($"if not exist \"{resultado}\" (");
            bat.AppendLine("  timeout /t 1 /nobreak > nul");
            bat.AppendLine("  goto aguardar_resultado");
            bat.AppendLine(")");
            bat.AppendLine($"type \"{resultado}\"");
            bat.AppendLine("echo.");
            bat.AppendLine("echo Pressione qualquer tecla para fechar...");
            bat.AppendLine("pause > nul");
            bat.AppendLine($"del /q \"{inicio}\" > nul 2>&1");
            bat.AppendLine($"del /q \"{resultado}\" > nul 2>&1");
            bat.AppendLine($"del /q \"{batPath}\" > nul 2>&1");
            File.WriteAllText(batPath, bat.ToString(), Encoding.UTF8);
            Process.Start(new ProcessStartInfo
            {
                FileName = batPath,
                UseShellExecute = true
            });
            return resultado;
        }
    }
}
