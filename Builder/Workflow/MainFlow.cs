using Builder.Models;
using Builder.Services;
using Environment = Builder.Models.Environment;
namespace Builder.Workflow;

public class MainFlow
{
    private readonly ISystemChecker _systemChecker;
    private readonly IConfigurationService _configService;
    private readonly ITemplateService _templateService;

    public MainFlow()
    {
        _systemChecker = new SystemChecker();
        _templateService = new TemplateService();
        _configService = new ConfigurationService(_templateService);
    }

    public void ExecuteFlow()
    {
        ShowBanner();
        
        // Step 1: Select Environment
        var environment = SelectEnvironment();
        
        // Step 2: Select Installation Type
        var installationType = SelectInstallationType();
        
        // Step 3: Execute based on selection
        switch (installationType)
        {
            case "Fresh install":
                ExecuteFreshInstall(environment);
                break;
            case "Custom install":
                ExecuteCustomInstall(environment);
                break;
            case "Upgrade existing install":
                ExecuteUpgrade(environment);
                break;
        }
    }

    private void ShowBanner()
    {
        AnsiConsole.Write(
            new FigletText("Alexandria")
                .Centered()
                .Color(Color.Blue));
        
        AnsiConsole.MarkupLine("[dim]Intelligent Installation System[/]\n");
    }

