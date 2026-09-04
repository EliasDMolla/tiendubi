## Tiendubi API

API de Tiendubi con autenticación, gestión de usuarios, roles, planes y ventas.

### Características

- **Autenticación JWT** con refresh tokens
- **Verificación de email** para nuevos usuarios
- **Recuperación de contraseña** por email
- **OAuth con Google** (opcional)
- **Sistema de roles** (User, Admin, SuperAdmin)
- **Sistema de planes** (FREE, PRO_TRIAL, PRO)
- **Checkout de MercadoPago** para suscripción Pro
- **Panel de administración** con métricas y gestión de usuarios
- **Auditoría** de acciones administrativas

### Tecnologías

- .NET 9.0
- PostgreSQL con Entity Framework Core
- BCrypt para hash de contraseñas
- JWT Bearer Authentication

### Estructura del Proyecto

```
Admin.Entities/        # Entidades y contexto de base de datos
Admin.WebApi/          # Controllers, Services y Models
```

### Configuración

1. Copiá `appsettings.json.sample` a `appsettings.json`
2. Configurá la conexión base a PostgreSQL (`Host`, `Port`, `Username`, `Password`)
3. Definí `AppSettings:ProjectName` como nombre visible del proyecto
3. Configurá el secret JWT
4. Configurá SMTP para envío de emails
5. (Opcional) Configurá Google OAuth
6. (Opcional) Configurá MercadoPago para habilitar cobros reales

Ejemplo:

```json
{
	"ConnectionStrings": {
		"PostgresConnection": "Host=localhost;Port=5432;Database=tiendubi;Username=postgres;Password=your_password"
	},
	"AppSettings": {
		"ProjectName": "Tiendubi",
		"FrontendUrl": "http://localhost:4200"
	},
	"Payment": {
		"Enabled": true,
		"MonthlyPrice": 24999,
		"AnnualPrice": 239990,
		"Currency": "ARS",
		"TrialDays": 30
	},
	"MercadoPagoSettings": {
		"PublicKey": "APP_USR-...",
		"AccessToken": "APP_USR-...",
		"SuccessUrl": "http://localhost:4200/plans?payment=success",
		"FailureUrl": "http://localhost:4200/plans?payment=failure",
		"PendingUrl": "http://localhost:4200/plans?payment=pending",
		"NotificationUrl": "https://localhost:5001/api/mercadopago/notify"
	}
}
```

> El nombre de la base se toma del `Database` de `ConnectionStrings:PostgresConnection`. Para Supabase normalmente debe ser `Database=postgres`.

### Migraciones

```bash
# Crear migración inicial
dotnet ef migrations add InitialCreate --project Admin.Entities --startup-project Admin.WebApi

# Aplicar migración
dotnet ef database update --project Admin.Entities --startup-project Admin.WebApi
```

Con eso alcanza para crear/actualizar la base definida en `ConnectionStrings:PostgresConnection`.

### Ejecutar

```bash
dotnet run --project Admin.WebApi
```

La API estará disponible en `http://localhost:44349`

### Endpoints Principales

#### Auth
- `POST /api/auth/register` - Registrar nuevo usuario
- `POST /api/auth/login` - Iniciar sesión
- `POST /api/auth/refresh-token` - Renovar token
- `GET /api/auth/me` - Obtener usuario actual
- `POST /api/auth/forgot-password` - Solicitar reset de contraseña
- `POST /api/auth/reset-password` - Resetear contraseña

#### Admin (requiere rol Admin)
- `GET /api/admin/dashboard` - Métricas del dashboard
- `GET /api/admin/users` - Listar usuarios con filtros
- `GET /api/admin/users/{id}` - Detalle de usuario
- `PUT /api/admin/users/{id}/status` - Activar/desactivar usuario
- `PUT /api/admin/users/{id}/role` - Cambiar rol (SuperAdmin only)
- `PUT /api/admin/users/{id}/plan` - Cambiar plan
- `DELETE /api/admin/users/{id}` - Eliminar usuario
- `GET /api/admin/actions` - Log de acciones administrativas

#### Subscription (requiere login)
- `GET /api/subscription/status` - Estado de plan del usuario actual
- `POST /api/subscription/activate-trial` - Activar prueba PRO
- `POST /api/subscription/mercadopago/checkout` - Crear preferencia de checkout MP
- `POST /api/subscription/mercadopago/confirm` - Confirmar pago por `merchant_order_id` (retorno frontend)

#### Webhook MercadoPago
- `POST /api/mercadopago/notify?topic=merchant_order&id={id}` - Notificación MP

#### Settings públicos
- `GET /api/settings/public` - Configuraciones públicas consumidas por frontend
- `GET /api/settings/payment` - Estado de habilitación de pagos

#### Health
- `GET /api/health` - Liveness básico
- `GET /api/health/ready` - Readiness detallado (PostgreSQL + SMTP)

### Configuración rápida de MercadoPago

Desde la raíz del repo:

```powershell
.\set-mercadopago-config.ps1 -AccessToken "APP_USR-..." -PublicKey "APP_USR-..." -FrontendUrl "http://localhost:4200" -ApiPublicBaseUrl "https://TU_API_PUBLICA"
```

Esto habilita pagos y setea URLs de retorno/webhook automáticamente en `appsettings.json`.

### Variables de entorno recomendadas (producción)

Usá `__` para mapear secciones anidadas en .NET:

- `ConnectionStrings__PostgresConnection`
- `AppSettings__ProjectName`
- `AppSettings__FrontendUrl`
- `Jwt__Secret`
- `Email__SmtpHost`
- `Email__SmtpPort`
- `Email__SmtpUser`
- `Email__SmtpPassword`
- `Email__FromEmail`
- `Email__FromName`
- `Google__Enabled`
- `Google__ClientId`
- `Google__ClientSecret`
- `Payment__Enabled`
- `Payment__MonthlyPrice`
- `Payment__AnnualPrice`
- `Payment__Currency`
- `Payment__TrialDays`
- `MercadoPagoSettings__PublicKey`
- `MercadoPagoSettings__AccessToken`
- `MercadoPagoSettings__SuccessUrl`
- `MercadoPagoSettings__FailureUrl`
- `MercadoPagoSettings__PendingUrl`
- `MercadoPagoSettings__NotificationUrl`

### Seed Inicial

Para crear el primer usuario SuperAdmin, ejecutá SQL directamente:

```sql
INSERT INTO "Users" 
("Email", "PasswordHash", "FullName", "IsActive", "EmailVerified", "Role", "PlanType", "CreatedAt") 
VALUES 
('admin@admin.com', '$2a$10$hashedpassword', 'Super Admin', true, true, 2, 0, NOW());
```

O usá BCrypt para generar el hash de la contraseña.
