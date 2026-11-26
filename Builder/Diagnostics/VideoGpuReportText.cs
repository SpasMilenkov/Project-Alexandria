using System.Text;

namespace Builder.Diagnostics;

// The report as plain text for the command line, so it can be pasted into an issue as it is.
public static class VideoGpuReportText
{
    public static string Render(VideoGpuReport report)
    {
        var text = new StringBuilder();
        text.AppendLine($"Video GPU check for {report.Folder}");

        if (report.Setting is not null)
        {
            text.AppendLine();
            text.AppendLine("Installation");
            text.AppendLine("  " + report.Setting);

            if (report.WorkerDetail is not null || report.WorkerEncodes is not null)
            {
                text.AppendLine("  Worker as installed: " + Outcome(report.WorkerEncodes, report.WorkerDetail));
            }
        }

        if (report.Nodes.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("Render nodes");

            foreach (var node in report.Nodes)
            {
                var use = node.InUse ? " (in use)" : string.Empty;
                text.AppendLine($"  {node.Node.Description}{use}");
                text.AppendLine("    " + node.Node.Path);
                text.AppendLine("    " + Outcome(node.Encodes, node.Detail));
            }
        }

        if (report.Notes.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("Notes");

            foreach (var note in report.Notes)
            {
                text.AppendLine("  - " + note);
            }
        }

        return text.ToString();
    }

    public static string Outcome(bool? encodes, string? detail)
    {
        if (encodes is null) return detail ?? "Not tested";

        var verdict = encodes.Value ? "Encodes" : "Cannot encode";

        if (detail is null) return verdict;

        var joint = encodes.Value ? " with " : ": ";

        return verdict + joint + detail;
    }
}
