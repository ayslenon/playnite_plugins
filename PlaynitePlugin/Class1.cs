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
        Mesclar,
        Sobrescrever,
        Ignorar
    }

    public class ImportadorGenerosSettings : ObservableObject
    {
        private string csvPath = @"C:\Users\Console\Downloads\Planilha_de_jogos_traduzida.csv";
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
            { ModoPreenchimento.Mesclar, "Mesclar (adiciona dados do CSV sem apagar os existentes)" },
            { ModoPreenchimento.Sobrescrever, "Sobrescrever (limpa gêneros/tags atuais e insere os do CSV)" },
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

            var labelCsv = new TextBlock
            {
                Text = "Caminho do Arquivo CSV de Jogos:",
                Margin = new Thickness(0, 0, 0, 8),
                FontWeight = FontWeights.Bold
            };
            Grid.SetRow(labelCsv, 0);

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

            var labelModo = new TextBlock
            {
                Text = "Modo de Preenchimento de Gêneros e Tags:",
                Margin = new Thickness(0, 0, 0, 8),
                FontWeight = FontWeights.Bold
            };
            Grid.SetRow(labelModo, 2);

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

            var chkVerbose = new CheckBox
            {
                Content = "Log detalhado (exibe todos os jogos e mapeamentos no terminal)",
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
                    Description = "Importar Dados do CSV de Jogos (Terminal)",
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
                PlayniteApi.Dialogs.ShowErrorMessage($"Arquivo CSV não encontrado em:\n'{csvPath}'\n\nConfigure em: Configurações > Extensões > Importador de Gêneros CSV.", "Erro Importador");
                return;
            }

            string tempBat = Path.Combine(Path.GetTempPath(), "playnite_import_log.bat");
            StringBuilder logCmd = new StringBuilder();

            logCmd.AppendLine("@echo off");
            logCmd.AppendLine("chcp 65001 > nul");
            logCmd.AppendLine("title Playnite - Importador Avançado de Planilhas (v2.0.0)");
            logCmd.AppendLine("echo =========================================================");
            logCmd.AppendLine("echo  INICIANDO IMPORTACAO DE GENEROS, TAGS E METADADOS VIA C#");
            logCmd.AppendLine($"echo  Modo de Preenchimento: {modo.ToString().ToUpper()}");
            logCmd.AppendLine("echo =========================================================");
            logCmd.AppendLine("echo.");

            int atualizados = 0;
            int falhas = 0;
            int ignorados = 0;
            int novasTags = 0;
            int novosGeneros = 0;

            using (PlayniteApi.Database.BufferedUpdate())
            {
                var linhas = File.ReadAllLines(csvPath, Encoding.UTF8)
                                 .Where(l => !string.IsNullOrWhiteSpace(l))
                                 .ToList();

                if (linhas.Count > 0 && linhas[0].ToLower().Contains("nome"))
                {
                    linhas.RemoveAt(0);
                }

                foreach (var linha in linhas)
                {
                    // Parser manual de CSV seguro contra vírgulas dentro de aspas (como a lista de gêneros)
                    var colunas = ParseCsvLine(linha);
                    if (colunas.Count < 5) continue;

                    string nomeJogoCsv = colunas[0].Trim();
                    string modoJogoCsv = colunas[1].Trim();
                    string numJogadoresCsv = colunas[2].Trim();
                    string vontadeJogarCsv = colunas[3].Trim();
                    string generosRaw = colunas[4].Trim();

                    if (string.IsNullOrWhiteSpace(nomeJogoCsv)) continue;

                    var jogo = PlayniteApi.Database.Games.FirstOrDefault(g => 
                        g.Name != null && g.Name.Trim().Equals(nomeJogoCsv, StringComparison.OrdinalIgnoreCase));

                    if (jogo == null)
                    {
                        logCmd.AppendLine($"echo [FALHA] Jogo nao encontrado: '{nomeJogoCsv}'");
                        falhas++;
                        continue;
                    }

                    if (modo == ModoPreenchimento.Ignorar && jogo.GenreIds != null && jogo.GenreIds.Count > 0)
                    {
                        if (verbose) logCmd.AppendLine($"echo [IGNORADO] {jogo.Name} ja possui generos.");
                        ignorados++;
                        continue;
                    }

                    // Inicialização/Limpeza dependendo do modo selecionado
                    if (modo == ModoPreenchimento.Sobrescrever || jogo.GenreIds == null) jogo.GenreIds = new List<Guid>();
                    if (modo == ModoPreenchimento.Sobrescrever || jogo.TagIds == null) jogo.TagIds = new List<Guid>();

                    List<string> tagsParaAdicionar = new List<string>();

                    // === REGRA 1: Mapeamento de Número de Jogadores ===
                    string tagJogadores = "1 Jogador"; // Default se for em branco
                    if (!string.IsNullOrWhiteSpace(numJogadoresCsv))
                    {
                        if (numJogadoresCsv.Equals("1", StringComparison.OrdinalIgnoreCase)) tagJogadores = "1 Jogador";
                        else if (numJogadoresCsv.Equals("2", StringComparison.OrdinalIgnoreCase)) tagJogadores = "2 Jogadores";
			else if (numJogadoresCsv.Contains("3")) tagJogadores = "Até 4 Jogadores";
                        else if (numJogadoresCsv.Contains("4")) tagJogadores = "Até 4 Jogadores";
                        else tagJogadores = "Mais de 4 Jogadores";
                    }
                    tagsParaAdicionar.Add(tagJogadores);

                    // === REGRA 2: Modo Coop (Independe do tipo se houver "Coop") ===
                    if (modoJogoCsv.ToLower().Contains("coop") || modoJogoCsv.ToLower().Contains("co-op"))
                    {
                        tagsParaAdicionar.Add("Coop");
                    }

                    // === REGRA 3: Mapeamento de Vontade de Jogar ===
                    if (int.TryParse(vontadeJogarCsv, out int vontadeValor))
                    {
                        int valorPlaynite = 1;
                        if (vontadeValor == -1) valorPlaynite = 1;
                        else if (vontadeValor == 1) valorPlaynite = 2;
                        else if (vontadeValor == 0) valorPlaynite = 3; // Jogos coop de sofá/party/online
                        else if (vontadeValor == 6) valorPlaynite = 3;
                        else if (vontadeValor == -2) valorPlaynite = 2;
                        else if (vontadeValor >= 1 && vontadeValor <= 5) valorPlaynite = vontadeValor;

                        tagsParaAdicionar.Add($"[Vontade de Jogar] {valorPlaynite}");
                    }

                    // Processamento e vínculo das Tags no Playnite
                    foreach (var tagNome in tagsParaAdicionar)
                    {
                        var tagObj = PlayniteApi.Database.Tags.FirstOrDefault(t => 
                            t.Name != null && t.Name.Trim().Equals(tagNome, StringComparison.OrdinalIgnoreCase));

                        if (tagObj == null)
                        {
                            tagObj = new Tag(tagNome);
                            PlayniteApi.Database.Tags.Add(tagObj);
                            novasTags++;
                        }

                        if (!jogo.TagIds.Contains(tagObj.Id))
                        {
                            jogo.TagIds.Add(tagObj.Id);
                        }
                    }

                    // === REGRA 4: Gêneros Traduzidos ===
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
                        logCmd.AppendLine($"echo [OK] Mapeado: {jogo.Name} ^| Gêneros: {jogo.GenreIds.Count} ^| Tags: {jogo.TagIds.Count}");
                    }
                }
            }

            logCmd.AppendLine("echo.");
            logCmd.AppendLine("echo =========================================================");
            logCmd.AppendLine("echo  RELATORIO FINAL DA IMPORTACAO");
            logCmd.AppendLine("echo =========================================================");
            logCmd.AppendLine($"echo  - Jogos Modificados com Sucesso: {atualizados}");
            logCmd.AppendLine($"echo  - Jogos Ignorados:               {ignorados}");
            logCmd.AppendLine($"echo  - Novas Tags Criadas no Banco:   {novasTags}");
            logCmd.AppendLine($"echo  - Novos Generos Adicionados:     {novosGeneros}");
            logCmd.AppendLine($"echo  - Jogos Não Localizados:         {falhas}");
            logCmd.AppendLine("echo =========================================================");
            logCmd.AppendLine("echo.");
            logCmd.AppendLine("echo Pressione qualquer tecla para encerrar...");
            logCmd.AppendLine("pause > nul");

            File.WriteAllText(tempBat, logCmd.ToString(), Encoding.UTF8);
            Process.Start("cmd.exe", $"/c start cmd.exe /k \"{tempBat}\"");
        }

        // Função auxiliar para destrinchar a linha de CSV respeitando blocos entre aspas
        private List<string> ParseCsvLine(string line)
        {
            List<string> result = new List<string>();
            StringBuilder currentColumn = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(currentColumn.ToString());
                    currentColumn.Clear();
                }
                else
                {
                    currentColumn.Append(c);
                }
            }
            result.Add(currentColumn.ToString());
            return result;
        }
    }
}