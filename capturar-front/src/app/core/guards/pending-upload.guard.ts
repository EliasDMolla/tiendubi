import { CanDeactivateFn } from '@angular/router';

interface PendingUploadAwareComponent {
  canLeavePage: () => boolean;
}

export const pendingUploadGuard: CanDeactivateFn<PendingUploadAwareComponent> = (component) => {
  return component.canLeavePage();
};
