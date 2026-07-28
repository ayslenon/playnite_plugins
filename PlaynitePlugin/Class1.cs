using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace ImportadorGeneros
{
    public enum ModoPreenchimento
    {
        Sobrescrever,
        Mesclar,
        Ignorar
    }

    public class ImportadorGenerosSettings : ObservableObject
    {
        private string csvPath = @"C:\Users\Console\Downloads\seus_generos.csv";
        private ModoPreenchimento modo = ModoPreenchimento.Mesclar;
        private bool verboseLog = true;

        public string CsvPath
        {
            get => csvPath;
            set => SetValue(ref csvPath, value);
        }

        public ModoPreenchimento Modo
        {
            get => modo;
            set => SetValue(ref modo, value);
        }

        public bool VerboseLog
        {
            get => verboseLog;
            set => SetValue(ref verboseLog, value);
        }
    }

    public class ImportadorGenerosSettingsViewModel : ObservableObject, ISettings
    {
        private readonly ImportadorGenerosPlugin plugin;
        private ImportadorGenerosSettings editingClone;

        public ImportadorGenerosSettings Settings { get; private set; }

        public RelayCommand SelectCsvCommand { get; }

        public Dictionary<ModoPreenchimento, string> ModosDisponiveis { get; } = new Dictionary<ModoPreenchimento, string>
        {
            { ModoPreenchimento.Mesclar, "Mesclar (adiciona gêneros do CSV sem apagar os existentes)" },
            { ModoPreenchimento.Sobrescrever, "Sobrescrever (limpa os gêneros atuais e insere os do CSV)" },
            { ModoPreenchimento.Ignorar, "Ignorar (pula jogos que já possuem qualquer gênero preenchido)" }
        };

        public ImportadorGenerosSettingsViewModel(ImportadorGenerosPlugin plugin)
        {
            this.plugin = plugin;
            var savedSettings = plugin.LoadPluginSettings<ImportadorGenerosSettings>();
            Settings = savedSettings ?? new ImportadorGenerosSettings();

            SelectCsvCommand = new RelayCommand(() =>
            {
                var filePath = plugin.PlayniteApi.Dialogs.SelectFile("Arquivos CSV (*.csv)|*.csv");
                if (!string.IsNullOrEmpty(filePath))
                {
                    Settings.CsvPath = filePath;
                }
            });
        }

        public void BeginEdit()
        {
            editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            Settings = editingClone;
        }

        public void EndEdit()
        {
            plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            return true;
        }
    }

    public class ImportadorGenerosPlugin : GenericPlugin
    {
        public override Guid Id { get; } = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

        public ImportadorGenerosSettingsViewModel SettingsVM { get; private set; }

        public ImportadorGenerosPlugin(IPlayniteAPI api) : base(api)
        {
            SettingsVM = new ImportadorGenerosSettingsViewModel(this);
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };
        }

        public override ISettings GetSettings(bool initialize)
        {
            return SettingsVM;
        }

        public override UserControl GetSettingsView(bool initialize)
        {
            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Linha 0: Label CSV
            var labelCsv = new TextBlock
            {
                Text = "Caminho do Arquivo CSV de Gêneros:",
                Margin = new Thickness(0, 0, 0, 8),
                FontWeight = FontWeights.Bold
            };
            Grid.SetRow(labelCsv, 0);

            // Linha 1: Input CSV + Botão
            var stackCsv = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
            var textBox = new TextBox
            {
                Width = 400,
                Height = 28,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            textBox.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding("Settings.CsvPath") { Mode = System.Windows.Data.BindingMode.TwoWay });

            var button = new Button
            {
                Content = "Buscar CSV...",
                Width = 100,
                Height = 28
            };
            button.SetBinding(Button.CommandProperty, new System.Windows.Data.Binding("SelectCsvCommand"));

            stackCsv.Children.Add(textBox);
            stackCsv.Children.Add(button);
            Grid.SetRow(stackCsv, 1);

            // Linha 2: Label Modo
            var labelModo = new TextBlock
            {
                Text = "Modo de Preenchimento de Gêneros:",
                Margin = new Thickness(0, 0, 0, 8),
                FontWeight = FontWeights.Bold
            };
            Grid.SetRow(labelModo, 2);

            // Linha 3: ComboBox para seleção do Modo
            var comboModo = new ComboBox
            {
                Width = 510,
                Height = 28,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 15),
                DisplayMemberPath = "Value",
                SelectedValuePath = "Key"
            };
            comboModo.SetBinding(ComboBox.ItemsSourceProperty, new System.Windows.Data.Binding("ModosDisponiveis"));
            comboModo.SetBinding(ComboBox.SelectedValueProperty, new System.Windows.Data.Binding("Settings.Modo") { Mode = System.Windows.Data.BindingMode.TwoWay });
            Grid.SetRow(comboModo, 3);

            // Linha 4: Checkbox Verbose Log
            var chkVerbose = new CheckBox
            {
                Content = "Log detalhado (exibe todos os jogos no terminal; se desmarcado, exibe apenas falhas)",
                Margin = new Thickness(0, 0, 0, 10)
            };
            chkVerbose.SetBinding(CheckBox.IsCheckedProperty, new System.Windows.Data.Binding("Settings.VerboseLog") { Mode = System.Windows.Data.BindingMode.TwoWay });
            Grid.SetRow(chkVerbose, 4);

            grid.Children.Add(labelCsv);
            grid.Children.Add(stackCsv);
            grid.Children.Add(labelModo);
            grid.Children.Add(comboModo);
            grid.Children.Add(chkVerbose);

            return new UserControl { Content = grid };
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            return new List<MainMenuItem>
            {
                new MainMenuItem
                {
                    Description = "Importar gêneros do CSV (Terminal)",
                    MenuSection = "@Sincronizar Dados",
                    Action = (mainMenuItemArgs) => ProcessarCSV()
                }
            };
        }

        private void ProcessarCSV()
        {
            string csvPath = SettingsVM.Settings.CsvPath;
            ModoPreenchimento modo = SettingsVM.Settings.Modo;
            bool verbose = SettingsVM.Settings.VerboseLog;

            if (string.IsNullOrWhiteSpace(csvPath) || !File.Exists(csvPath))
            {
                PlayniteApi.Dialogs.ShowErrorMessage($"Arquivo CSV não encontrado em:\n'{csvPath}'\n\nPor favor, configure o caminho em: Configurações > Extensões > Importador de Gêneros CSV.", "Erro Importador");
                return;
            }

            string tempBat = Path.Combine(Path.GetTempPath(), "playnite_import_log.bat");
            StringBuilder logCmd = new StringBuilder();

            logCmd.AppendLine("@echo off");
            logCmd.AppendLine("chcp 65001 > nul");
            logCmd.AppendLine("title Playnite - Importador de Generos (Plugin C# v1.2.0)");
            logCmd.AppendLine("echo =========================================================");
            logCmd.AppendLine("echo  INICIANDO IMPORTACAO DE GENEROS VIA PLUGIN C# NATIVO");
            logCmd.AppendLine($"echo  Modo de Preenchimento: {modo.ToString().ToUpper()}");
            logCmd.AppendLine($"echo  Nivel de Log:          {(verbose ? "DETALHADO (Tudo)" : "RESUMIDO (Apenas Falhas)")}");
            logCmd.AppendLine("echo =========================================================");
            logCmd.AppendLine("echo.");

            int atualizados = 0;
            int falhas = 0;
            int ignorados = 0;
            int novosGeneros = 0;

            using (PlayniteApi.Database.BufferedUpdate())
            {
                var linhas = File.ReadAllLines(csvPath, Encoding.UTF8)
                                 .Where(l => !string.IsNullOrWhiteSpace(l))
                                 .ToList();

                if (linhas.Count > 0 && (linhas[0].ToLower().Contains("nome") || linhas[0].ToLower().Contains("game")))
                {
                    linhas.RemoveAt(0);
                }

                foreach (var linha in linhas)
                {
                    int posVirgula = linha.IndexOf(',');
                    if (posVirgula == -1) continue;

                    string nomeJogoCsv = linha.Substring(0, posVirgula).Trim('"', ' ');
                    string generosRaw = linha.Substring(posVirgula + 1).Trim('"', ' ');

                    if (string.IsNullOrWhiteSpace(nomeJogoCsv)) continue;

                    var jogo = PlayniteApi.Database.Games.FirstOrDefault(g => 
                        g.Name != null && g.Name.Trim().Equals(nomeJogoCsv, StringComparison.OrdinalIgnoreCase));

                    if (jogo == null)
                    {
                        logCmd.AppendLine($"echo [FALHA] Jogo nao encontrado no Playnite: '{nomeJogoCsv}'");
                        falhas++;
                        continue;
                    }

                    // AQUI ESTÁ A LÓGICA DO 'CONTINUE' PARA O MODO IGNORAR:
                    if (modo == ModoPreenchimento.Ignorar && jogo.GenreIds != null && jogo.GenreIds.Count > 0)
                    {
                        if (verbose)
                        {
                            logCmd.AppendLine($"echo [IGNORADO] {jogo.Name} já possui generos cadastrados.");
                        }
                        ignorados++;
                        continue; // Sai do loop e pula para o próximo jogo
                    }

                    // Se for modo Sobrescrever, limpa a lista existente antes
                    if (modo == ModoPreenchimento.Sobrescrever || jogo.GenreIds == null)
                    {
                        jogo.GenreIds = new List<Guid>();
                    }

                    var listaGeneros = generosRaw.Split(new[] { ',', ';', '/' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var gNomeRaw in listaGeneros)
                    {
                        string gNome = gNomeRaw.Trim();
                        if (string.IsNullOrWhiteSpace(gNome)) continue;

                        var generoObj = PlayniteApi.Database.Genres.FirstOrDefault(g => 
                            g.Name != null && g.Name.Trim().Equals(gNome, StringComparison.OrdinalIgnoreCase));

                        if (generoObj == null)
                        {
                            generoObj = new Genre(gNome);
                            PlayniteApi.Database.Genres.Add(generoObj);
                            novosGeneros++;
                            if (verbose)
                            {
                                logCmd.AppendLine($"echo   [NOVO GENERO] Criado no banco: '{gNome}'");
                            }
                        }

                        if (!jogo.GenreIds.Contains(generoObj.Id))
                        {
                            jogo.GenreIds.Add(generoObj.Id);
                        }
                    }

                    PlayniteApi.Database.Games.Update(jogo);
                    atualizados++;

                    if (verbose)
                    {
                        logCmd.AppendLine($"echo [OK] {jogo.Name} ^| Total de generos atrelados: {jogo.GenreIds.Count}");
                    }
                }
            }

            logCmd.AppendLine("echo.");
            logCmd.AppendLine("echo =========================================================");
            logCmd.AppendLine("echo  RELATORIO DA IMPORTACAO");
            logCmd.AppendLine("echo =========================================================");
            logCmd.AppendLine($"echo  - Jogos Atualizados com Sucesso: {atualizados}");
            logCmd.AppendLine($"echo  - Jogos Ignorados (Já com gênero): {ignorados}");
            logCmd.AppendLine($"echo  - Novos Generos Criados:         {novosGeneros}");
            logCmd.AppendLine($"echo  - Jogos Nao Encontrados:         {falhas}");
            logCmd.AppendLine("echo =========================================================");
            logCmd.AppendLine("echo.");
            logCmd.AppendLine("echo Pressione qualquer tecla para fechar esta janela...");
            logCmd.AppendLine("pause > nul");

            File.WriteAllText(tempBat, logCmd.ToString(), Encoding.UTF8);

            Process.Start("cmd.exe", $"/c start cmd.exe /k \"{tempBat}\"");
        }
    }
}