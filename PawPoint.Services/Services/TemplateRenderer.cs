using PawPoint.Common.Helpers;
using PawPoint.Services.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Razor.Templating.Core;
using System.Text;

namespace PawPoint.Services.Services
{
    public class TemplateRenderer : ITemplateRenderer
    {
        private readonly TemplateSettings _settings;
        private readonly IHostEnvironment _env;

        public TemplateRenderer(IOptions<TemplateSettings> options, IHostEnvironment env)
        {
            _settings = options.Value;
            _env = env;
        }

        public async Task<string> RenderAsync(string relativePath, IReadOnlyDictionary<string, string> model)
        {
            var path = NormalizePath(relativePath);

            if (path.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
            {
                return await RazorTemplateEngine.RenderAsync(path, model);
            }

            var physical = ToPhysicalPath(path);
            if (!File.Exists(physical))
                throw new FileNotFoundException($"Text template not found: {physical}");

            var text = await File.ReadAllTextAsync(physical, Encoding.UTF8);
            foreach (var kv in model)
            {
                text = text.Replace("{{" + kv.Key + "}}", kv.Value ?? string.Empty, StringComparison.Ordinal);
            }
            return text;
        }

        private string NormalizePath(string input)
        {
            var p = input.Replace('\\', '/').Trim();

            if (p.StartsWith("/", StringComparison.Ordinal) || p.StartsWith("~/", StringComparison.Ordinal))
                return p.StartsWith("~/") ? "/" + p[2..] : p;

            if (!Path.HasExtension(p))
                p += ".cshtml";

            return "/Views/" + p.TrimStart('/');
        }

        private string ToPhysicalPath(string absolutePath)
        {
            var rel = absolutePath.TrimStart('/', '\\')
                                   .Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(_env.ContentRootPath, rel);
        }
    }
}
