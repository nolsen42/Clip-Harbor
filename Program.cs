namespace ClipHarbor;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        string? shot = null;
        var links = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--screenshot" && i + 1 < args.Length) shot = args[++i];
            else if (Engine.IsUrl(args[i])) links.Add(args[i]);
        }

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            if (shot != null) { Console.Error.WriteLine(e.Exception); return; }
            MessageBox.Show("Something went wrong: " + e.Exception.Message, "ClipHarbor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        };

        // In screenshot mode use a fresh, empty state so the picture is predictable and nothing is changed.
        var state = shot != null ? new AppState() : AppState.Load();
        if (shot != null)
            foreach (var o in new[] { state.Settings.SmartOptions, state.Settings.LastOptions }) o.Folder = AppState.DefaultFolder();

        var form = new MainForm(state, shot);
        if (links.Count > 0) form.Shown += async (_, _) => await form.AddLinks(string.Join("\n", links));
        Application.Run(form);
    }
}
