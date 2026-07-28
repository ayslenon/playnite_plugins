using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace SyncServerPlaynite
{
    // DTO customizado idêntico à sua estrutura desejada (sem ReleaseDate)
    public class GameDTO
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string InstallDirectory { get; set; }
        public bool IsInstalled { get; set; }
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
        public override Guid Id { get; } = Guid.Parse("f9e8d7c6-b5a4-3210-fedc-ba9876543210");

        public SyncServerPlugin(IPlayniteAPI api) : base(api)
        {
            Properties = new GenericPluginProperties
            {
                HasSettings = false
            };
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            return new List<GameMenuItem>
            {
                new GameMenuItem
                {
                    Description = "Inspecionar JSON (Terminal)",
                    MenuSection = "Sincronizar Dados",
                    Action = (gameMenuItemArgs) =>
                    {
                        var jogo = gameMenuItemArgs.Games.FirstOrDefault();
                        if (jogo != null)
                        {
                            PrintarDtoCustomizado(jogo);
                        }
                    }
                }
            };
        }

        private GameDTO CriarDtoCustomizado(Game jogo)
        {
            // Validação local do HD (marca IsInstalled = false se a pasta do HD não existir)
            bool estaInstalado = jogo.IsInstalled;
            if (estaInstalado && !string.IsNullOrWhiteSpace(jogo.InstallDirectory))
            {
                if (!Directory.Exists(jogo.InstallDirectory))
                {
                    estaInstalado = false;
                }
            }

            var dto = new GameDTO
            {
                Id = jogo.Id.ToString(),
                Name = jogo.Name,
                InstallDirectory = jogo.InstallDirectory,
                IsInstalled = estaInstalado,
                Playtime = Convert.ToInt32(jogo.Playtime),
                Favorite = jogo.Favorite,
                CoverImage = jogo.CoverImage,
                BackgroundImage = jogo.BackgroundImage,
                Hidden = jogo.Hidden,
                PlayCount = Convert.ToInt32(jogo.PlayCount),
                Added = jogo.Added?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Modified = jogo.Modified?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Notes = jogo.Notes
            };

            // Mapeamento das listas de GUIDs para NOMES legíveis
            if (jogo.GenreIds != null)
            {
                dto.Genres = PlayniteApi.Database.Genres
                    .Where(g => jogo.GenreIds.Contains(g.Id))
                    .Select(g => g.Name).ToList();
            }

            if (jogo.PlatformIds != null)
            {
                dto.Platforms = PlayniteApi.Database.Platforms
                    .Where(p => jogo.PlatformIds.Contains(p.Id))
                    .Select(p => p.Name).ToList();
            }

            if (jogo.CategoryIds != null)
            {
                dto.Categories = PlayniteApi.Database.Categories
                    .Where(c => jogo.CategoryIds.Contains(c.Id))
                    .Select(c => c.Name).ToList();
            }

            if (jogo.TagIds != null)
            {
                dto.Tags = PlayniteApi.Database.Tags
                    .Where(t => jogo.TagIds.Contains(t.Id))
                    .Select(t => t.Name).ToList();
            }

            if (jogo.FeatureIds != null)
            {
                dto.Features = PlayniteApi.Database.Features
                    .Where(f => jogo.FeatureIds.Contains(f.Id))
                    .Select(f => f.Name).ToList();
            }

            // Tradução do SourceId (Steam, GOG, Epic, etc.)
            if (jogo.SourceId != Guid.Empty)
            {
                var sourceObj = PlayniteApi.Database.Sources.FirstOrDefault(s => s.Id == jogo.SourceId);
                dto.Source = sourceObj != null ? sourceObj.Name : null;
            }

            // Tradução do CompletionStatusId (Playing, Completed, Plan to Play, etc.)
            if (jogo.CompletionStatusId != Guid.Empty)
            {
                var statusObj = PlayniteApi.Database.CompletionStatuses.FirstOrDefault(s => s.Id == jogo.CompletionStatusId);
                dto.CompletionStatus = statusObj != null ? statusObj.Name : null;
            }

            return dto;
        }

        private void PrintarDtoCustomizado(Game jogo)
        {
            // 1. Caminhos para os arquivos temporários
            string tempTxt = Path.Combine(Path.GetTempPath(), "playnite_inspect_payload.txt");
            string tempBat = Path.Combine(Path.GetTempPath(), "playnite_inspect_custom.bat");

            // 2. Serializa o DTO e grava direto no arquivo TXT em UTF-8
            var dto = CriarDtoCustomizado(jogo);
            string jsonFormatado = Serialization.ToJson(dto, true);
            File.WriteAllText(tempTxt, jsonFormatado, Encoding.UTF8);

            // 3. Monta o script .bat usando 'type' para exibir o conteúdo instantaneamente
            StringBuilder logCmd = new StringBuilder();
            logCmd.AppendLine("@echo off");
            logCmd.AppendLine("chcp 65001 > nul");
            logCmd.AppendLine($"title Playnite - JSON Customizado: {jogo.Name}");
            logCmd.AppendLine("echo =========================================================");
            logCmd.AppendLine($"echo  SAIDA JSON NO FORMATO ESPECIFICO:");
            logCmd.AppendLine($"echo  {jogo.Name}");
            logCmd.AppendLine("echo =========================================================");
            logCmd.AppendLine("echo.");

            // Exibe o JSON de uma só vez
            logCmd.AppendLine($"type \"{tempTxt}\"");

            logCmd.AppendLine("echo.");
            logCmd.AppendLine("echo =========================================================");
            logCmd.AppendLine("echo Pressione qualquer tecla para fechar esta janela...");
            logCmd.AppendLine("pause > nul");

            File.WriteAllText(tempBat, logCmd.ToString(), Encoding.UTF8);

            Process.Start("cmd.exe", $"/c start cmd.exe /k \"{tempBat}\"");
        }
    }
}