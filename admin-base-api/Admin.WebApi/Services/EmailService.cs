using System.Net;
using System.Net.Mail;
using System.Linq;

namespace Admin.WebApi.Services;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string toEmail, string resetToken, string userName);
    Task SendWelcomeEmailAsync(string toEmail, string userName);
    Task SendPlanChangeEmailAsync(string toEmail, string userName, string oldPlan, string newPlan, bool isUpgrade);
    Task SendEmailVerificationAsync(string toEmail, string verificationToken, string userName);
    Task SendPurchasedPhotosEmailAsync(string toEmail, string buyerName, string eventName, string externalReference, IReadOnlyList<PhotoDeliveryLink> photoLinks);
    Task SendPurchaseProcessingEmailAsync(string toEmail, string buyerName, string externalReference);
    Task SendPhotoDeliveryIssueEmailAsync(string toEmail, string buyerName, string externalReference, string issueDescription);
    Task SendPhotoDeliveryExhaustedEmailAsync(string toEmail, string buyerName, string externalReference);
    Task SendDigitalProductDeliveryEmailAsync(string toEmail, string buyerName, string productName, string deliveryLink, string? buyerInstructions);
    Task SendDigitalAssetsDeliveryEmailAsync(string toEmail, string buyerName, string productName, string externalReference, IReadOnlyList<PhotoDeliveryLink> assetLinks);
    Task SendSellerTransferSaleEmailAsync(string toEmail, string sellerName, string productName, string buyerName, decimal totalAmount, string currency, string externalReference);
    Task SendBuyerTransferPendingEmailAsync(string toEmail, string buyerName, string productName, string externalReference);
}

