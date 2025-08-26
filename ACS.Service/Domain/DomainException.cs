using System;

namespace ACS.Service.Domain
{
    /// <summary>
    /// Exception thrown when domain business rules are violated
    /// </summary>
    public class DomainException : Exception
    {
        public string DomainErrorCode { get; }

        public DomainException(string message) : base(message)
        {
            DomainErrorCode = "DOMAIN_RULE_VIOLATION";
        }

        public DomainException(string message, Exception innerException) : base(message, innerException)
        {
            DomainErrorCode = "DOMAIN_RULE_VIOLATION";
        }

        public DomainException(string domainErrorCode, string message) : base(message)
        {
            DomainErrorCode = domainErrorCode;
        }

        public DomainException(string domainErrorCode, string message, Exception innerException) : base(message, innerException)
        {
            DomainErrorCode = domainErrorCode;
        }
    }
}