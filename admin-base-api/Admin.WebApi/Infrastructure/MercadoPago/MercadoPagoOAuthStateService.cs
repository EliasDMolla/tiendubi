using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace Admin.WebApi.Infrastructure.MercadoPago
{
    public interface IMercadoPagoOAuthStateService
    {
        string CreateState(int photographerId);
        bool TryReadState(string state, out int photographerId);
    }

    public class MercadoPagoOAuthStateService : IMercadoPagoOAuthStateService
    {
        private readonly IDataProtector _protector;

        public MercadoPagoOAuthStateService(IDataProtectionProvider dataProtectionProvider)
        {
            _protector = dataProtectionProvider.CreateProtector("capturar.mercadopago.oauth.state.v1");
        }

        public string CreateState(int photographerId)
        {
            var payload = $"{photographerId}|{Guid.NewGuid():N}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            var protectedPayload = _protector.Protect(payload);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(protectedPayload));
        }

        public bool TryReadState(string state, out int photographerId)
        {
            photographerId = 0;

            if (string.IsNullOrWhiteSpace(state))
                return false;

            try
            {
                var protectedPayload = Encoding.UTF8.GetString(Convert.FromBase64String(state));
                var payload = _protector.Unprotect(protectedPayload);
                var pieces = payload.Split('|', StringSplitOptions.TrimEntries);

                if (pieces.Length < 3)
                    return false;

                if (!int.TryParse(pieces[0], out photographerId) || photographerId <= 0)
                    return false;

                if (!long.TryParse(pieces[2], out var issuedAtUnix))
                    return false;

                var issuedAt = DateTimeOffset.FromUnixTimeSeconds(issuedAtUnix);
                if (issuedAt < DateTimeOffset.UtcNow.AddMinutes(-15))
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
