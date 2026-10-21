using Microsoft.Extensions.Options;

namespace Snail.Toolkit.AI.Ollama.Configuration;

/// <summary>
/// Refuses settings no call can succeed with, naming the setting, before the first call is made.
/// </summary>
/// <remarks>
/// Without it a BaseUrl missing its scheme — "localhost:11434" parses as a URI whose scheme is the host — failed
/// only at the first call, and an empty one with a bare UriFormatException; a zero Timeout failed every unary call
/// at once, a negative one threw from inside the call.
/// </remarks>
internal sealed class OllamaOptionsValidator : IValidateOptions<OllamaOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, OllamaOptions options)
    {
        var fault = Fault(options);

        return fault is null ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(fault);
    }

    /// <summary>
    /// The first thing wrong with the settings, or null when they can be used.
    /// </summary>
    internal static string? Fault(OllamaOptions options)
    {
        bool isHttp = Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var address)
                      && (address.Scheme == Uri.UriSchemeHttp || address.Scheme == Uri.UriSchemeHttps);

        if (!isHttp)
        {
            return $"OllamaOptions.{nameof(OllamaOptions.BaseUrl)} has to be an absolute http or https address such as "
                   + $"'http://localhost:11434', not '{options.BaseUrl}'.";
        }

        if (options.Timeout <= TimeSpan.Zero && options.Timeout != Timeout.InfiniteTimeSpan)
        {
            return $"OllamaOptions.{nameof(OllamaOptions.Timeout)} has to be positive or Timeout.InfiniteTimeSpan, "
                   + $"not '{options.Timeout}': it would fail every unary call.";
        }

        return null;
    }
}
