const isLocalHost =
	typeof window !== 'undefined' &&
	(window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1');

export const API_BASE_URL = isLocalHost
	? 'https://localhost:44349'
	: 'https://api.capturar.ordenapp.ar';

export const API_AUTH_URL = `${API_BASE_URL}/api/auth`;
