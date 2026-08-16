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

            // Dispara o envio assíncrono para não travar a interface do Playnite
            Task.Run(() => ProcessarEnvioBatchAsync(jogos, incluirInstalacao));
        }

        private async Task ProcessarEnvioBatchAsync(List<Game> jogos, bool incluirInstalacao)
        {
            try
            {
                var dtos = jogos.Select(j => CriarDtoCustomizado(j, incluirInstalacao)).ToList();
                
                var payload = new SyncPayload { games = dtos };
                string jsonBody = Serialization.ToJson(payload, true);

                // Log em janela CMD (debug local)
                ExibirLogTerminal(jsonBody, jogos.Count, incluirInstalacao);

                // Envio HTTP POST
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await httpClient.PostAsync(ServerUrl, content);

                string responseText = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // Faz a leitura da resposta JSON do backend
                    var syncResult = Serialization.FromJson<SyncResponse>(responseText);

                    string mensagemSucesso = "Sincronização concluída!\n\n" +
                                             $"• Processados: {syncResult?.processed ?? 0}\n" +
                                             $"• Pulados: {syncResult?.skipped ?? 0}";

                    PlayniteApi.Dialogs.ShowMessage(
                        mensagemSucesso, 
                        "Resultado da Sincronização",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                else
                {
                    PlayniteApi.Dialogs.ShowErrorMessage(
                        $"Erro no servidor (Status {(int)response.StatusCode}):\n{responseText}", 
                        "Erro de Sincronização"
                    );
                }
            }
            catch (Exception ex)
            {
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

        private void ExibirLogTerminal(string json, int total, bool comInstalacao)
        {
            string tempBat = Path.Combine(Path.GetTempPath(), "playnite_sync_batch.bat");
            StringBuilder logCmd = new StringBuilder();

            logCmd.AppendLine("@echo off");
            logCmd.AppendLine("chcp 65001 > nul");
            logCmd.AppendLine($"title Playnite Sync - Batch ({total} jogos)");
            logCmd.AppendLine("echo =========================================================");
            logCmd.AppendLine($"echo  DESTINO: {ServerUrl}");
            logCmd.AppendLine($"echo  ENVIO EM BATCH - TOTAL: {total} JOGOS");
            logCmd.AppendLine($"echo  INCLUI STATUS DE INSTALACAO: {comInstalacao}");
            logCmd.AppendLine("echo =========================================================");
            logCmd.AppendLine("echo.");

            using (var reader = new StringReader(json))
            {
                string linha;
                while ((linha = reader.ReadLine()) != null)
                {
                    string linhaEscapada = linha.Replace("^", "^^")
                                                 .Replace("&", "^&")
                                                 .Replace("<", "^<")
                                                 .Replace(">", "^>")
                                                 .Replace("|", "^|");

                    logCmd.AppendLine($"echo {linhaEscapada}");
                }
            }

            logCmd.AppendLine("echo.");
            logCmd.AppendLine("echo =========================================================");
            logCmd.AppendLine("echo Pressione qualquer tecla para fechar...");
            logCmd.AppendLine("pause > nul");

            File.WriteAllText(tempBat, logCmd.ToString(), Encoding.UTF8);
            Process.Start("cmd.exe", $"/c start cmd.exe /k \"{tempBat}\"");
        }
    }
}
