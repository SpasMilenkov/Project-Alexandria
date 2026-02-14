// Services/TemplateService.cs
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Builder.Services;

public interface ITemplateService
{
    string LoadTemplate(string templateName);
    string ReplaceTokens(string template, Dictionary<string, string> tokens);
}

public class TemplateService : ITemplateService
{
    private readonly string _templateBasePath;
    
    public TemplateService()
    {
        // Get the path to the Templates folder
        var assembly = Assembly.GetExecutingAssembly();
        var assemblyPath = Path.GetDirectoryName(assembly.Location);
        if (assemblyPath != null) _templateBasePath = Path.Combine(assemblyPath, "Templates/Development");
    }

    public string LoadTemplate(string templateName)
    {
        var templatePath = Path.Combine(_templateBasePath, templateName);
        
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"Template not found: {templateName}", templatePath);
        }
        
        return File.ReadAllText(templatePath);
    }

    public string ReplaceTokens(string template, Dictionary<string, string> tokens)
    {
        var result = template;
        
        foreach (var token in tokens)
        {
            result = result.Replace($"{{{{{token.Key}}}}}", token.Value);
        }
        
        return result;
    }
}