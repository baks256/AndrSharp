using System.Diagnostics;
using System.Text.Json;

var lessonPath = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
lessonPath = Path.GetFullPath(lessonPath);

var testFile = Path.Combine(lessonPath, "tests.json");
if (!File.Exists(testFile))
{
    Console.WriteLine("В этой папке нет tests.json.");
    Console.WriteLine("Открой Program.cs нужного урока и снова нажми Ctrl+Shift+B.");
    return 2;
}

var definition = JsonSerializer.Deserialize<TestDefinition>(
    File.ReadAllText(testFile),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

if (definition is null || definition.Cases.Count == 0)
{
    Console.WriteLine("В уроке пока нет проверок.");
    return 2;
}

var project = Directory.GetFiles(lessonPath, "*.csproj").SingleOrDefault();
if (project is null)
{
    Console.WriteLine("Не найден проект урока (*.csproj).");
    return 2;
}

Console.WriteLine(definition.Name);
Console.WriteLine();

var failed = 0;

foreach (var test in definition.Cases)
{
    var startInfo = new ProcessStartInfo("dotnet", $"run --project \"{project}\" --no-restore")
    {
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    using var process = Process.Start(startInfo)!;
    await process.StandardInput.WriteAsync(test.Input);
    process.StandardInput.Close();

    var output = Normalize(await process.StandardOutput.ReadToEndAsync());
    var error = await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();

    var expected = Normalize(test.Output);

    if (process.ExitCode == 0 && output == expected)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ {test.Name}");
        Console.ResetColor();
        continue;
    }

    failed++;
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"✗ {test.Name}");
    Console.ResetColor();

    if (!string.IsNullOrWhiteSpace(error))
    {
        Console.WriteLine(error.Trim());
    }

    Console.WriteLine("Ожидалось:");
    Console.WriteLine(expected);
    Console.WriteLine("Получилось:");
    Console.WriteLine(output);
    Console.WriteLine();
}

Console.WriteLine();

if (failed == 0)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("✓ Все проверки зелёные. Урок пройден!");
    Console.ResetColor();
    return 0;
}

Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine($"Не пройдено проверок: {failed}.");
Console.ResetColor();
return 1;

static string Normalize(string value)
{
    return value.Replace("\r\n", "\n").Trim();
}

internal sealed class TestDefinition
{
    public string Name { get; set; } = "Проверка";
    public List<TestCase> Cases { get; set; } = [];
}

internal sealed class TestCase
{
    public string Name { get; set; } = "Тест";
    public string Input { get; set; } = "";
    public string Output { get; set; } = "";
}
