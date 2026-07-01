import { PagedResult } from './api-response.model';

export interface ProductReview {
  reviewId: number;
  author: string;
  rating: number;
  title: string | null;
  comment: string | null;
  isVerifiedPurchase: boolean;
  createdAt: string;
}

export interface ReviewSummary {
  average: number;
  count: number;
  distribution: number[]; // [0]=1★ … [4]=5★
}

export interface ProductReviews {
  summary: ReviewSummary;
  reviews: PagedResult<ProductReview>;
}

export interface SubmitReviewRequest {
  productId: number;
  rating: number;
  title: string | null;
  comment: string | null;
}

export interface AdminReview {
  reviewId: number;
  productId: number;
  productName: string;
  author: string;
  rating: number;
  title: string | null;
  comment: string | null;
  isApproved: boolean;
  isVerifiedPurchase: boolean;
  createdAt: string;
}
