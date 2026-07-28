# Suite de Extensões Customizadas para Playnite (.NET Plugins)

Conjunto de plugins nativos genéricos desenvolvidos em C# para o ecossistema **Playnite**, focados em automação de metadados, análise de dados e integração/sincronização bidirecional com backend e dashboard web.

---

## 📌 Contexto & Arquitetura Geral

### 1. Execução Nativa dentro da API do Playnite

O Playnite utiliza internamente o **LiteDB** com cabeçalhos proprietários. Tentativas de acessar ou modificar os arquivos `.db` por aplicações externas causam inconsistências no banco de dados (*"File is not a valid LiteDB database"*).

Esta suite de ferramentas roda **dentro do próprio processo do Playnite**, utilizando as instâncias oficiais da interface `IPlayniteAPI` (como `PlayniteApi.Database.Games`, `Genres`, `Categories`, etc.).

### 2. Alteração Segura e Atômica (`BufferedUpdate`)

Todas as rotinas que gravam ou atualizam o banco de dados utilizam o bloco `using (PlayniteApi.Database.BufferedUpdate())`. Isso garante que as modificações sejam aplicadas atômica e em lote diretamente na memória, eliminando riscos de corrupção do banco no disco.

### 3. Exibição Instantânea de Logs no Terminal

Para monitorar payloads e sincronizações em lote sem lentidão, as extensões geram arquivos temporários `.txt` com os dados codificados em UTF-8 e abrem o Prompt de Comando do Windows (`cmd.exe`) usando o comando `type`. Isso permite visualizar objetos e matrizes JSON imensas instantaneamente, sem travar o Playnite ou apresentar atrasos de escrita *linha a linha*.

---

## 🚀 Os Três Projetos da Suite

### 1. Importador de Gêneros via CSV

* **Ideia Central:** Permite vincular gêneros em massa aos jogos da biblioteca a partir de um arquivo `.csv` externo.
* **Modos de Operação:** Suporta mesclagem, sobrescrita total ou descarte de jogos que já possuem gêneros cadastrados.
* **Recursos:** Interface visual integrada às configurações do Playnite com seletor de arquivo e tratamento *case-insensitive* de delimitadores (`,`, `;`, `/`).

### 2. Analisador / Inspecionador de JSON

* **Ideia Central:** Ferramenta de inspeção leve para isolar e validar como o Playnite estrutura um jogo individualmente.
* **Como Funciona:** Adiciona a opção *"Inspecionar JSON (Terminal)"* ao menu de contexto (botão direito) dos jogos. Quando acionado, ele traduz as chaves proprietárias de GUIDs (gêneros, plataformas, categorias) para nomes legíveis em texto puro e exibe o objeto DTO em formato JSON formatado na tela.

### 3. Sincronizador com Servidor / Dashboard

* **Ideia Central:** Ponte de integração HTTP/REST entre a biblioteca local do Playnite e uma Dashboard Web/Servidor Backend.
* **Diferencial de Estado (HD Externo):** Permite realizar sincronizações de **metadados simples** (horas jogadas, notas, coleções) do dia a dia, ou sincronizações completas de **estado de instalação**, validando se os jogos em unidades de disco externas desconectadas devem ser marcados como indisponíveis no banco de dados.
* **Controle de Envio Interativo:** Dispara diálogos nativos ao enviar jogos (em lote, filtrados ou seleção individual), perguntando se o usuário deseja incluir os parâmetros de instalação do HD no payload.
* **Tradução Transparente (Servidor $\rightarrow$ Playnite):** Recebe dados em texto puro vindos do servidor e os converte dinamicamente em GUIDs nativos do Playnite, criando novas categorias e gêneros de forma automática quando necessário.

---

## 🛠️ Requisitos para Compilação

* **.NET SDK 8.0 (ou superior):** Para compilação dos projetos C#.
* **Target Framework (`net462`):** O Playnite Desktop roda sobre a runtime .NET Framework 4.6.2.
* **Suporte WPF (`<UseWPF>true</UseWPF>`):** Necessário caso vá manipular janelas/diálogos avançados do Windows.
* **Pacote NuGet `PlayniteSDK` (v6.11.0):** Dependência essencial para acesso aos modelos e interfaces do Playnite.

---

## ⚙️ Como Replicar e Compilar os Projetos

### 1. Estrutura Padrão do Arquivo de Projeto (`.csproj`)

Todos os plugins compartilham a mesma estrutura base de projeto C#:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net462</TargetFramework>
    <OutputType>Library</OutputType>
    <LangVersion>latest</LangVersion>
    <UseWPF>true</UseWPF>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="PresentationFramework" />
    <Reference Include="PresentationCore" />
    <Reference Include="WindowsBase" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="PlayniteSDK" Version="6.11.0" />
  </ItemGroup>

</Project>

```

### 2. Passos para Compilação no Terminal

No PowerShell, acesse o diretório do projeto e execute a limpeza e compilação em modo *Release*:

```powershell
# Limpa caches antigos de build (evita conflitos de WPF/MSBuild)
Remove-Item -Recurse -Force obj, bin -ErrorAction SilentlyContinue

# Compila a DLL do plugin
dotnet build -c Release

```

A DLL final compilada será gerada em:
`bin\Release\net462\<nome_do_projeto>.dll`

---

## 📁 Instalação no Playnite

### 1. Local do Diretório de Extensões

Pressione `Win + R`, cole o caminho correspondente e aperte Enter:

* **Instalação Padrão:** `%AppData%\Playnite\Extensions\`
* **Instalação Portátil:** `<Pasta_Do_Playnite>\Extensions\`

### 2. Criando o Manifesto (`extension.yaml`)

Para cada plugin, crie uma pasta dedicada dentro do diretório `Extensions` contendo a DLL compilada e um arquivo **`extension.yaml`**.

Exemplo de manifesto para o **Sincronizador com Servidor**:

```yaml
Id: SyncServerDashboard_99887766
Name: Sincronizador com Dashboard
Author: Custom
Version: 1.0.0
Module: server_sync.dll
Type: Generic

```

> ⚠️ **Atenção:**
> * O campo `Module:` deve conter exatamente o mesmo nome do arquivo `.dll` compilado.
> * Cada plugin instalado deve possuir um `Id` e um `Guid` (declarado internamente no código C#) 100% únicos para evitar que o Playnite bloqueie o carregamento por conflito de duplicação.
> 
> 

---

## 🎮 Como Usar

1. Abra ou reinicie o **Playnite** (garanta que o processo anterior foi finalizado na bandeja do Windows).
2. **Menu de Contexto (Clique com Botão Direito no Jogo):**
* Use a opção **Inspecionar JSON (Terminal)** para analisar DTOs isolados.
* Use **Sincronizar este jogo com o Servidor** para disparar o pop-up de envio individual.


3. **Menu Principal (Canto Superior Esquerdo / `Alt`):**
* Acesse a seção **Sincronizar com Dashboard** para realizar envios em lote da biblioteca inteira, de jogos filtrados/visíveis ou da seleção atual na tela.