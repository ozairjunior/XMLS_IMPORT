# Validação da entrega

## Verificações executadas neste ambiente

A validação estática automatizada terminou sem erros e cobriu:

- 65 arquivos legíveis, não vazios e codificados em UTF-8;
- 9 arquivos XML, projetos e SVGs com estrutura válida;
- 4 arquivos JSON válidos;
- 5 projetos presentes, com referências internas resolvidas;
- manifesto com cobertura exata dos 65 arquivos da solução;
- ausência de marcadores usuais de implementação incompleta no código e na documentação;
- balanceamento lexical de delimitadores nos 29 arquivos C#, desconsiderando comentários e literais;
- XML de teste com chave de acesso de 44 dígitos, dois itens, três parcelas, um pagamento e GTINs com dígito verificador válido;
- opções de importação, segurança XML e matching com limites coerentes;
- guardrails arquiteturais para impedir o uso de `cProd` como identificador mestre e a criação de vencimentos fictícios.

## Validação real da migração SQLite

O SQL da migração inicial foi executado diretamente contra um banco SQLite temporário:

- `Up` criou 17 tabelas e 53 índices;
- chaves estrangeiras, índices únicos e restrições `CHECK` foram inspecionados;
- datas com fuso foram mapeadas para valores inteiros ordenáveis;
- scores de matching foram mapeados para `REAL`, permitindo ordenação no SQLite;
- `Down` removeu integralmente o esquema criado.

Essa verificação valida o SQL produzido na migração incluída, mas não substitui a execução de `Database.MigrateAsync()` pelo Entity Framework Core em um processo .NET real.

## Restrição do ambiente de geração

O contêiner de geração não possuía o SDK .NET. A tentativa de instalar o SDK oficial foi bloqueada pelas restrições do ambiente para binários externos. Por isso, o compilador C# e o runner xUnit não foram executados aqui.

A solução inclui um workflow de CI e deve passar pelos comandos abaixo em uma máquina com .NET 8 antes de qualquer implantação:

```bash
dotnet restore XmlFiscal.sln
dotnet build XmlFiscal.sln --configuration Release --no-restore
dotnet test XmlFiscal.sln --configuration Release --no-build
```

A validação compilada continua sendo obrigatória. Qualquer erro ou aviso relevante encontrado pelo compilador deve ser corrigido antes da implantação.
