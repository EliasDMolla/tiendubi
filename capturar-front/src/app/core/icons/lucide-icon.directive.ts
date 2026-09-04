import { Directive, ElementRef, Input, OnChanges, Renderer2 } from '@angular/core';
import { TIENDUBI_ICONS } from './lucide-icons';

const SVG_NAMESPACE = 'svg';

@Directive({
  selector: '[data-lucide]',
  standalone: true
})
export class LucideIconDirective implements OnChanges {
  @Input('data-lucide') iconName = '';

  constructor(
    private readonly elementRef: ElementRef<HTMLElement>,
    private readonly renderer: Renderer2
  ) {}

  ngOnChanges(): void {
    this.renderIcon();
  }

  private renderIcon(): void {
    const host = this.elementRef.nativeElement;
    const registryKey = this.toRegistryKey(this.iconName);
    const iconNode = TIENDUBI_ICONS[registryKey as keyof typeof TIENDUBI_ICONS];

    while (host.firstChild) {
      this.renderer.removeChild(host, host.firstChild);
    }

    if (!iconNode) {
      return;
    }

    this.renderer.setStyle(host, 'display', 'inline-flex');
    this.renderer.setStyle(host, 'align-items', 'center');
    this.renderer.setStyle(host, 'justify-content', 'center');
    this.renderer.setStyle(host, 'line-height', '0');

    const svg = this.renderer.createElement('svg', SVG_NAMESPACE) as SVGElement;
    const svgAttributes: Record<string, string> = {
      xmlns: 'http://www.w3.org/2000/svg',
      width: '100%',
      height: '100%',
      viewBox: '0 0 24 24',
      fill: 'none',
      stroke: 'currentColor',
      'stroke-width': '2',
      'stroke-linecap': 'round',
      'stroke-linejoin': 'round',
      'aria-hidden': 'true',
      focusable: 'false'
    };

    for (const [name, value] of Object.entries(svgAttributes)) {
      this.renderer.setAttribute(svg, name, value);
    }

    for (const [tagName, attributes] of iconNode) {
      const child = this.renderer.createElement(tagName, SVG_NAMESPACE) as SVGElement;
      for (const [name, value] of Object.entries(attributes)) {
        this.renderer.setAttribute(child, name, String(value));
      }
      this.renderer.appendChild(svg, child);
    }

    this.renderer.appendChild(host, svg);
  }

  private toRegistryKey(iconName: string): string {
    return iconName
      .trim()
      .split('-')
      .filter(Boolean)
      .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
      .join('');
  }
}
