namespace Builder.Models;

public sealed record ContainerStatus(string Service, string Name, string State, string Health, int? ExitCode)
{
    public string Display => $"{Service}: {State}" +
                             HealthDescription +
                             (State == "exited" ? $" [exit {ExitCode?.ToString() ?? "unknown"}]" : string.Empty);

    private string HealthDescription
    {
        get
        {
            if (!string.IsNullOrEmpty(Health)) return $" ({Health})";
            return State == "running" ? " (no healthcheck; process state only)" : string.Empty;
        }
    }
}

public sealed record ServiceReadiness(bool Ready, bool Failed, IReadOnlyList<string> Problems);

public static class ContainerReadiness
{
    public static ServiceReadiness Evaluate(
        IReadOnlyCollection<string> expectedServices,
        IReadOnlyList<ContainerStatus> statuses,
        IReadOnlyCollection<string> oneShotServices)
    {
        var problems = new List<string>();
        var failed = false;

        if (expectedServices.Count == 0)
        {
            return new ServiceReadiness(false, true, ["The expected Compose service inventory is empty."]);
        }

        foreach (var service in expectedServices)
        {
            var containers = statuses.Where(status => status.Service == service).ToList();

            if (containers.Count == 0)
            {
                problems.Add($"{service}: container missing");
                failed = true;
                continue;
            }

            if (containers.Count != 1)
            {
                problems.Add($"{service}: expected 1 container, found {containers.Count}");
                failed = true;
            }

            foreach (var container in containers)
            {
                var oneShot = oneShotServices.Contains(service, StringComparer.Ordinal);

                if (oneShot && container.State == "exited" && container.ExitCode == 0)
                {
                    continue;
                }

                if (!oneShot && container.State == "running" &&
                    (container.Health.Length == 0 || container.Health == "healthy"))
                {
                    continue;
                }

                problems.Add(container.Display);

                if (container.State is "dead" or "exited" || container.Health == "unhealthy")
                {
                    failed = true;
                }
            }
        }

        return new ServiceReadiness(problems.Count == 0, failed, problems);
    }
}
