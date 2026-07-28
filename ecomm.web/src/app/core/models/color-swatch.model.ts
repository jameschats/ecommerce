export interface ColorSwatch {
  colorSwatchId: number;
  name: string;
  hexCode: string;
}

export interface SaveColorSwatchRequest {
  name: string;
  hexCode: string;
}
