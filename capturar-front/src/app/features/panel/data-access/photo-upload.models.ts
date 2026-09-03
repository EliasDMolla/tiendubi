export interface PresignedUrlsRequest {
  eventId: number;
  fileNames: string[];
}

export interface PresignedUrlItem {
  fileName: string;
  objectKey: string;
  uploadUrl: string;
}

export interface PresignedUrlsResponse {
  eventId: number;
  files: PresignedUrlItem[];
}

export interface ConfirmUploadFileItem {
  fileName: string;
  objectKey: string;
  sizeBytes: number;
}

export interface ConfirmUploadRequest {
  eventId: number;
  files: ConfirmUploadFileItem[];
}

export interface ConfirmUploadResponse {
  eventId: number;
  savedCount: number;
  photoIds: number[];
  missingFiles: ConfirmUploadFileItem[];
}

export interface ProxyUploadResponse {
  eventId: number;
  objectKey: string;
  fileName: string;
  sizeBytes: number;
}

export interface DownloadPhotoResponse {
  photoId: number;
  downloadUrl: string;
  expiresInSeconds: number;
}

export interface PhotoProcessingItem {
  photoId: number;
  fileName: string;
  isProcessed: boolean;
  isFailed: boolean;
  failureReason?: string | null;
  createdAt: string;
}

export interface PhotoProcessingStatusResponse {
  eventId: number;
  totalPhotos: number;
  processedPhotos: number;
  failedPhotos: number;
  pendingPhotos: number;
  progressPercent: number;
  recentPhotos: PhotoProcessingItem[];
}

export interface BatchProcessingStatusRequest {
  photoIds: number[];
}

export interface PhotographerGalleryPhotoItem {
  photoId: number;
  fileName: string;
  isProcessed: boolean;
  isFailed: boolean;
  failureReason?: string | null;
  thumbnailUrl: string;
  previewUrl: string;
  tags: string[];
  createdAt: string;
}

export interface UpdatePhotoTagsResponse {
  eventId: number;
  photoId: number;
  tags: string[];
}

export interface PhotographerGalleryResponse {
  eventId: number;
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  items: PhotographerGalleryPhotoItem[];
}
