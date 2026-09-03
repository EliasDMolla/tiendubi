# MercadoPago Marketplace Split (OAuth) - Implementación

## Arquitectura aplicada

### Backend (.NET 9)
- `Controllers`
  - `Admin.WebApi/Controllers/PaymentsMercadoPagoController.cs`
- `Services`
  - `Admin.WebApi/Services/MercadoPagoService.cs`
  - `Admin.WebApi/Services/ISecretProtector.cs`
  - `Admin.WebApi/Services/DataProtectionSecretProtector.cs`
- `Infrastructure`
  - `Admin.WebApi/Infrastructure/MercadoPago/MercadoPagoClient.cs`
  - `Admin.WebApi/Infrastructure/MercadoPago/MercadoPagoOAuthStateService.cs`
- `Repositories`
  - `Admin.WebApi/Repositories/PhotographerMercadoPagoAccountRepository.cs`
- `DTOs`
  - `Admin.WebApi/Models/Payments/MercadoPagoOAuthModels.cs`
- `Entities + EF`
  - `Admin.Entities/Entities/PhotographerMercadoPagoAccount.cs`
  - `Admin.Entities/Context.cs`
  - `Admin.Entities/Migrations/20260221093000_AddPhotographerMercadoPagoAccount.cs`

### Frontend (Angular)
- `Settings`
  - `capturar-front/src/app/pages/settings-page/settings-page.component.ts`
  - `capturar-front/src/app/pages/settings-page/settings-page.component.html`
- `Callback`
  - `capturar-front/src/app/pages/mercadopago-callback-page/mercadopago-callback-page.component.ts`
  - `capturar-front/src/app/pages/mercadopago-callback-page/mercadopago-callback-page.component.html`
- `Core service`
  - `capturar-front/src/app/core/payments/mercadopago.service.ts`
  - `capturar-front/src/app/core/payments/mercadopago.models.ts`
- `Router`
  - `capturar-front/src/app/app.routes.ts`

## Configuración (`appsettings.json`)

```json
{
  "MercadoPagoSettings": {
    "ClientId": "TU_CLIENT_ID",
    "ClientSecret": "TU_CLIENT_SECRET",
    "RedirectUri": "https://api.tudominio.com/api/payments/mercadopago/callback",
    "CommissionPercentage": 25,
    "PublicKey": "APP_USR-...",
    "AccessToken": "APP_USR-...",
    "SuccessUrl": "https://tudominio.com/plans?payment=success",
    "FailureUrl": "https://tudominio.com/plans?payment=failure",
    "PendingUrl": "https://tudominio.com/plans?payment=pending",
    "NotificationUrl": "https://api.tudominio.com/api/mercadopago/notify"
  },
  "DataProtection": {
    "KeysPath": "/app/keys"
  }
}
```

## Endpoints nuevos

### 1) Iniciar OAuth
`GET /api/payments/mercadopago/connect`

Respuesta:
```json
{
  "authorizationUrl": "https://auth.mercadopago.com.ar/authorization?..."
}
```

### 2) Callback OAuth
`GET /api/payments/mercadopago/callback?code=XXX&state=YYY`

- Intercambia `code` por tokens en `POST https://api.mercadopago.com/oauth/token`
- Guarda en DB tokens cifrados, `public_key`, `user_id`, expiración
- Redirige a frontend:
  - éxito: `/mercadopago/callback?status=success&message=...`
  - error: `/mercadopago/callback?status=error&message=...`

### 3) Estado de conexión
`GET /api/payments/mercadopago/status`

### 4) Crear pago split
`POST /api/payments/mercadopago/payment`

## Ejemplo real de request de pago (Split con `application_fee`)

Request al backend:
```http
POST /api/payments/mercadopago/payment
Authorization: Bearer {JWT_FOTOGRAFO}
Content-Type: application/json

{
  "photographerId": 42,
  "totalAmount": 10000,
  "description": "Venta de fotos evento boda",
  "paymentMethodId": "visa",
  "payerEmail": "cliente@email.com",
  "payerFirstName": "Ana",
  "payerLastName": "Pérez",
  "token": "CARD_TOKEN_MP",
  "installments": 1
}
```

Payload enviado a MercadoPago (`v1/payments`, con `Authorization: Bearer SELLER_ACCESS_TOKEN`):
```json
{
  "transaction_amount": 10000,
  "description": "Venta de fotos evento boda",
  "payment_method_id": "visa",
  "token": "CARD_TOKEN_MP",
  "installments": 1,
  "application_fee": 2500,
  "payer": {
    "email": "cliente@email.com",
    "first_name": "Ana",
    "last_name": "Pérez"
  }
}
```

## Seguridad implementada
- `ClientSecret` solo backend
- No se guardan tokens MercadoPago en frontend ni `localStorage`
- `access_token` y `refresh_token` cifrados con DataProtection en DB
- `state` OAuth firmado/cifrado y con expiración
- `PhotographerId` validado contra usuario autenticado en endpoint de pago
- Refresh automático de token antes de crear pago si está vencido o por vencer

## Operación en Docker/VPS
- `docker-compose.yml` monta volumen para claves de DataProtection:
  - `capturar_webapi_keys:/app/keys`
- Esto evita perder la capacidad de descifrar tokens tras reinicios del contenedor.

## Migración EF
Aplicar:
```bash
dotnet ef database update --project Admin.Entities/Admin.Entities.csproj --startup-project Admin.WebApi/Admin.WebApi.csproj
```