public record PhotoDeliveryLink(int PhotoId, string FileName, string DownloadUrl);

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string resetToken, string userName)
    {
        var frontendUrl = (_configuration["AppSettings:FrontendUrl"] ?? "http://localhost:4200").TrimEnd('/');
        var resetLink = $"{frontendUrl}/reset-password?token={resetToken}";
        
        var subject = "Recuperación de contraseña";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #6366f1 0%, #8b5cf6 100%); padding: 30px; text-align: center; color: white; border-radius: 10px 10px 0 0; }}
        .content {{ background: #f9fafb; padding: 30px; border-radius: 0 0 10px 10px; }}
        .button {{ display: inline-block; padding: 12px 30px; background: #6366f1; color: white; text-decoration: none; border-radius: 6px; margin: 20px 0; }}
        .footer {{ text-align: center; color: #6b7280; font-size: 12px; margin-top: 20px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🔐 Recuperación de Contraseña</h1>
        </div>
        <div class='content'>
            <p>Hola {userName},</p>
            <p>Recibimos una solicitud para restablecer tu contraseña.</p>
            <p>Hacé clic en el siguiente botón para crear una nueva contraseña:</p>
            <p style='text-align: center;'>
                <a href='{resetLink}' class='button'>Restablecer Contraseña</a>
            </p>
            <p>O copiá y pegá este enlace en tu navegador:</p>
            <p style='word-break: break-all; color: #6366f1;'>{resetLink}</p>
            <p><strong>Este enlace expira en 1 hora.</strong></p>
            <p>Si no solicitaste este cambio, podés ignorar este email.</p>
        </div>
        <div class='footer'>
            <p>© 2026 Tiendubi</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(toEmail, subject, body);
    }

    public async Task SendWelcomeEmailAsync(string toEmail, string userName)
    {
        var subject = "¡Bienvenido! 🎉";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #6366f1 0%, #8b5cf6 100%); padding: 30px; text-align: center; color: white; border-radius: 10px 10px 0 0; }}
        .content {{ background: #f9fafb; padding: 30px; border-radius: 0 0 10px 10px; }}
        .footer {{ text-align: center; color: #6b7280; font-size: 12px; margin-top: 20px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>¡Bienvenido! 🎉</h1>
        </div>
        <div class='content'>
            <p>Hola {userName},</p>
            <p>¡Gracias por registrarte! Tu cuenta ha sido creada exitosamente.</p>
        </div>
        <div class='footer'>
            <p>© 2026 Tiendubi</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(toEmail, subject, body);
    }

    public async Task SendPlanChangeEmailAsync(string toEmail, string userName, string oldPlan, string newPlan, bool isUpgrade)
    {
        // Map plan enum names to friendly display names
        string GetPlanDisplayName(string plan) => plan switch
        {
            "PRO" => "PRO",
            "PRO_TRIAL" => "PRO Trial",
            "FREE" => "Free",
            _ => plan
        };

        var oldPlanDisplay = GetPlanDisplayName(oldPlan);
        var newPlanDisplay = GetPlanDisplayName(newPlan);

        var subject = isUpgrade 
            ? $"¡Tu plan fue actualizado a {newPlanDisplay}! 🚀" 
            : "Cambio de plan";

        var emoji = isUpgrade ? "🚀" : "📋";
        var title = isUpgrade ? "¡Plan Actualizado!" : "Cambio de Plan";

        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #6366f1 0%, #8b5cf6 100%); padding: 30px; text-align: center; color: white; border-radius: 10px 10px 0 0; }}
        .content {{ background: #f9fafb; padding: 30px; border-radius: 0 0 10px 10px; }}
        .footer {{ text-align: center; color: #6b7280; font-size: 12px; margin-top: 20px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>{emoji} {title}</h1>
        </div>
        <div class='content'>
            <p>Hola {userName},</p>
            <p>Te informamos que tu plan fue actualizado:</p>
            
            <div style='text-align: center; margin: 25px 0;'>
                <span style='color: #6b7280; font-size: 16px;'>{oldPlanDisplay}</span>
                <span style='margin: 0 15px; font-size: 20px;'>→</span>
                <span style='color: #6366f1; font-size: 16px; font-weight: bold;'>{newPlanDisplay}</span>
            </div>

            <p style='margin-top: 20px;'>Si tenés alguna consulta sobre este cambio, no dudes en contactarnos.</p>
            <p>¡Gracias por usar nuestro sistema!</p>
        </div>
        <div class='footer'>
            <p>© 2026 Tiendubi</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(toEmail, subject, body);
    }

    public async Task SendEmailVerificationAsync(string toEmail, string verificationToken, string userName)
    {
        var verifyLink = $"{_configuration["AppSettings:FrontendUrl"]}/auth/verify-email?token={verificationToken}";
        
        var subject = "Tu link de acceso a Tiendubi";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #3b82f6 0%, #1d4ed8 100%); padding: 30px; text-align: center; color: white; border-radius: 10px 10px 0 0; }}
        .content {{ background: #f9fafb; padding: 30px; border-radius: 0 0 10px 10px; }}
        .button {{ display: inline-block; padding: 14px 35px; background: #3b82f6; color: white; text-decoration: none; border-radius: 8px; margin: 20px 0; font-weight: bold; font-size: 16px; }}
        .footer {{ text-align: center; color: #6b7280; font-size: 12px; margin-top: 20px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>✉️ Validá tu cuenta</h1>
        </div>
        <div class='content'>
            <p>¡Hola {userName}!</p>
            <p>Gracias por registrarte en Tiendubi. Te mandamos este link de acceso para validar la cuenta y confirmar que este mail es tuyo.</p>
            <p style='text-align: center;'>
                <a href='{verifyLink}' class='button' style='color: white;'>Validar mi cuenta</a>
            </p>
            <p>O copiá y pegá este enlace en tu navegador:</p>
            <p style='word-break: break-all; color: #3b82f6;'>{verifyLink}</p>
            <p><strong>Este enlace expira en 24 horas.</strong></p>
            <p>Si no creaste esta cuenta, podés ignorar este email.</p>
        </div>
        <div class='footer'>
            <p>© 2026 Tiendubi</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(toEmail, subject, body);
    }

    public async Task SendPurchasedPhotosEmailAsync(string toEmail, string buyerName, string eventName, string externalReference, IReadOnlyList<PhotoDeliveryLink> photoLinks)
    {
        var safeBuyerName = string.IsNullOrWhiteSpace(buyerName) ? "Comprador" : buyerName.Trim();
        var safeEventName = string.IsNullOrWhiteSpace(eventName) ? "tu evento" : eventName.Trim();

        var linksHtml = string.Join(string.Empty, photoLinks.Select(link =>
            $"<li style='margin-bottom:10px;'><a href='{link.DownloadUrl}' style='color:#2563eb;text-decoration:none;' target='_blank'>📷 {WebUtility.HtmlEncode(link.FileName)}</a></li>"));

        var subject = "Tus fotos originales ya están listas";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #2563eb 0%, #1d4ed8 100%); padding: 30px; text-align: center; color: white; border-radius: 10px 10px 0 0; }}
        .content {{ background: #f9fafb; padding: 30px; border-radius: 0 0 10px 10px; }}
        .footer {{ text-align: center; color: #6b7280; font-size: 12px; margin-top: 20px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>✅ Pago confirmado</h1>
        </div>
        <div class='content'>
            <p>Hola {WebUtility.HtmlEncode(safeBuyerName)},</p>
            <p>Tu compra fue confirmada para <strong>{WebUtility.HtmlEncode(safeEventName)}</strong>.</p>
            <p>Acá tenés los enlaces para descargar tus fotos en calidad original:</p>
            <ul style='padding-left: 18px;'>
                {linksHtml}
            </ul>
            <p>Referencia de compra: <strong>{WebUtility.HtmlEncode(externalReference)}</strong></p>
            <p>Si algún enlace no funciona, respondé este correo para ayudarte.</p>
        </div>
        <div class='footer'>
            <p>© 2026 Tiendubi</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(toEmail, subject, body);
    }

    public async Task SendPhotoDeliveryIssueEmailAsync(string toEmail, string buyerName, string externalReference, string issueDescription)
    {
        var safeBuyerName = string.IsNullOrWhiteSpace(buyerName) ? "Comprador" : buyerName.Trim();
        var safeIssue = string.IsNullOrWhiteSpace(issueDescription) ? "No se pudo completar la entrega automática." : issueDescription.Trim();

        var subject = "Recibimos tu pago, estamos procesando la entrega";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #f59e0b 0%, #d97706 100%); padding: 30px; text-align: center; color: white; border-radius: 10px 10px 0 0; }}
        .content {{ background: #f9fafb; padding: 30px; border-radius: 0 0 10px 10px; }}
        .footer {{ text-align: center; color: #6b7280; font-size: 12px; margin-top: 20px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>⏳ Entrega en proceso</h1>
        </div>
        <div class='content'>
            <p>Hola {WebUtility.HtmlEncode(safeBuyerName)},</p>
            <p>Tu pago fue recibido correctamente, pero detectamos un inconveniente al preparar la entrega automática de tus fotos.</p>
            <p><strong>Referencia:</strong> {WebUtility.HtmlEncode(externalReference)}</p>
            <p><strong>Detalle:</strong> {WebUtility.HtmlEncode(safeIssue)}</p>
            <p>Ya lo registramos para revisión y te contactaremos a la brevedad para que recibas tus fotos.</p>
        </div>
        <div class='footer'>
            <p>© 2026 Tiendubi</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(toEmail, subject, body);
    }

    public async Task SendPurchaseProcessingEmailAsync(string toEmail, string buyerName, string externalReference)
    {
        var safeBuyerName = string.IsNullOrWhiteSpace(buyerName) ? "Comprador" : buyerName.Trim();

        var subject = "Recibimos tu pago, estamos preparando tus fotos";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #0ea5e9 0%, #2563eb 100%); padding: 30px; text-align: center; color: white; border-radius: 10px 10px 0 0; }}
        .content {{ background: #f9fafb; padding: 30px; border-radius: 0 0 10px 10px; }}
        .footer {{ text-align: center; color: #6b7280; font-size: 12px; margin-top: 20px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>✅ Pago recibido</h1>
        </div>
        <div class='content'>
            <p>Hola {WebUtility.HtmlEncode(safeBuyerName)},</p>
            <p>Confirmamos tu pago correctamente.</p>
            <p>Estamos preparando la entrega de tus fotos originales por este mismo medio.</p>
            <p><strong>Referencia:</strong> {WebUtility.HtmlEncode(externalReference)}</p>
            <p>Si tenés alguna duda, podés responder este correo.</p>
        </div>
        <div class='footer'>
            <p>© 2026 Tiendubi</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(toEmail, subject, body);
    }

    public async Task SendPhotoDeliveryExhaustedEmailAsync(string toEmail, string buyerName, string externalReference)
    {
        var safeBuyerName = string.IsNullOrWhiteSpace(buyerName) ? "Comprador" : buyerName.Trim();

        var subject = "Estamos gestionando manualmente la entrega de tu compra";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #f59e0b 0%, #b45309 100%); padding: 30px; text-align: center; color: white; border-radius: 10px 10px 0 0; }}
        .content {{ background: #f9fafb; padding: 30px; border-radius: 0 0 10px 10px; }}
        .footer {{ text-align: center; color: #6b7280; font-size: 12px; margin-top: 20px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>⚠️ Entrega en revisión</h1>
        </div>
        <div class='content'>
            <p>Hola {WebUtility.HtmlEncode(safeBuyerName)},</p>
            <p>Recibimos tu pago, pero no pudimos completar el envío automático de tus fotos tras varios intentos.</p>
            <p><strong>Referencia:</strong> {WebUtility.HtmlEncode(externalReference)}</p>
            <p>Ya lo estamos gestionando manualmente para que recibas tu compra lo antes posible.</p>
            <p>No necesitás hacer nada por ahora; te vamos a contactar por esta vía.</p>
        </div>
        <div class='footer'>
            <p>© 2026 Tiendubi</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(toEmail, subject, body);
    }

    public async Task SendDigitalProductDeliveryEmailAsync(string toEmail, string buyerName, string productName, string deliveryLink, string? buyerInstructions)
    {
        var safeBuyerName = string.IsNullOrWhiteSpace(buyerName) ? "Comprador" : buyerName.Trim();
        var safeProductName = string.IsNullOrWhiteSpace(productName) ? "tu producto" : productName.Trim();
        var safeLink = WebUtility.HtmlEncode(deliveryLink.Trim());

        var instructionsHtml = string.IsNullOrWhiteSpace(buyerInstructions)
            ? string.Empty
            : $"<div style='background:#f1f5f9;padding:16px;border-radius:8px;margin:20px 0;white-space:pre-line;'>{WebUtility.HtmlEncode(buyerInstructions.Trim())}</div>";

        var subject = "Tu producto digital ya está disponible";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #7c3aed 0%, #a855f7 100%); padding: 30px; text-align: center; color: white; border-radius: 10px 10px 0 0; }}
        .content {{ background: #f9fafb; padding: 30px; border-radius: 0 0 10px 10px; }}
        .button {{ display: inline-block; padding: 14px 35px; background: #7c3aed; color: white; text-decoration: none; border-radius: 8px; margin: 20px 0; font-weight: bold; font-size: 16px; }}
        .footer {{ text-align: center; color: #6b7280; font-size: 12px; margin-top: 20px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>✅ Compra confirmada</h1>
        </div>
        <div class='content'>
            <p>Hola {WebUtility.HtmlEncode(safeBuyerName)},</p>
            <p>Tu compra de <strong>{WebUtility.HtmlEncode(safeProductName)}</strong> ya está confirmada.</p>
            <p style='text-align: center;'>
                <a href='{safeLink}' class='button' style='color: white;' target='_blank'>Acceder a mi producto</a>
            </p>
            <p>Si el botón no funciona, copiá y pegá este enlace en tu navegador:</p>
            <p style='word-break: break-all; color: #7c3aed;'>{safeLink}</p>
            {instructionsHtml}
        </div>
        <div class='footer'>
            <p>© 2026 Tiendubi</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(toEmail, subject, body);
    }

    public async Task SendDigitalAssetsDeliveryEmailAsync(string toEmail, string buyerName, string productName, string externalReference, IReadOnlyList<PhotoDeliveryLink> assetLinks)
    {
        var safeBuyerName = string.IsNullOrWhiteSpace(buyerName) ? "Comprador" : buyerName.Trim();
        var safeProductName = string.IsNullOrWhiteSpace(productName) ? "tu producto" : productName.Trim();

        var linksHtml = string.Join(string.Empty, assetLinks.Select(link =>
            $"<li style='margin-bottom:10px;'><a href='{link.DownloadUrl}' style='color:#7c3aed;text-decoration:none;' target='_blank'>📦 {WebUtility.HtmlEncode(link.FileName)}</a></li>"));

        var subject = "Tus archivos digitales están listos";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #7c3aed 0%, #a855f7 100%); padding: 30px; text-align: center; color: white; border-radius: 10px 10px 0 0; }}
        .content {{ background: #f9fafb; padding: 30px; border-radius: 0 0 10px 10px; }}
        .footer {{ text-align: center; color: #6b7280; font-size: 12px; margin-top: 20px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>✅ Compra confirmada</h1>
        </div>
        <div class='content'>
            <p>Hola {WebUtility.HtmlEncode(safeBuyerName)},</p>
            <p>Tu compra de <strong>{WebUtility.HtmlEncode(safeProductName)}</strong> ya está confirmada. Descargá tus archivos:</p>
            <ul style='list-style: none; padding: 0;'>{linksHtml}</ul>
            <p><strong>Referencia:</strong> {WebUtility.HtmlEncode(externalReference)}</p>
            <p>Los enlaces de descarga expiran en 24 horas.</p>
        </div>
        <div class='footer'>
            <p>© 2026 Tiendubi</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(toEmail, subject, body);
    }

    public async Task SendSellerTransferSaleEmailAsync(string toEmail, string sellerName, string productName, string buyerName, decimal totalAmount, string currency, string externalReference)
    {
        var safeSellerName = string.IsNullOrWhiteSpace(sellerName) ? "Vendedor" : sellerName.Trim();
        var safeProductName = string.IsNullOrWhiteSpace(productName) ? "tu producto" : productName.Trim();
        var safeBuyerName = string.IsNullOrWhiteSpace(buyerName) ? "Comprador" : buyerName.Trim();

        var subject = "Nueva venta por transferencia para aprobar";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #f59e0b 0%, #d97706 100%); padding: 30px; text-align: center; color: white; border-radius: 10px 10px 0 0; }}
        .content {{ background: #f9fafb; padding: 30px; border-radius: 0 0 10px 10px; }}
        .footer {{ text-align: center; color: #6b7280; font-size: 12px; margin-top: 20px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>💰 Venta pendiente de aprobación</h1>
        </div>
        <div class='content'>
            <p>Hola {WebUtility.HtmlEncode(safeSellerName)},</p>
            <p>Recibiste una nueva compra por <strong>transferencia</strong> que está esperando tu aprobación:</p>
            <ul>
                <li><strong>Producto:</strong> {WebUtility.HtmlEncode(safeProductName)}</li>
                <li><strong>Comprador:</strong> {WebUtility.HtmlEncode(safeBuyerName)}</li>
                <li><strong>Monto:</strong> {totalAmount:0.00} {WebUtility.HtmlEncode(currency)}</li>
                <li><strong>Referencia:</strong> {WebUtility.HtmlEncode(externalReference)}</li>
            </ul>
            <p>Cuando verifiques el pago, aprobá la compra desde tu panel para que se libere la entrega al comprador.</p>
        </div>
        <div class='footer'>
            <p>© 2026 Tiendubi</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(toEmail, subject, body);
    }

    public async Task SendBuyerTransferPendingEmailAsync(string toEmail, string buyerName, string productName, string externalReference)
    {
        var safeBuyerName = string.IsNullOrWhiteSpace(buyerName) ? "Comprador" : buyerName.Trim();
        var safeProductName = string.IsNullOrWhiteSpace(productName) ? "tu producto" : productName.Trim();

        var subject = "Recibimos tu pago, en breve liberamos tu compra";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #2563eb 0%, #1d4ed8 100%); padding: 30px; text-align: center; color: white; border-radius: 10px 10px 0 0; }}
        .content {{ background: #f9fafb; padding: 30px; border-radius: 0 0 10px 10px; }}
        .footer {{ text-align: center; color: #6b7280; font-size: 12px; margin-top: 20px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>⏳ Pago en verificación</h1>
        </div>
        <div class='content'>
            <p>Hola {WebUtility.HtmlEncode(safeBuyerName)},</p>
            <p>Recibimos tu comprobante de transferencia para <strong>{WebUtility.HtmlEncode(safeProductName)}</strong>.</p>
            <p>El vendedor está verificando el pago. En cuanto lo apruebe, recibirás tu producto automáticamente por este medio.</p>
            <p><strong>Referencia:</strong> {WebUtility.HtmlEncode(externalReference)}</p>
        </div>
        <div class='footer'>
            <p>© 2026 Tiendubi</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(toEmail, subject, body);
    }

    private async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        try
        {
            var smtpHost = _configuration["Email:SmtpHost"];
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
            var smtpUser = _configuration["Email:SmtpUser"];
            var smtpPassword = _configuration["Email:SmtpPassword"];
            var fromEmail = _configuration["Email:FromEmail"] ?? smtpUser;
            var fromName = _configuration["Email:FromName"] ?? "Tiendubi";

            if (string.IsNullOrWhiteSpace(smtpHost) ||
                string.IsNullOrWhiteSpace(smtpUser) ||
                string.IsNullOrWhiteSpace(smtpPassword) ||
                string.IsNullOrWhiteSpace(fromEmail))
            {
                throw new InvalidOperationException("SMTP no configurado. Completá Email:SmtpHost, Email:SmtpUser, Email:SmtpPassword y Email:FromEmail.");
            }

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPassword),
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail ?? smtpUser, fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage);
            _logger.LogInformation($"Email enviado exitosamente a {toEmail}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error enviando email a {toEmail}");
            throw;
        }
    }
}