    private Environment SelectEnvironment()
    {
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]Select your environment:[/]")
                .AddChoices(
                    "Development - For local development with debug tools",
                    "Local Preview - Production build running locally",
                    "Deployment - Production environment setup")
        );

        return choice.Split('-')[0].Trim() switch
        {
            "Development" => Models.Environment.Development,
            "Local Preview" => Models.Environment.LocalPreview,
            "Deployment" => Models.Environment.Deployment,
            _ => Models.Environment.Development
        };
    }

    private string SelectInstallationType()
    {
        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]Select operation type:[/]")
                .AddChoices("Fresh install", "Custom install", "Upgrade existing install")
        );
    }

    private void ExecuteFreshInstall(Models.Environment environment)
    {
        AnsiConsole.MarkupLine($"\n[bold blue]Starting fresh installation for {environment} environment[/]\n");
        
        // System checks
        if (!RunSystemChecks())
            return;
        
        // Port scanning
        var portCheckResults = ScanRequiredPorts();
        var portConflicts = portCheckResults.Where(p => !p.IsAvailable).ToList();
        
        if (portConflicts.Any())
        {
            if (!HandlePortConflicts(portConflicts))
                return;
        }
        
        // S3 Provider selection
        var s3Provider = SelectS3Provider();
        
        // Create configuration
        var config = _configService.CreateConfiguration(environment, s3Provider);
        
        // Show configuration summary
        ShowConfigurationSummary(config);
        
        if (!AnsiConsole.Confirm("Proceed with installation?"))
        {
            AnsiConsole.MarkupLine("[yellow]Installation cancelled.[/]");
            return;
        }
        
        // Generate and save docker-compose
        var composeContent = _configService.GenerateDockerCompose(config);
        var installPath = GetInstallPath();
        
        File.WriteAllText(Path.Combine(installPath, "docker-compose.yml"), composeContent);
        _configService.SaveConfiguration(config, installPath);
        
        // Execute docker-compose
        ExecuteDockerCompose(installPath, environment);
        
        // Show success and credentials
        ShowSuccessMessage(config, installPath);
    }

    private bool RunSystemChecks()
    {
        return AnsiConsole.Status()
            .Start("Checking system requirements...", ctx =>
            {
                ctx.Status("Detecting operating system...");
                var os = _systemChecker.GetOperatingSystem();
                AnsiConsole.MarkupLine($"✓ OS detected: [green]{os}[/]");
                Thread.Sleep(500);
                
                ctx.Status("Checking for Docker...");
                if (!_systemChecker.IsDockerInstalled())
                {
                    AnsiConsole.MarkupLine("[red]✗ Docker not found[/]");
                    AnsiConsole.MarkupLine("Please install Docker first: https://docs.docker.com/get-docker/");
                    return false;
                }
                AnsiConsole.MarkupLine($"✓ Docker: [green]{_systemChecker.GetDockerVersion()}[/]");
                Thread.Sleep(500);
                
                ctx.Status("Checking for Docker Compose...");
                if (!_systemChecker.IsDockerComposeInstalled())
                {
                    AnsiConsole.MarkupLine("[red]✗ Docker Compose not found[/]");
                    return false;
                }
                AnsiConsole.MarkupLine($"✓ Docker Compose: [green]{_systemChecker.GetDockerComposeVersion()}[/]");
                Thread.Sleep(500);
                
                return true;
            });
    }

    private List<PortCheckResult> ScanRequiredPorts()
    {
        var requiredPorts = new List<int> { 5432, 9000, 9001, 5672, 15672, 3901, 3902 };
        
        return AnsiConsole.Status()
            .Start("Scanning ports...", ctx =>
            {
                var results = _systemChecker.CheckPorts(requiredPorts);
                
                foreach (var result in results)
                {
                    var status = result.IsAvailable ? "[green]✓ Available[/]" : "[red]✗ In use[/]";
                    AnsiConsole.MarkupLine($"  Port {result.Port}: {status}");
                }
                
                return results;
            });
    }

    private bool HandlePortConflicts(List<PortCheckResult> conflicts)
    {
        AnsiConsole.MarkupLine("\n[yellow]⚠ Port conflicts detected![/]\n");
        
        var table = new Table();
        table.AddColumn("Port");
        table.AddColumn("Process");
        
        foreach (var conflict in conflicts)
        {
            table.AddRow(conflict.Port.ToString(), conflict.ProcessBlocking ?? "Unknown");
        }
        
        AnsiConsole.Write(table);
        
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("\n[bold]How would you like to proceed?[/]")
                .AddChoices(
                    "Use alternative ports (recommended)",
                    "Cancel installation",
                    "Continue anyway (may fail)")
        );
        
        return choice != "Cancel installation";
    }

    private S3Provider SelectS3Provider()
    {
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("\n[bold]Select S3-compatible storage provider:[/]")
                .AddChoices(
                    "Garage - Lightweight, geo-distributed (recommended)",
                    "MinIO - Feature-rich, enterprise-grade",
                    "RustFS - Minimal, high-performance")
        );

        return choice.Split('-')[0].Trim() switch
        {
            "Garage" => S3Provider.Garage,
            "MinIO" => S3Provider.MinIO,
            "RustFS" => S3Provider.RustFS,
            _ => S3Provider.Garage
        };
    }

    private void ShowConfigurationSummary(InstallationConfig config)
    {
        AnsiConsole.MarkupLine("\n[bold]Configuration Summary:[/]");
        
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("Setting");
        table.AddColumn("Value");
        
        table.AddRow("Environment", config.Environment.ToString());
        table.AddRow("S3 Provider", config.S3Provider.ToString());
        table.AddRow("PostgreSQL Port", config.Ports["postgres"].ToString());
        table.AddRow("S3 API Port", config.Ports["s3_api"].ToString());
        table.AddRow("RabbitMQ Port", config.Ports["rabbitmq"].ToString());
        
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private string GetInstallPath()
    {
        var defaultPath = Path.Combine(System.Environment.GetFolderPath(
            System.Environment.SpecialFolder.UserProfile), "alexandria");
            
        var path = AnsiConsole.Ask("Installation path:", defaultPath);
        
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
            
        return path;
    }

    private void ExecuteDockerCompose(string path, Models.Environment environment)
    {
        AnsiConsole.Status()
            .Start("Starting containers...", ctx =>
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "compose up -d",
                    WorkingDirectory = path,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };

                using var process = System.Diagnostics.Process.Start(startInfo);
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                
                if (process.ExitCode == 0)
                {
                    AnsiConsole.MarkupLine("[green]✓ Containers started successfully[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]✗ Failed to start containers[/]");
                    AnsiConsole.WriteException(new Exception(process.StandardError.ReadToEnd()));
                }
            });
    }

    private void ShowSuccessMessage(InstallationConfig config, string installPath)
    {
        var panel = new Panel(
            new Markup($@"[green]✓ Installation completed successfully![/]

[bold]Service URLs:[/]
- PostgreSQL: localhost:{config.Ports["postgres"]}
- S3 API: http://localhost:{config.Ports["s3_api"]}
- S3 Console: http://localhost:{config.Ports["s3_console"]}
- RabbitMQ: localhost:{config.Ports["rabbitmq"]}
- RabbitMQ Management: http://localhost:{config.Ports["rabbitmq_management"]}

[bold]Credentials saved to:[/]
{Path.Combine(installPath, "alexandria-config.json")}

[dim]Use 'docker compose logs -f' to view logs[/]"))
        {
            Header = new PanelHeader("🎉 Success"),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Green)
        };
        
        AnsiConsole.Write(panel);
    }

    private void ExecuteUpgrade(Models.Environment environment)
    {
        AnsiConsole.MarkupLine("[yellow]Upgrade functionality coming soon![/]");
    }

    private void ExecuteCustomInstall(Models.Environment environment)
    {
        AnsiConsole.MarkupLine("[yellow]Custom install functionality coming soon![/]");
    }
}