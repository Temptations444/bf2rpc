using System.Diagnostics;

namespace BF2Presence.Game;

public static class GameProcess
{
    public static Process? Find(IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            var processes = Process.GetProcessesByName(name);
            var match = processes.FirstOrDefault(p => !p.HasExited);
            foreach (var p in processes)
                if (p != match) p.Dispose();
            if (match is not null) return match;
        }
        return null;
    }
}
