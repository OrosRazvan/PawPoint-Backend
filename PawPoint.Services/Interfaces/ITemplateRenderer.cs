namespace PawPoint.Services.Interfaces
{
    public interface ITemplateRenderer
    {
        Task<string> RenderAsync(string relativePath, IReadOnlyDictionary<string, string> model);
    }
}
