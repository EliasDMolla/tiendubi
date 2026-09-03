import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export interface UploadItemSnapshot {
  fileName: string;
  fileSize: number;
  progress: number;
  status: 'pending' | 'uploading' | 'uploaded' | 'error';
  error?: string;
}

export interface UploadStateSnapshot {
  active: boolean;
  phase: 'idle' | 'transferring' | 'processing';
  progressPercent: number;
  elapsedSeconds: number;
  totalFiles: number;
  uploadedFiles: number;
  failedFiles: number;
  finished: boolean;
  items: UploadItemSnapshot[];
}

const INITIAL_STATE: UploadStateSnapshot = {
  active: false,
  phase: 'idle',
  progressPercent: 0,
  elapsedSeconds: 0,
  totalFiles: 0,
  uploadedFiles: 0,
  failedFiles: 0,
  finished: false,
  items: []
};

@Injectable({ providedIn: 'root' })
export class UploadStateService {
  private readonly _state$ = new BehaviorSubject<UploadStateSnapshot>({ ...INITIAL_STATE });

  readonly state$ = this._state$.asObservable();

  get snapshot(): UploadStateSnapshot {
    return this._state$.value;
  }

  get isActive(): boolean {
    return this._state$.value.active;
  }

  update(partial: Partial<UploadStateSnapshot>): void {
    this._state$.next({ ...this._state$.value, ...partial });
  }

  reset(): void {
    this._state$.next({ ...INITIAL_STATE, items: [] });
  }
}
