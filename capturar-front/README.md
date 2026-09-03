# Capturar Front

Frontend Angular standalone organizado para servir como base reutilizable en otros proyectos.

## Stack

- Angular 20 con componentes standalone
- Router con lazy loading por feature
- HttpClient con interceptor global de autenticación
- CSS plano por pantalla

## Scripts

```bash
npm start
npm run build
```

## Estructura

```text
src/app/
	core/
		auth/
		config/
		guards/
	features/
		auth/
		landing/
		market/
		panel/
		owner/
		payments/
	shared/
		utils/
```

## Criterio de arquitectura

### `core/`

Va solamente lo transversal a toda la app:

- autenticación
- configuración global
- guards reutilizables
- infraestructura compartida

### `features/`

Cada dominio vive aislado con su propia estructura.

- `pages/`: pantallas standalone de esa feature
- `data-access/`: servicios HTTP, modelos y estado de esa feature
- `guards/`: guards exclusivos de la feature
- `layout/`: shells o contenedores propios de la feature

### `shared/`

Va lo reutilizable que no pertenece a un dominio puntual.

- utilidades puras
- helpers
- componentes realmente compartidos si aparecen a futuro

## Convenciones

- Las páginas usan el sufijo `*-page.component.ts`
- Los modelos viven junto a su servicio o feature
- Las rutas raíz sólo orquestan features; la definición real de rutas vive dentro de cada feature
- Si algo se usa en varias features, primero evaluar `core/`; si no es infraestructura, evaluar `shared/`
- Evitar meter lógica de negocio global en `app.routes.ts`
- Evitar seguir creciendo páginas gigantes: si una pantalla mezcla varios flujos, extraer subcomponentes o estado por dominio

## Features actuales

- `auth`: login y registro
- `landing`: landing pública y SEO
- `market`: sitio público y compra de fotos
- `panel`: panel del fotógrafo
- `owner`: administración de owner
- `payments`: integraciones y callbacks de pago

## Build

La build de producción se valida con:

```bash
npm run build
```
