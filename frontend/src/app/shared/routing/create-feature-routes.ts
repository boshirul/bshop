import { Routes } from '@angular/router';
import { FeaturePlaceholder } from '../pages/feature-placeholder/feature-placeholder';

export function createFeatureRoutes(
  title: string,
  description: string,
  phase: string
): Routes {
  return [
    {
      path: '',
      component: FeaturePlaceholder,
      data: { title, description, phase }
    }
  ];
}
