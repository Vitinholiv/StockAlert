# Stock Alert

Projeto em C# para criar um alerter de ativos, notificando o usuário por email quando um determinado ativo ultrapassar algum limiar inferior no caso de compra ou superior no caso de venda.

O tutorial de uso é feito para a versão de Release (disponível na parte direita da página do GitHub), mas também pode ser feito com a versão de desenvolvimento ao compilar o código com o comando `dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o ./publish`.

Primeiro, baixe a versão de Release mais recente e descompacte os arquivos. Você verá uma pasta `publish` (que pode ser renomeada como quiser), abra ela.

## Configurações

Para usar o programa, é preciso configurar ele adequadamente, definindo o sender de emails por SMTP, os dados de envio e destino e também criando uma chave de API pessoal para usar, tudo isso no `config.json`.

- Para configurar o email, você precisa primeiro de uma conta de email utilizável via app, por exemplo, um conta com Gmail configurada. Você deve primeiro ativar a [verificação em duas etapas](https://support.google.com/accounts/answer/185839?hl=en) e ativar uma [senha de aplicativo](https://support.google.com/mail/answer/185833?hl=en), seguindo os tutoriais oficiais do Google indicados. Após fazer isso, esse email poderá ser usado como sender e usuário SMTP com os dados de email e senha colocados no arquivo de configuração `config.json`. Você também deve definir o email recipiente que receberá os emails.

- Para configurar a chave de API, você vai entrar no [site da Brapi](https://brapi.dev/login?callbackUrl=https%3A%2F%2Fbrapi.dev%2Fdashboard%3Fsource%3Dlanding_header), que é a API de ativos utilizada no projeto, criar uma conta e **copiar a chave de API** disponível [nessa página](https://brapi.dev/dashboard?source=landing_header) da Brapi. Substitua essa chave no `config.json` no parâmetro de ApiToken.

- Também é necessário que você instale o [.NET](https://dotnet.microsoft.com/pt-br/download) para rodar o código adequadamente. 

## Uso

Agora que os dados estão configurados, é muito simples:
- Abra um terminal do windows dentro da pasta (por exemplo, botão direito do mouse > abrir no terminal).
- Execute o comando `.\stock-quote-alert.exe ATIVO VENDA COMPRA`, substituindo `ATIVO` pela sigla do ativo que você deseja consultar, como PETR4 ou BBAS3, `VENDA` pelo preço que o programa considerará para enviar o email de sujestão de venda e `COMPRA` para o preço do email de sugestão de compra. Um exemplo de uso seria: `.\stock-quote-alert.exe BBAS3 19.80 19.40`.
- Apenas deixe o computador com o terminal aberto e conectado a internet, o terminal mostrará de minuto em minuto as variações no preço e quando um email é enviado. Você receberá os emails corretamente, assumindo que tenha configurado tudo de maneira adequada.
- Ao finalizar um dia de execução, como no horário de fechamento do mercado, os dados coletados durante a execução estarão salvos na pasta `data` que será criada conforme o programa executa. Para cada dia, também terá um arquivo `HTML` com um gráfico representativo, mostrando como os valores do ativo se comportaram ao decorrer do dia, comparando com suas margens de compra e venda, indicando os momentos onde um email foi enviado. Basta abrir o arquivo no navegador.