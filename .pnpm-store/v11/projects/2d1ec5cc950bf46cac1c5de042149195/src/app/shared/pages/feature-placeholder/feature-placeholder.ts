import { Component, input } from '@angular/core';
import { MatCardModule } from '@angular/material/card';

@Component({
  selector: 'app-feature-placeholder',
  imports: [MatCardModule],
  template: `
    <mat-card appearance="outlined">
      <mat-card-header>
        <mat-card-title>{{ title() }}</mat-card-title>
        <mat-card-subtitle>{{ phase() }}</mat-card-subtitle>
      </mat-card-header>
      <mat-card-content>
        <p>{{ description() }}</p>
        <p class="status">Foundation route ready. Business implementation is pending.</p>
      </mat-card-content>
    </mat-card>
  `,
  styles: `
    mat-card {
      max-width: 52rem;
    }

    p {
      line-height: 1.6;
    }

    .status {
      color: #516170;
      font-size: 0.9rem;
    }
  `
})
export class FeaturePlaceholder {
  readonly title = input('Feature');
  readonly description = input('This feature will be implemented in a later phase.');
  readonly phase = input('Planned');
}
