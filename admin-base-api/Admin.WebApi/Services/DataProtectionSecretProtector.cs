using Microsoft.AspNetCore.DataProtection;

namespace Admin.WebApi.Services
{
    public class DataProtectionSecretProtector : ISecretProtector
    {
        private readonly IDataProtector _dataProtector;

        public DataProtectionSecretProtector(IDataProtectionProvider dataProtectionProvider)
        {
            _dataProtector = dataProtectionProvider.CreateProtector("capturar.mercadopago.tokens.v1");
        }

        public string Protect(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return _dataProtector.Protect(value);
        }

        public string Unprotect(string protectedValue)
        {
            if (string.IsNullOrWhiteSpace(protectedValue))
                return string.Empty;

            return _dataProtector.Unprotect(protectedValue);
        }
    }
}
