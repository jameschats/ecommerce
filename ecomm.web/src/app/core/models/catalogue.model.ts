/** A downloadable PDF catalogue — file URL already absolute. */
export interface Catalogue {
  catalogueId: number;
  title: string;
  fileUrl: string;
  fileName: string;
  fileSizeBytes: number;
  displayOrder: number;
  isActive: boolean;
}

export interface CatalogueUpdateRequest {
  title: string;
  displayOrder: number;
  isActive: boolean;
}
