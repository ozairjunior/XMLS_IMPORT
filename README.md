# XML Fiscal

Aplicação web em .NET 8 para importar, interpretar, consolidar, pesquisar e exportar XMLs fiscais de origens heterogêneas. A primeira entrega funcional prioriza NF-e modelo 55 e NFC-e modelo 65, mantendo a arquitetura extensível para CT-e, MDF-e e outros documentos.

## O que esta entrega implementa

- cadastro de uma ou mais empresas analisadas por CPF/CNPJ;
- importação recursiva de uma pasta do servidor e upload de múltiplos XMLs;
- progresso com contadores de processados, importados, ignorados, duplicados e erros;
- leitura segura contra DTD/XXE, limite de tamanho e isolamento de XML malformado;
- detecção e parsing de NF-e/NFC-e com namespaces tratados pelo nome local;
- deduplicação de notas por chave de acesso e de arquivos por SHA-256;
- identificação de entrada/saída pelo CNPJ configurado, não apenas por emitente/destinatário;
- clientes, fornecedores, documentos, itens, pagamentos, contas a pagar/receber e parcelas;
- nenhuma data de vencimento ou parcela é inventada;
- produtos consolidados por GTIN válido, descrição normalizada, NCM, unidade e medida;
- `cProd` armazenado somente em `ProductAlias` como código de origem;
- fila “Produtos para validar”, decisões persistidas e mesclagem rastreável;
- arquivo permanente dos XMLs importados, erros e logs de auditoria;
- dashboard, pesquisa e exportações CSV/Excel;
- migração inicial SQLite e testes unitários/integrados.

## Solução

```text
XmlFiscal.sln
├── src/XmlFiscal.Domain
├── src/XmlFiscal.Application
├── src/XmlFiscal.Infrastructure
├── src/XmlFiscal.Web
└── tests/XmlFiscal.Tests
```

A descrição detalhada da arquitetura, entidades, relacionamentos, fluxo de importação, estratégia de produtos e tecnologias está em [`docs/01-arquitetura.md`](docs/01-arquitetura.md).

## Pré-requisitos

- SDK .NET 8;
- navegador moderno;
- SQLite é usado no desenvolvimento. A abstração do EF Core permite adicionar SQL Server em implantação posterior.

## Executar

```bash
dotnet restore XmlFiscal.sln
dotnet build XmlFiscal.sln
dotnet test XmlFiscal.sln
dotnet run --project src/XmlFiscal.Web/XmlFiscal.Web.csproj
```

A aplicação cria o banco e aplica a migração na inicialização. O endereço padrão do perfil HTTP é `http://localhost:5186`.

## Configuração

As opções ficam em `src/XmlFiscal.Web/appsettings.json`:

- `ConnectionStrings:XmlFiscal`: banco SQLite;
- `XmlFiscal:ProductMatching`: pesos e limites de consolidação;
- `XmlFiscal:Import`: paralelismo, canal, tamanho máximo e arquivo permanente;
- `XmlFiscal:XmlSecurity`: limite de caracteres e política de namespace.

Os limites de matching são configuração, não regras rígidas compiladas no fluxo. Mudanças devem ser acompanhadas por testes com amostras reais anonimizadas.

## Importação pela web

“Pasta no servidor” lê um caminho acessível ao processo web. Para arquivos que estão no computador do usuário, use o seletor de múltiplos arquivos. Os uploads são gravados temporariamente, processados por streaming e removidos ao final.

## Dados financeiros

Contas a pagar ou receber só são criadas quando o XML contém evidência financeira (`dup` ou `detPag`). Parcelas refletem exatamente os elementos `dup`; `DueDate` permanece nulo quando o XML não informa vencimento.

## Consolidação de produtos

O fluxo nunca usa `cProd` como identificador mestre. GTINs válidos e diferentes bloqueiam a consolidação automática. GTIN igual com descrição ou medida conflitante gera revisão. Sem GTIN, a consolidação automática exige descrição muito próxima, NCM e unidade compatíveis. Consulte [`docs/01-arquitetura.md`](docs/01-arquitetura.md) e [`docs/03-status-implementacao.md`](docs/03-status-implementacao.md).

## Próximas evoluções recomendadas

CT-e/MDF-e, autenticação e perfis, SQL Server, importação em job persistente, bulk insert para lotes extremos, exportação realmente streaming, extração fiscal tributária detalhada e um corpus maior de testes com layouts de ERPs diferentes.
