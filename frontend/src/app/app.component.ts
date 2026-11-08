import { ChangeDetectionStrategy, Component, effect, inject, OnInit } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { PublicBrandService } from './branding/public-brand.service';

@Component({
  selector: 'crecisol-root',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app.component.html'
})
export class AppComponent implements OnInit {
  readonly brand = inject(PublicBrandService);
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);

  constructor() {
    effect(() => {
      const identity = this.brand.identity();
      if (!identity) return;
      this.title.setTitle(identity.name + ' · ' + identity.slogan);
      this.meta.updateTag({ name: 'description', content: identity.descriptor });
    });
  }

  ngOnInit(): void {
    this.brand.load();
  }
}
