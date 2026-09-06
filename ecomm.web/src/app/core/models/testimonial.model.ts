/** A customer quote shown on the home page (photo URL already resolved by the service). */
export interface Testimonial {
  testimonialId: number;
  name: string;
  roleOrCompany: string | null;
  quote: string;
  rating: number;
  photoUrl: string | null;
}

/** Full testimonial row for the admin editor. */
export interface AdminTestimonial {
  testimonialId: number;
  name: string;
  roleOrCompany: string | null;
  quote: string;
  rating: number;
  photoUrl: string | null;
  hasUpload: boolean;
  displayOrder: number;
  isActive: boolean;
}

export interface SaveTestimonialRequest {
  name: string;
  roleOrCompany: string | null;
  quote: string;
  rating: number;
  photoUrl: string | null;
  displayOrder: number;
  isActive: boolean;
}
