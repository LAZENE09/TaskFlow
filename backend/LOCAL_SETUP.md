# Configuração local da API

O repositório público não contém credenciais nem chaves de API.

Para rodar localmente:

1. Copie `TaskFlowApi/appsettings.Development.example.json` para `TaskFlowApi/appsettings.Development.json`.
2. Preencha a connection string do MySQL e a chave da TomTom com os seus valores locais.
3. Abra `TaskFlowApi.slnx` no Visual Studio e execute o perfil `https` ou `http`.

`appsettings.Development.json` está no `.gitignore` e não deve ser commitado.
