namespace ExchangeTest.Extensions;

public static class ConfigurationBuilderExtensions
{
    public static IConfigurationBuilder AddEnvFile(this IConfigurationBuilder builder, string path = ".env")
    {
        if (!File.Exists(path))
        {
            return builder;
        }

        var envVariables = File.ReadAllLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary<string[], string, string?>(parts => parts[0].Trim('"').Trim(), parts => parts[1].Trim('"').Trim());

        builder.AddInMemoryCollection(envVariables);

        return builder;
    }
}
