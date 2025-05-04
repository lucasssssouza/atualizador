using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Diagnostics;
using System.IO.Compression;
using System.Threading.Tasks;

class Program
{
    // 1) Raw URL do versao.json no GitHub
    const string jsonUrl =
      "https://raw.githubusercontent.com/lucasssssouza/atualizador/main/versao.json";
    // 2) Template da URL do ZIP no Release (note o "v" + tag)
    const string zipUrlTemplate =
      "https://github.com/lucasssssouza/atualizador/releases/download/v{0}/{1}";

    const string destinoBase = @"C:\Ganso\Copiar Acesso\";
    const string nomeExePrincipal = "CopiarAcessos";

    static async Task Main()
    {
        Console.WriteLine("Verificando nova versão...");

        var info = await ObterVersaoRemota();
        if (info == null) return;

        string local = ObterVersaoLocal();
        Console.WriteLine($"Versão local:  {local}");
        Console.WriteLine($"Versão server: {info.versao}");

        if (new Version(info.versao) <= new Version(local))
        {
            Console.WriteLine("Nenhuma atualização necessária.");
        }
        else
        {
            Console.WriteLine("Atualizando para v" + info.versao + "...");

            // pasta temporária
            string tempDir = Path.Combine(destinoBase, "TEMP_UPDATE");
            Directory.CreateDirectory(tempDir);

            // monta URL e caminho do ZIP
            string zipName = info.arquivoZip;
            string zipUrl = string.Format(zipUrlTemplate, info.versao, zipName);
            string zipTemp = Path.Combine(tempDir, zipName);

            Console.WriteLine($"Baixando {zipName} de:\n  {zipUrl}");
            await BaixarZipHttp(zipUrl, zipTemp);

            Console.WriteLine("Removendo arquivos antigos...");
            LimparArquivosDestino();

            Console.WriteLine("Extraindo atualização...");
            ZipFile.ExtractToDirectory(zipTemp, destinoBase, true);

            Console.WriteLine("Limpando temporários...");
            File.Delete(zipTemp);
            Directory.Delete(tempDir, true);

            SalvarVersaoLocal(info.versao);
            Console.WriteLine("Atualização concluída.");

            // reinicia app principal
            var exePath = Path.Combine(destinoBase, nomeExePrincipal + ".exe");
            if (File.Exists(exePath))
                Process.Start(exePath);
            else
                Console.WriteLine("Não achou o executável principal.");
        }

        Console.WriteLine("\nPressione ENTER para sair...");
        Console.ReadLine();
    }

    static async Task<InfoVersao?> ObterVersaoRemota()
    {
        using var cli = new HttpClient();
        try
        {
            var json = await cli.GetStringAsync(jsonUrl);
            return JsonSerializer.Deserialize<InfoVersao>(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao baixar versao.json: " + ex.Message);
            return null;
        }
    }

    static async Task BaixarZipHttp(string url, string destino)
    {
        using var cli = new HttpClient();
        using var resp = await cli.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();

        using var src = await resp.Content.ReadAsStreamAsync();
        using var dst = new FileStream(destino, FileMode.Create, FileAccess.Write);

        var buffer = new byte[8192];
        int read;
        Console.Write("Progresso: ");
        while ((read = await src.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await dst.WriteAsync(buffer, 0, read);
            Console.Write(".");
        }
        Console.WriteLine("\nDownload concluído.");
    }

    static void LimparArquivosDestino()
    {
        foreach (var f in Directory.GetFiles(destinoBase))
        {
            var n = Path.GetFileName(f);
            if (n.Equals("Atualizador.exe", StringComparison.OrdinalIgnoreCase)) continue;
            File.Delete(f);
            Console.WriteLine($"Removido: {n}");
        }
    }

    static string ObterVersaoLocal()
    {
        var p = Path.Combine(destinoBase, "versao.txt");
        return File.Exists(p) ? File.ReadAllText(p).Trim() : "0.0.0";
    }

    static void SalvarVersaoLocal(string v)
        => File.WriteAllText(Path.Combine(destinoBase, "versao.txt"), v);

    static bool VersaoNova(string local, string server)
        => new Version(server) > new Version(local);
}

class InfoVersao
{
    public string versao { get; set; }
    public string arquivoZip { get; set; }
}
