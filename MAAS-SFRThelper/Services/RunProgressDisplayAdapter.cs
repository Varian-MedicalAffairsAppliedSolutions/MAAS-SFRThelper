using CalculateInfluenceMatrix;
using System.Text.RegularExpressions;

namespace MAAS_SFRThelper.Services
{
    /// <summary>
    /// Bridges the DoseInfluenceMatrix library's DisplayProgress (a single
    /// abstract Message) onto SFRThelper's IRunProgress. Every message is
    /// forwarded (which also pumps the dispatcher, keeping the UI alive
    /// while Calculate runs on the UI thread), and the library's own
    /// "Beamlet i/N" progress lines are parsed to drive the percent bar,
    /// since the library has no separate progress channel.
    /// </summary>
    public class RunProgressDisplayAdapter : DisplayProgress
    {
        private static readonly Regex s_rxBeamlet =
            new Regex(@"Beamlet\s*(\d+)\s*/\s*(\d+)", RegexOptions.Compiled);

        private readonly IRunProgress _progress;

        public RunProgressDisplayAdapter(IRunProgress progress)
        {
            _progress = progress;
        }

        public override void Message(string szMsg)
        {
            _progress.Message(szMsg);

            Match m = s_rxBeamlet.Match(szMsg ?? string.Empty);
            if (m.Success)
            {
                double cur, max;
                if (double.TryParse(m.Groups[1].Value, out cur) &&
                    double.TryParse(m.Groups[2].Value, out max) && max > 0)
                {
                    _progress.Progress(100.0 * cur / max);
                }
            }
        }
    }
}