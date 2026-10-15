// The API is a sibling IIS application rather than a child of the Angular SPA.
// Keep this relative to the host so local IIS installations do not need a CORS
// exception or an environment-specific hostname.
export const environment = {
  apiBaseUrl: '/BShopApi/api'
};
