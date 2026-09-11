# Manifesto de arquivos

Cada caminho abaixo contém o código/configuração completo da unidade indicada. O manifesto permite navegar a entrega sem perder a separação de responsabilidades.

| Caminho | Responsabilidade |
|---|---|
| `.github/workflows/ci.yml` | Pipeline de integração contínua: restore, build e testes em .NET 8. |
| `.gitignore` | Exclusões de build, banco local, arquivos arquivados e IDE. |
| `Directory.Build.props` | Configura .NET 8, C# 12, nullable e implicit usings para toda a solução. |
| `Directory.Packages.props` | Centraliza versões dos pacotes NuGet. |
| `README.md` | Instruções, escopo funcional, execução, configuração e próximos passos. |
| `XmlFiscal.sln` | Agrupa os quatro projetos de aplicação e o projeto de testes. |
| `docs/01-arquitetura.md` | Arquitetura, pastas, entidades, relacionamentos, fluxo, matching e tecnologias. |
| `docs/02-manifesto-arquivos.md` | Inventário completo dos arquivos e responsabilidades desta entrega. |
| `docs/03-status-implementacao.md` | Matriz de funcionalidades implementadas, limites e critérios de produção. |
| `docs/04-validacao.md` | Validações estáticas executadas e comandos para build/teste. |
| `global.json` | Seleciona SDK .NET 8 com roll-forward para feature band disponível. |
| `src/XmlFiscal.Application/Contracts.cs` | DTOs de parsing, importação, dashboard, busca, revisão, exportação e matching. |
| `src/XmlFiscal.Application/Interfaces.cs` | Contratos dos serviços usados pela infraestrutura e interface. |
| `src/XmlFiscal.Application/Matching.cs` | Score e travas de consolidação por GTIN, descrição, NCM, unidade e medida. |
| `src/XmlFiscal.Application/Normalization.cs` | Normalização de CPF/CNPJ, GTIN, descrição e chave de deduplicação de partes. |
| `src/XmlFiscal.Application/Options.cs` | Opções configuráveis de matching, importação e segurança XML. |
| `src/XmlFiscal.Application/PartyResolution.cs` | Identifica empresa e direção pelo CPF/CNPJ configurado. |
| `src/XmlFiscal.Application/XmlFiscal.Application.csproj` | Projeto de aplicação referenciando o domínio. |
| `src/XmlFiscal.Domain/Common.cs` | Entidade base com auditoria temporal e objeto de valor de endereço. |
| `src/XmlFiscal.Domain/Enums.cs` | Enums de documento, direção, importação, financeiro, matching e auditoria. |
| `src/XmlFiscal.Domain/Financial.cs` | Contas a pagar/receber, parcelas e pagamentos. |
| `src/XmlFiscal.Domain/FiscalDocuments.cs` | Documento fiscal e itens com dados originais e produto consolidado. |
| `src/XmlFiscal.Domain/ImportAudit.cs` | Lotes, arquivos, erros e logs de auditoria. |
| `src/XmlFiscal.Domain/Parties.cs` | Entidades Company, Customer e Supplier. |
| `src/XmlFiscal.Domain/Products.cs` | Produto consolidado, aliases, sugestões e decisões de matching. |
| `src/XmlFiscal.Domain/XmlFiscal.Domain.csproj` | Projeto de domínio sem dependências externas. |
| `src/XmlFiscal.Infrastructure/DependencyInjection.cs` | Composição de serviços, opções, SQLite e inicialização do banco. |
| `src/XmlFiscal.Infrastructure/ExportService.cs` | Exportações CSV e Excel dos oito conjuntos de dados. |
| `src/XmlFiscal.Infrastructure/Importing.cs` | Pipeline produtor-consumidor, transação por XML, persistência, financeiro e arquivo permanente. |
| `src/XmlFiscal.Infrastructure/Parsing.cs` | Detector, leitor XML seguro e parser NF-e/NFC-e. |
| `src/XmlFiscal.Infrastructure/Persistence/Migrations/202608190001_InitialCreate.cs` | Migração inicial SQLite com tabelas, FKs, índices e checks. |
| `src/XmlFiscal.Infrastructure/Persistence/XmlFiscalDbContext.cs` | DbContext, mappings, índices, constraints e factory de design. |
| `src/XmlFiscal.Infrastructure/ProductConsolidation.cs` | Busca candidatos, aplica matching, cria produtos, aliases e sugestões. |
| `src/XmlFiscal.Infrastructure/QueryServices.cs` | Dashboard, empresas, revisão/merge e pesquisa unificada. |
| `src/XmlFiscal.Infrastructure/XmlFiscal.Infrastructure.csproj` | Infraestrutura com EF Core SQLite, ClosedXML e ASP.NET Core shared framework. |
| `src/XmlFiscal.Web/Components/App.razor` | Documento HTML raiz, estilos e scripts. |
| `src/XmlFiscal.Web/Components/Layout/MainLayout.razor` | Estrutura visual principal. |
| `src/XmlFiscal.Web/Components/Layout/NavMenu.razor` | Navegação lateral. |
| `src/XmlFiscal.Web/Components/MetricCard.razor` | Card reutilizável de indicador. |
| `src/XmlFiscal.Web/Components/Pages/Companies.razor` | Cadastro e edição de empresas analisadas. |
| `src/XmlFiscal.Web/Components/Pages/Error.razor` | Página de erro controlado. |
| `src/XmlFiscal.Web/Components/Pages/Exports.razor` | Seleção e download de exportações. |
| `src/XmlFiscal.Web/Components/Pages/Home.razor` | Dashboard. |
| `src/XmlFiscal.Web/Components/Pages/Import.razor` | Importação por pasta ou upload, progresso e cancelamento. |
| `src/XmlFiscal.Web/Components/Pages/ProductReview.razor` | Fila e ações de validação de produtos. |
| `src/XmlFiscal.Web/Components/Pages/Search.razor` | Pesquisa por termo e período. |
| `src/XmlFiscal.Web/Components/Routes.razor` | Roteamento e layout padrão. |
| `src/XmlFiscal.Web/Components/_Imports.razor` | Namespaces compartilhados pelos componentes Razor. |
| `src/XmlFiscal.Web/Program.cs` | Inicialização da aplicação, DI, migração e endpoints Blazor. |
| `src/XmlFiscal.Web/Properties/launchSettings.json` | Perfis HTTP/HTTPS para execução local. |
| `src/XmlFiscal.Web/XmlFiscal.Web.csproj` | Projeto ASP.NET Core Blazor Server. |
| `src/XmlFiscal.Web/appsettings.Development.json` | Logging do ambiente de desenvolvimento. |
| `src/XmlFiscal.Web/appsettings.json` | Conexão e opções padrão de matching, importação e XML. |
| `src/XmlFiscal.Web/wwwroot/app.css` | Design responsivo da interface. |
| `src/XmlFiscal.Web/wwwroot/app.js` | Download de arquivos gerados por stream do Blazor. |
| `src/XmlFiscal.Web/wwwroot/favicon.svg` | Ícone da aplicação. |
| `tests/XmlFiscal.Tests/DeduplicationTests.cs` | Testes das chaves de deduplicação de clientes/fornecedores. |
| `tests/XmlFiscal.Tests/GlobalUsings.cs` | Importação global do xUnit. |
| `tests/XmlFiscal.Tests/ImportIntegrationTests.cs` | Teste integrado de importação, persistência financeira e duplicidade. |
| `tests/XmlFiscal.Tests/MatchingTests.cs` | Testes da matriz de matching e conflito de medidas. |
| `tests/XmlFiscal.Tests/NormalizationTests.cs` | Testes de GTIN e descrição. |
| `tests/XmlFiscal.Tests/ParserTests.cs` | Testes de parsing fiscal, parcelas, pagamentos e bloqueio de DTD/XXE. |
| `tests/XmlFiscal.Tests/PartyResolverTests.cs` | Testes de entrada/saída pelo CNPJ configurado. |
| `tests/XmlFiscal.Tests/TestData/nfe-inbound.xml` | NF-e anonimizada para testes de entrada e parcelas. |
| `tests/XmlFiscal.Tests/XmlFiscal.Tests.csproj` | Projeto xUnit com cobertura e referências da solução. |
