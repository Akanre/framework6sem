namespace Pr3.ConfigAndSecurity.Config;

public static class AppOptionsValidator
{
    public static IReadOnlyList<string> Validate(AppOptions options)
    {
        var errors = new List<string>();

        if (options.TrustedOrigins.Length == 0)
            errors.Add("Список доверенных источников пуст, служба не может быть открыта без ограничений");

        foreach (var origin in options.TrustedOrigins)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            {
                errors.Add($"Доверенный источник задан неверно, значение {origin}");
                continue;
            }

            if (uri.Scheme is not ("http" or "https"))
                errors.Add($"Доверенный источник должен иметь схему http или https, значение {origin}");
        }

        if (options.RateLimits.ReadPerMinute <= 0)
            errors.Add("Лимит чтения должен быть больше нуля");

        if (options.RateLimits.WritePerMinute <= 0)
            errors.Add("Лимит записи должен быть больше нуля");

        if (options.RateLimits.WritePerMinute > options.RateLimits.ReadPerMinute)
            errors.Add("Лимит записи не должен быть выше лимита чтения, иначе защита выглядит случайной");

        return errors;
    }
}
