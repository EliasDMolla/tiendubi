using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace Admin.WebApi.Infrastructure.MercadoPago
{
    public interface IMercadoPagoOAuthStateService
    {
        string CreateState(int photographerId, string codeVerifier);
        bool TryReadState(string state, out int photographerId, out string codeVerifier);
    }

    public class MercadoPagoOAuthStateService : IMercadoPagoOAuthStateService
    {
        private readonly IDataProtector _protector;

        public MercadoPagoOAuthStateService(IDataProtectionProvider dataProtectionProvider)
        {
            _protector = dataProtectionProvider.CreateProtector("tiendubi.mercadopago.oauth.state.v2");
        }

        public string CreateState(int photographerId, string codeVerifier)
        {
            var payload = $"{photographerId}|{Guid.NewGuid():N}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}|{codeVerifier}";
            var protectedPayload = _protector.Protect(payload);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(protectedPayload));
        }

        public bool TryReadState(string state, out int photographerId, out string codeVerifier)
        {
            photographerId = 0;
            codeVerifier = string.Empty;

            if (string.IsNullOrWhiteSpace(state))
                return false;

            try
            {
                var protectedPayload = Encoding.UTF8.GetString(Convert.FromBase64String(state));
                var payload = _protector.Unprotect(protectedPayload);
                var pieces = payload.Split('|', StringSplitOptions.TrimEntries);

                if (pieces.Length < 4)
                    return false;

                if (!int.TryParse(pieces[0], out photographerId) || photographerId <= 0)
                    return false;

                if (!long.TryParse(pieces[2], out var issuedAtUnix))
                    return false;

                var issuedAt = DateTimeOffset.FromUnixTimeSeconds(issuedAtUnix);
                if (issuedAt < DateTimeOffset.UtcNow.AddMinutes(-15))
                    return false;

                codeVerifier = pieces[3];
                if (codeVerifier.Length is < 43 or > 128)
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
