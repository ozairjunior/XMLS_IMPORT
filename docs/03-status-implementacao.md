# Status da implementação

## Entrega atual — núcleo funcional

| Área | Status | Observações |
|---|---|---|
| Arquitetura em camadas | Implementado | Domain, Application, Infrastructure, Web e Tests. |
| NF-e 55 / NFC-e 65 | Implementado | Detecção, parser e protocolo. |
| CT-e / MDF-e | Preparado | Detector reconhece; parsers ficam para a etapa seguinte. |
| Segurança XML | Implementado | DTD/XXE, tamanho, caracteres, malformação e avisos de namespace. |
| Importação de pasta/arquivos | Implementado | Recursão, upload, canal limitado, paralelismo e progresso. |
| Duplicidade | Implementado | SHA-256 e chave de acesso. |
| Empresas e direção | Implementado | CNPJ configurado define entrada/saída. |
| Clientes/fornecedores | Implementado | Deduplicação por documento ou chave nome/cidade/UF. |
| Produtos/aliases | Implementado | GTIN, descrição, NCM, unidade, medida e rastreabilidade. |
| Revisão manual | Implementado | Mesmo, diferente, merge e ignorar, com auditoria. |
| Financeiro | Implementado | `dup` e `detPag`; não cria vencimentos fictícios. |
| Dashboard/pesquisa | Implementado | Consultas principais. |
| CSV/Excel | Implementado | Oito conjuntos de dados. |
| Migração SQLite | Implementado | Migração inicial aplicada ao iniciar. |
| Testes | Código implementado | Unitários e integração SQLite em memória; execução compilada pendente neste contêiner e automatizada pelo CI incluído. |
| SQL Server | Próxima etapa | Adicionar provider, configuração e migração específica. |
| Bulk insert | Próxima etapa | Recomendado após benchmark com lote real extremo. |
| Autenticação/perfis | Próxima etapa | Necessário antes de exposição pública/multiusuário. |
| Job persistente/retomada | Próxima etapa | Para importações distribuídas ou retomáveis após reinício. |
| Tributos detalhados por item | Próxima etapa | O núcleo atual guarda totais e campos comerciais solicitados. |

## Limites deliberados

- O upload web grava arquivos em pasta temporária antes da importação; a leitura do XML em si continua por streaming.
- A exportação retorna um arquivo em memória. Para dezenas de milhões de linhas, deve ser substituída por geração streaming ou job.
- O matching usa similaridade determinística, não modelo de aprendizado de máquina. As decisões humanas são reaproveitadas por aliases e merges.
- A primeira entrega não autentica usuários; o campo de usuário está preparado nos lotes, decisões e auditoria.

## Critérios antes de produção

1. executar `dotnet restore`, `dotnet build` e `dotnet test` no ambiente-alvo;
2. testar com amostras anonimizadas de todos os ERPs reais;
3. revisar limites de arquivo, paralelismo e matching;
4. ativar autenticação/autorização e política de retenção dos XMLs;
5. definir backup, criptografia de disco e controle de acesso ao arquivo permanente;
6. executar benchmark de 1 mil, 10 mil, 50 mil e 100 mil XMLs;
7. configurar observabilidade e alertas de falha de lote.
