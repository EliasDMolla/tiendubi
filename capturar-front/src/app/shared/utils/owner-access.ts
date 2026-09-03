export const OWNER_EMAIL = 'elias.molla.cel@gmail.com';

export function isOwnerEmail(email?: string | null): boolean {
  return (email ?? '').trim().toLowerCase() === OWNER_EMAIL;
}
