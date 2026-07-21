namespace MAAS_SFRThelper.Services
{
    /// <summary>
    /// Progress reporting and cooperative cancellation for long-running
    /// operations. Deliberately UI-agnostic: implementations may pump a
    /// WPF dispatcher, write to a console, or record calls in a test.
    /// Long-running code must poll CancellationRequested at safe
    /// boundaries (e.g. between beamlets) and unwind through its own
    /// cleanup when it returns true - callers never abort a run forcibly.
    /// </summary>
    public interface IRunProgress
    {
        /// <summary>Append a line to the run log.</summary>
        void Message(string text);

        /// <summary>Report overall completion, 0 to 100.</summary>
        void Progress(double percent);

        /// <summary>
        /// True once cancellation has been requested. Polled, not pushed:
        /// the running code decides when it is safe to stop.
        /// </summary>
        bool CancellationRequested { get; }
    }
}