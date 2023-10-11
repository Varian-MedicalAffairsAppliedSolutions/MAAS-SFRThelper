namespace System.Reflection
{
    /// <summary>
    /// Custom assembly attribute class
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    public class AssemblyExpirationDate : Attribute
    {
        /// <summary>
        /// ExparationDate. 
        /// The format is always 'mm/dd/yyyy'
        /// </summary>
        public string ExpirationDate { get; set; }

        /// <summary>
        /// Initializes a new instance of the AssemblyExpirationDate class.
        /// </summary>
        public AssemblyExpirationDate() : this(string.Empty) { }

        /// <summary>
        /// Initializes a new instance of the AssemblyExpirationDate class.
        /// </summary>
        /// <param name="expDate">Exparation date, expected format is 'mm/dd/yyyy'</param>
        public AssemblyExpirationDate(string expDate) { ExpirationDate = expDate; }
    }
}
