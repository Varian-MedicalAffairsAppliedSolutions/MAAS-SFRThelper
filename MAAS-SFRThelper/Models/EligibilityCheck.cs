namespace MAAS_SFRThelper.Models
{
    /// <summary>
    /// Result of a single extraction-precondition check.
    /// IsBlocking distinguishes hard failures (extraction impossible)
    /// from warnings (extraction proceeds with documented behavior).
    /// Status is a display key: Pass / Warn / Fail.
    /// </summary>
    public class EligibilityCheck
    {
        public string Name { get; }
        public bool Passed { get; }
        public bool IsBlocking { get; }
        public string Detail { get; }

        public string Status => Passed ? "Pass" : (IsBlocking ? "Fail" : "Warn");

        public EligibilityCheck(string name, bool passed, string detail, bool isBlocking = true)
        {
            Name = name;
            Passed = passed;
            Detail = detail;
            IsBlocking = isBlocking;
        }
    }
}