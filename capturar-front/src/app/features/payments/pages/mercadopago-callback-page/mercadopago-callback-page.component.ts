import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

@Component({
  selector: 'app-mercadopago-callback-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './mercadopago-callback-page.component.html'
})
export class MercadoPagoCallbackPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);

  status: 'success' | 'error' = 'error';
  message = 'No se pudo conectar la cuenta de MercadoPago.';

  ngOnInit(): void {
    this.route.queryParamMap.subscribe((params) => {
      const status = (params.get('status') || 'error').toLowerCase();
      const message = params.get('message');

      this.status = status === 'success' ? 'success' : 'error';
      this.message = message?.trim() || (this.status === 'success'
        ? 'Cuenta conectada correctamente.'
        : 'No se pudo conectar la cuenta de MercadoPago.');
    });
  }
}
